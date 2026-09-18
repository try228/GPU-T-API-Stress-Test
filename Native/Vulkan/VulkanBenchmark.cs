using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;

using MouseButton = Silk.NET.Input.MouseButton;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// High-performance native Vulkan compute stress test and silicon metric benchmark engine.
/// </summary>
public sealed unsafe class VulkanStressBenchmark : IDisposable
{
    private const int WinWidth = PixelUiEngine.BaseWidth;
    private const int WinHeight = PixelUiEngine.BaseHeight;
    private const int InFlightFrames = 2;

    private const nuint BufferSize = 128 * 1024 * 1024; // 128 MB SSBO

    private uint _wgAluAndL1;
    private uint _wgL3Cache;
    private uint _wgVramBus;
    private uint _wgMatrix;

    private volatile bool _isBenchmarking = false;
    private volatile int _targetDurationSec = 0;

    private string _customInputBuffer = "";
    private bool _isCustomFocused = false;
    private bool _isSettingsOpen = false;

    private readonly Stopwatch _benchTimer = new();
    private ulong _totalSubmits = 0;

    private readonly Dictionary<string, double> _liveTestDps = new();
    private readonly Dictionary<string, ulong> _totalDispatchesByTest = new();
    private readonly Dictionary<string, ulong> _lastDispatchesSnapshot = new();
    private double _lastDpsMeasureTime = 0.0;

    private volatile string[] _activeTestSnapshot = [];

    private int _mouseX = 0;
    private int _mouseY = 0;

    private Vk _vk = null!;
    private Instance _instance;
    private ulong _debugMessenger = 0;

    private PhysicalDevice _physicalDevice;
    private Device _device;

    private Queue _computeQueue;
    private uint _computeQueueFamilyIndex;

    private string _deviceName = "Vulkan GPU";
    private readonly object _queueLock = new();

    private CommandPool _cmdPool;
    private readonly Dictionary<string, CommandBuffer[]> _testCmdBuffers = new();
    private readonly Fence[] _inFlightFences = new Fence[InFlightFrames];

    private DescriptorPool _descPool;
    private DescriptorSetLayout _descLayout;
    private DescriptorSet _descSet;
    private PipelineLayout _pipeLayout;

    private readonly Dictionary<string, (Pipeline Pipeline, ShaderModule Module)> _pipelines = new();

    private VkBuffer _ssboBuffer;
    private DeviceMemory _ssboMemory;

    private VulkanCapabilityProbe _capabilityProbe = null!;
    private VulkanTestRegistry _testRegistry = null!;

    private CancellationTokenSource? _computeCts;
    private Thread? _computeThread;
    private IInputContext? _inputContext;

    private readonly record struct WorkloadInfo(string Id, double OpsPerInvocation, string Unit);

    private static readonly Dictionary<string, WorkloadInfo> _workloads = new()
    {
        ["alu_fp32"] = new("alu_fp32", VulkanShaders.FP32_OPS_PER_INVOCATION, "FLOP"),
        ["alu_fp16"] = new("alu_fp16", VulkanShaders.FP16_OPS_PER_INVOCATION, "FLOP"),
        ["alu_bf16"] = new("alu_bf16", VulkanShaders.BF16_OPS_PER_INVOCATION, "FLOP"),
        ["alu_fp64"] = new("alu_fp64", VulkanShaders.FP64_OPS_PER_INVOCATION, "FLOP"),
        ["alu_int32"] = new("alu_int32", VulkanShaders.INT32_OPS_PER_INVOCATION, "IOP"),
        ["alu_int16"] = new("alu_int16", VulkanShaders.INT16_OPS_PER_INVOCATION, "IOP"),
        ["alu_int64"] = new("alu_int64", VulkanShaders.INT64_OPS_PER_INVOCATION, "IOP"),
        ["alu_int8"]  = new("alu_int8",  VulkanShaders.INT8_OPS_PER_INVOCATION, "IOP"),
        ["alu_dp4a"]  = new("alu_dp4a",  VulkanShaders.DP4A_OPS_PER_INVOCATION, "IOP"),
        ["alu_dp2a"]  = new("alu_dp2a",  VulkanShaders.DP2A_OPS_PER_INVOCATION, "IOP"),
        ["mat_fp16"]  = new("mat_fp16",  4096.0, "FLOP"),
        ["mat_bf16"]  = new("mat_bf16",  4096.0, "FLOP"),
        ["mat_int8"]  = new("mat_int8",  4096.0, "IOP")
    };

    private static int GetBatchCount(string id) => id switch
    {
        "alu_fp32" or "alu_fp16" or "alu_bf16" or "alu_int32" or "alu_int16" or "alu_int8" => 64,
        "alu_dp4a" or "alu_dp2a" => 64,
        "mat_fp16" or "mat_bf16" or "mat_int8" => 32,
        "alu_fp64" or "alu_int64" => 16,
        "mem_l1l2" => 64,
        "mem_l3"   => 32,
        "mem_bw"   => 8,
        "mem_lat"  => 1,
        _ => 16
    };

    private bool HasPipelineFor(string id) => id switch
    {
        "mem_l1l2" or "mem_l3" or "mem_bw" => _pipelines.ContainsKey("mem_stream"),
        "mem_lat" => _pipelines.ContainsKey("mem_lat"),
        _ => _pipelines.ContainsKey(id)
    };

    public static void Run(
        CancellationToken hostCt,
        int initialDuration = 0,
        int selectedGpuIndex = 0,
        MockGpuTarget mockTarget = MockGpuTarget.None,
        bool enableDebug = false)
    {
        using var benchmark = new VulkanStressBenchmark(initialDuration, selectedGpuIndex, mockTarget, enableDebug);
        benchmark.Execute(hostCt);
    }

    public VulkanStressBenchmark(
        int initialDuration = 0,
        int selectedGpuIndex = 0,
        MockGpuTarget mockTarget = MockGpuTarget.None,
        bool enableDebug = false)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        _targetDurationSec = initialDuration;
        _customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";

        bool envHasValidation = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VK_INSTANCE_LAYERS"));
        InitializeVulkan(selectedGpuIndex, mockTarget, enableDebug || envHasValidation);
    }

    private void InitializeVulkan(int selectedGpuIndex, MockGpuTarget mockTarget, bool enableDebug)
    {
        _vk = Vk.GetApi();

        List<string> instExtensions = new();
        if (enableDebug) instExtensions.Add("VK_EXT_debug_utils");

        byte** ppInstExts = stackalloc byte*[Math.Max(1, instExtensions.Count)];
        for (int i = 0; i < instExtensions.Count; i++)
            ppInstExts[i] = (byte*)SilkMarshal.StringToPtr(instExtensions[i]);

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("GPU-T.Stress"),
            ApplicationVersion = Vk.MakeVersion(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("GPU-T"),
            EngineVersion = Vk.MakeVersion(1, 0, 0),
            ApiVersion = Vk.Version11
        };

        InstanceCreateInfo instInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)instExtensions.Count,
            PpEnabledExtensionNames = instExtensions.Count > 0 ? ppInstExts : null,
            EnabledLayerCount = 0,
            PpEnabledLayerNames = null
        };

        Result instRes = _vk.CreateInstance(&instInfo, null, out _instance);
        if (instRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan Instance: {instRes}");

        for (int i = 0; i < instExtensions.Count; i++) SilkMarshal.Free((nint)ppInstExts[i]);
        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);

        uint devCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, &devCount, null);
        if (devCount == 0) throw new InvalidOperationException("No Vulkan physical devices found.");

        PhysicalDevice* pDevs = stackalloc PhysicalDevice[(int)devCount];
        Unsafe.InitBlock(pDevs, 0, (uint)(sizeof(PhysicalDevice) * devCount));
        _vk.EnumeratePhysicalDevices(_instance, &devCount, pDevs);

        int gpuIdx = Math.Clamp(selectedGpuIndex, 0, (int)devCount - 1);
        _physicalDevice = pDevs[gpuIdx];

        _capabilityProbe = new VulkanCapabilityProbe(_vk, _physicalDevice, _instance, mockTarget);
        _testRegistry = new VulkanTestRegistry(_capabilityProbe);
        _deviceName = _capabilityProbe.DeviceName;

        uint l2Bytes = _capabilityProbe.L2CacheSizeBytes;
        _wgAluAndL1 = Math.Clamp((uint)((l2Bytes * 0.75) / 256), 2048, 65536);
        _wgMatrix = Math.Clamp(_wgAluAndL1 / 2, 2048, 16384);

        if (_capabilityProbe.HasL3InfinityCache)
        {
            uint l3Bytes = _capabilityProbe.L3CacheSizeBytes;
            _wgL3Cache = Math.Clamp((uint)((l3Bytes * 0.75) / 256), 16384, 524288);
        }
        else
        {
            _wgL3Cache = 0;
        }

        _wgVramBus = (uint)(BufferSize / 256);

        uint qfCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &qfCount, null);
        QueueFamilyProperties* pQf = stackalloc QueueFamilyProperties[(int)qfCount];
        Unsafe.InitBlock(pQf, 0, (uint)(sizeof(QueueFamilyProperties) * qfCount));
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &qfCount, pQf);

        bool foundComputeQueue = false;
        for (uint i = 0; i < qfCount; i++)
        {
            if (pQf[i].QueueFlags.HasFlag(QueueFlags.ComputeBit))
            {
                _computeQueueFamilyIndex = i;
                foundComputeQueue = true;
                break;
            }
        }

        if (!foundComputeQueue) throw new InvalidOperationException("No Vulkan compute queue found.");

        InitializeLogicalDevice();

        BufferCreateInfo bufInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            PNext = null,
            Size = BufferSize,
            Usage = BufferUsageFlags.StorageBufferBit,
            SharingMode = SharingMode.Exclusive
        };

        _vk.CreateBuffer(_device, &bufInfo, null, out _ssboBuffer);

        MemoryRequirements memReqs;
        _vk.GetBufferMemoryRequirements(_device, _ssboBuffer, &memReqs);

        PhysicalDeviceMemoryProperties memProps;
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, &memProps);

        uint memTypeIdx = uint.MaxValue;
        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((memReqs.MemoryTypeBits & (1 << (int)i)) == 0) continue;
            if ((memProps.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0)
            {
                memTypeIdx = i;
                break;
            }
        }

        if (memTypeIdx == uint.MaxValue) throw new InvalidOperationException("No suitable device-local memory type.");

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            PNext = null,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = memTypeIdx
        };

        _vk.AllocateMemory(_device, &allocInfo, null, out _ssboMemory);
        _vk.BindBufferMemory(_device, _ssboBuffer, _ssboMemory, 0);

        DescriptorSetLayoutBinding binding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit
        };

        DescriptorSetLayoutCreateInfo dslInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            PNext = null,
            BindingCount = 1,
            PBindings = &binding
        };

        _vk.CreateDescriptorSetLayout(_device, &dslInfo, null, out _descLayout);

        var dsl = _descLayout;
        PipelineLayoutCreateInfo plInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            PNext = null,
            SetLayoutCount = 1,
            PSetLayouts = &dsl
        };

        _vk.CreatePipelineLayout(_device, &plInfo, null, out _pipeLayout);

        DescriptorPoolSize poolSize = new() { Type = DescriptorType.StorageBuffer, DescriptorCount = 1 };
        DescriptorPoolCreateInfo dpInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PNext = null,
            MaxSets = 1,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize
        };

        _vk.CreateDescriptorPool(_device, &dpInfo, null, out _descPool);

        DescriptorSetAllocateInfo dsAlloc = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            PNext = null,
            DescriptorPool = _descPool,
            DescriptorSetCount = 1,
            PSetLayouts = &dsl
        };

        fixed (DescriptorSet* pSet = &_descSet)
        {
            _vk.AllocateDescriptorSets(_device, &dsAlloc, pSet);
        }

        DescriptorBufferInfo descBufInfo = new() { Buffer = _ssboBuffer, Offset = 0, Range = BufferSize };
        var ds = _descSet;
        WriteDescriptorSet writeDs = new()
        {
            SType = StructureType.WriteDescriptorSet,
            PNext = null,
            DstSet = ds,
            DstBinding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            PBufferInfo = &descBufInfo
        };

        _vk.UpdateDescriptorSets(_device, 1, &writeDs, 0, null);

        InitializePipelines();

        CommandPoolCreateInfo cpInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            PNext = null,
            QueueFamilyIndex = _computeQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };

        _vk.CreateCommandPool(_device, &cpInfo, null, out _cmdPool);

        for (int i = 0; i < InFlightFrames; i++)
        {
            FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, PNext = null, Flags = FenceCreateFlags.SignaledBit };
            _vk.CreateFence(_device, &fenceInfo, null, out _inFlightFences[i]);
        }
    }

    private void InitializeLogicalDevice()
    {
        List<string> devExtensions = new();
        void* pNextChain = null;

        VkPhysicalDeviceShaderFloat16Int8FeaturesKHR float16Features = default;
        float16Features.sType = VulkanConstants.StructureTypePhysicalDeviceShaderFloat16Int8FeaturesKHR;
        float16Features.shaderFloat16 = _capabilityProbe.HasFloat16 ? 1u : 0u;
        float16Features.shaderInt8 = _capabilityProbe.HasInt8 ? 1u : 0u;

        if (_capabilityProbe.HasFloat16 || _capabilityProbe.HasInt8)
        {
            devExtensions.Add("VK_KHR_shader_float16_int8");
            float16Features.pNext = pNextChain;
            pNextChain = &float16Features;
        }

        VkPhysicalDeviceShaderIntegerDotProductFeaturesKHR dotFeatures = default;
        dotFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceShaderIntegerDotProductFeaturesKHR;
        dotFeatures.shaderIntegerDotProduct = _capabilityProbe.HasIntegerDotProduct ? 1u : 0u;

        if (_capabilityProbe.HasIntegerDotProduct)
        {
            devExtensions.Add("VK_KHR_shader_integer_dot_product");
            dotFeatures.pNext = pNextChain;
            pNextChain = &dotFeatures;
        }

        VkPhysicalDeviceShaderBfloat16FeaturesKHR bf16Features = default;
        bf16Features.sType = VulkanConstants.StructureTypePhysicalDeviceShaderBfloat16FeaturesKHR;

        if (_capabilityProbe.HasBFloat16Extension)
        {
            PhysicalDeviceFeatures2 queryFeat2 = new()
            {
                SType = StructureType.PhysicalDeviceFeatures2,
                PNext = &bf16Features
            };
            _vk.GetPhysicalDeviceFeatures2(_physicalDevice, &queryFeat2);

            if (bf16Features.shaderBFloat16Type != 0)
            {
                bf16Features.pNext = pNextChain;
                pNextChain = &bf16Features;
                devExtensions.Add("VK_KHR_shader_bfloat16");
            }
        }

        VkPhysicalDeviceFeatures2KHR feat2 = default;
        feat2.sType = VulkanConstants.StructureTypePhysicalDeviceFeatures2KHR;
        feat2.features.ShaderFloat64 = _capabilityProbe.HasFloat64;
        feat2.features.ShaderInt64 = _capabilityProbe.HasInt64;
        feat2.features.ShaderInt16 = _capabilityProbe.HasInt16;
        feat2.pNext = pNextChain;

        byte** ppDevExts = stackalloc byte*[Math.Max(1, devExtensions.Count)];
        for (int i = 0; i < devExtensions.Count; i++)
            ppDevExts[i] = (byte*)SilkMarshal.StringToPtr(devExtensions[i]);

        float qPri = 1.0f;
        DeviceQueueCreateInfo qInfo = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            PNext = null,
            QueueFamilyIndex = _computeQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &qPri
        };

        DeviceCreateInfo devInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &feat2,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &qInfo,
            EnabledExtensionCount = (uint)devExtensions.Count,
            PpEnabledExtensionNames = devExtensions.Count > 0 ? ppDevExts : null,
            PEnabledFeatures = null
        };

        Result devRes = _vk.CreateDevice(_physicalDevice, &devInfo, null, out _device);
        if (devRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan Device: {devRes}");

        for (int i = 0; i < devExtensions.Count; i++)
            SilkMarshal.Free((nint)ppDevExts[i]);

        _vk.GetDeviceQueue(_device, _computeQueueFamilyIndex, 0, out _computeQueue);
    }

    private Pipeline CreateComputePipeline(uint[] spirvCode, out ShaderModule shaderModule)
    {
        shaderModule = default;
        if (spirvCode == null || spirvCode.Length == 0) return default;

        fixed (uint* pCode = spirvCode)
        {
            ShaderModuleCreateInfo smInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                PNext = null,
                CodeSize = (nuint)(spirvCode.Length * sizeof(uint)),
                PCode = pCode
            };

            Result res = _vk.CreateShaderModule(_device, &smInfo, null, out shaderModule);
            if (res != Result.Success) return default;
        }

        byte* pMain = stackalloc byte[] { (byte)'m', (byte)'a', (byte)'i', (byte)'n', 0 };
        PipelineShaderStageCreateInfo stageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            PNext = null,
            Stage = ShaderStageFlags.ComputeBit,
            Module = shaderModule,
            PName = pMain,
            Flags = 0
        };

        ComputePipelineCreateInfo pipeInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            PNext = null,
            Stage = stageInfo,
            Layout = _pipeLayout,
            Flags = 0,
            BasePipelineHandle = default,
            BasePipelineIndex = -1
        };

        Pipeline pipeline = default;
        Result pipeRes = _vk.CreateComputePipelines(_device, default, 1, &pipeInfo, null, &pipeline);

        if (pipeRes != Result.Success)
        {
            if (shaderModule.Handle != 0)
            {
                _vk.DestroyShaderModule(_device, shaderModule, null);
                shaderModule = default;
            }
            return default;
        }

        return pipeline;
    }

    private void SafeRegisterPipeline(string id, uint[] spirv)
    {
        try
        {
            var p = CreateComputePipeline(spirv, out var sm);
            if (p.Handle != 0) _pipelines[id] = (p, sm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vulkan] Skipping pipeline '{id}': {ex.Message}");
        }
    }

    private void InitializePipelines()
    {
        SafeRegisterPipeline("alu_fp32", VulkanShaders.GetShaderFP32());
        SafeRegisterPipeline("alu_int32", VulkanShaders.GetShaderINT32());
        SafeRegisterPipeline("mem_stream", VulkanShaders.GetShaderMem());
        SafeRegisterPipeline("mem_lat", VulkanShaders.GetShaderLatency());

        if (_capabilityProbe.HasFloat64)
            SafeRegisterPipeline("alu_fp64", VulkanShaders.GetShaderFP64());

        if (_capabilityProbe.HasInt64)
            SafeRegisterPipeline("alu_int64", VulkanShaders.GetShaderINT64());

        if (_capabilityProbe.HasFloat16)
            SafeRegisterPipeline("alu_fp16", VulkanShaders.GetShaderFP16());

        SafeRegisterPipeline("alu_bf16", VulkanShaders.GetShaderBF16(_capabilityProbe));

        if (_capabilityProbe.HasInt16)
            SafeRegisterPipeline("alu_int16", VulkanShaders.GetShaderINT16());

        if (_capabilityProbe.HasInt8)
            SafeRegisterPipeline("alu_int8", VulkanShaders.GetShaderINT8());

        if (_capabilityProbe.HasIntegerDotProduct)
        {
            SafeRegisterPipeline("alu_dp4a", VulkanShaders.GetShaderDP4A());
            if (_capabilityProbe.HasInt16)
                SafeRegisterPipeline("alu_dp2a", VulkanShaders.GetShaderDP2A());
        }

        SafeRegisterPipeline("mat_fp16", VulkanShaders.GetShaderMatFP16());
        SafeRegisterPipeline("mat_bf16", VulkanShaders.GetShaderMatBF16());
        SafeRegisterPipeline("mat_int8", VulkanShaders.GetShaderMatINT8());
    }

    private void DispatchLinear(CommandBuffer cb, uint totalWorkGroups)
    {
        if (totalWorkGroups == 0) return;

        if (totalWorkGroups <= 65535)
        {
            _vk.CmdDispatch(cb, totalWorkGroups, 1, 1);
        }
        else
        {
            uint dimX = 32768;
            uint dimY = (totalWorkGroups + dimX - 1) / dimX;
            _vk.CmdDispatch(cb, dimX, dimY, 1);
        }
    }

    private void RecordCommandBuffers()
    {
        var activeList = new List<string>();
        foreach (var item in _testRegistry.Items)
        {
            if (item.IsChecked && item.IsSupported && HasPipelineFor(item.Id))
            {
                if (item.Id == "mem_l3" && !_capabilityProbe.HasL3InfinityCache) continue;
                activeList.Add(item.Id);
            }
        }

        _activeTestSnapshot = activeList.ToArray();
        if (_activeTestSnapshot.Length == 0) return;

        CommandBufferBeginInfo cbBegin = new() { SType = StructureType.CommandBufferBeginInfo, PNext = null, Flags = CommandBufferUsageFlags.SimultaneousUseBit };
        var ds = _descSet;

        foreach (var testId in _activeTestSnapshot)
        {
            if (!_testCmdBuffers.TryGetValue(testId, out var buffers))
            {
                buffers = new CommandBuffer[InFlightFrames];
                CommandBufferAllocateInfo cbAlloc = new()
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    PNext = null,
                    CommandPool = _cmdPool,
                    Level = CommandBufferLevel.Primary,
                    CommandBufferCount = 1
                };
                for (int i = 0; i < InFlightFrames; i++)
                    _vk.AllocateCommandBuffers(_device, &cbAlloc, out buffers[i]);

                _testCmdBuffers[testId] = buffers;
            }

            int batch = GetBatchCount(testId);
            Pipeline pipe = testId switch
            {
                "mem_l1l2" or "mem_l3" or "mem_bw" => _pipelines["mem_stream"].Pipeline,
                "mem_lat" => _pipelines["mem_lat"].Pipeline,
                _ => _pipelines[testId].Pipeline
            };

            uint wgCount = testId switch
            {
                "mem_l1l2" => _wgAluAndL1,
                "mem_l3" => _wgL3Cache,
                "mem_bw" => _wgVramBus,
                "mem_lat" => 1024,
                "mat_fp16" or "mat_bf16" or "mat_int8" => _wgMatrix,
                _ => _wgAluAndL1
            };

            for (int frame = 0; frame < InFlightFrames; frame++)
            {
                _vk.BeginCommandBuffer(buffers[frame], &cbBegin);
                _vk.CmdBindPipeline(buffers[frame], PipelineBindPoint.Compute, pipe);
                _vk.CmdBindDescriptorSets(buffers[frame], PipelineBindPoint.Compute, _pipeLayout, 0, 1, &ds, 0, null);

                for (int d = 0; d < batch; d++) DispatchLinear(buffers[frame], wgCount);

                _vk.EndCommandBuffer(buffers[frame]);
            }
        }
    }

    private double GetWorkloadTops(string id, double dispatchesPerSecond)
    {
        if (!_workloads.TryGetValue(id, out var workload)) return 0.0;
        
        uint wgCount = id.StartsWith("mat_") ? _wgMatrix : _wgAluAndL1;
        double totalInvocationsPerDispatch = wgCount * VulkanShaders.LocalSizeX;
        double operationsPerDispatch = workload.OpsPerInvocation * totalInvocationsPerDispatch;
        return (dispatchesPerSecond * operationsPerDispatch) / 1e12;
    }

    private void Execute(CancellationToken hostCt)
    {
        _computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);

        _computeThread = new Thread(() =>
        {
            int testIdx = 0;
            int burstCounter = 0;
            const int BurstsPerTest = 8;

            string[] frameTestId = new string[InFlightFrames];
            int frameIndex = 0;

            while (!_computeCts.Token.IsCancellationRequested)
            {
                var currentTests = _activeTestSnapshot;
                if (!_isBenchmarking || currentTests.Length == 0)
                {
                    Thread.Sleep(10);
                    Array.Clear(frameTestId);
                    burstCounter = 0;
                    continue;
                }

                lock (_queueLock)
                {
                    var fence = _inFlightFences[frameIndex];
                    _vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue);
                    _vk.ResetFences(_device, 1, &fence);

                    string completedTest = frameTestId[frameIndex];

                    // Учитываем честно выполненный батч диспатчей
                    if (!string.IsNullOrEmpty(completedTest))
                    {
                        int batch = GetBatchCount(completedTest);
                        lock (_totalDispatchesByTest)
                        {
                            _totalDispatchesByTest[completedTest] = _totalDispatchesByTest.GetValueOrDefault(completedTest) + (ulong)batch;
                        }
                    }

                    if (currentTests.Length > 0)
                    {
                        string nextTest = currentTests[testIdx % currentTests.Length];
                        burstCounter++;
                        if (burstCounter >= BurstsPerTest)
                        {
                            burstCounter = 0;
                            testIdx++;
                        }

                        if (_testCmdBuffers.TryGetValue(nextTest, out var cbs))
                        {
                            var scb = cbs[frameIndex];
                            SubmitInfo stressSubmit = new()
                            {
                                SType = StructureType.SubmitInfo,
                                PNext = null,
                                CommandBufferCount = 1,
                                PCommandBuffers = &scb
                            };

                            frameTestId[frameIndex] = nextTest;

                            Result submitResult = _vk.QueueSubmit(_computeQueue, 1, &stressSubmit, fence);
                            if (submitResult == Result.Success)
                            {
                                Interlocked.Increment(ref _totalSubmits);
                            }
                        }
                    }

                    frameIndex = (frameIndex + 1) % InFlightFrames;
                }
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        _computeThread.Start();

        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Vulkan Compute Benchmark";
        winOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        winOptions.VSync = true;

        using var window = Window.Create(winOptions);
        GL? gl = null;

        uint uiProgram = 0;
        uint vao = 0;
        uint vbo = 0;
        uint uiTexture = 0;

        uint[] uiPixels = new uint[WinWidth * WinHeight];
        float animTime = 0f;

        window.Load += () =>
        {
            gl = GL.GetApi(window);
            gl.Disable(EnableCap.DepthTest);
            gl.ClearColor(0.08f, 0.09f, 0.12f, 1.0f);

            string vsSource = """
                #version 330 core
                layout(location = 0) in vec2 aPos;
                layout(location = 1) in vec2 aTexCoord;
                out vec2 TexCoord;
                void main() { gl_Position = vec4(aPos, 0.0, 1.0); TexCoord = aTexCoord; }
                """;

            string fsSource = """
                #version 330 core
                in vec2 TexCoord;
                out vec4 FragColor;
                uniform sampler2D uUiTexture;
                void main() { FragColor = texture(uUiTexture, TexCoord); }
                """;

            uiProgram = CreateGlProgram(gl, vsSource, fsSource);
            gl.UseProgram(uiProgram);

            float[] quadVertices = new float[]
            {
                -1.0f, -1.0f, 0.0f, 1.0f,
                 1.0f, -1.0f, 1.0f, 1.0f,
                 1.0f,  1.0f, 1.0f, 0.0f,
                -1.0f, -1.0f, 0.0f, 1.0f,
                 1.0f,  1.0f, 1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f, 0.0f
            };

            vao = gl.GenVertexArray();
            vbo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

            fixed (float* pVerts = quadVertices)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quadVertices.Length * sizeof(float)), pVerts, BufferUsageARB.StaticDraw);
            }

            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            uiTexture = gl.GenTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, uiTexture);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)WinWidth, (uint)WinHeight, 0, PixelFormat.Bgra, PixelType.UnsignedByte, (void*)0);

            _inputContext = window.CreateInput();
            foreach (var mouse in _inputContext.Mice)
            {
                mouse.MouseMove += (m, pos) => { _mouseX = (int)pos.X; _mouseY = (int)pos.Y; };
                mouse.MouseDown += (m, btn) => { if (btn == MouseButton.Left) HandleMouseClick(_mouseX, _mouseY); };
            }
            foreach (var kb in _inputContext.Keyboards)
            {
                kb.KeyDown += (k, key, code) =>
                {
                    if (key == Key.Space) ToggleBenchmark();
                    else if (key == Key.Escape) window.Close();
                };
            }
        };

        window.Update += delta =>
        {
            if (hostCt.IsCancellationRequested) window.Close();
            if (_isBenchmarking && _targetDurationSec > 0 && _benchTimer.Elapsed.TotalSeconds >= _targetDurationSec)
            {
                StopBenchmark();
            }
        };

        window.Render += delta =>
        {
            if (gl == null) return;
            if (_isBenchmarking) animTime += 0.02f;

            double elapsed = _isBenchmarking ? _benchTimer.Elapsed.TotalSeconds : 0.0;

            // Расчет DPS на скользящем окне 250 мс
            double nowSec = _benchTimer.Elapsed.TotalSeconds;
            double dt = nowSec - _lastDpsMeasureTime;
            if (_isBenchmarking && dt >= 0.25)
            {
                lock (_totalDispatchesByTest)
                {
                    foreach (var kvp in _totalDispatchesByTest)
                    {
                        ulong current = kvp.Value;
                        ulong prev = _lastDispatchesSnapshot.TryGetValue(kvp.Key, out var val) ? val : 0;
                        double dps = (current - prev) / dt;
                        lock (_liveTestDps)
                        {
                            _liveTestDps[kvp.Key] = dps;
                        }
                        _lastDispatchesSnapshot[kvp.Key] = current;
                    }
                }
                _lastDpsMeasureTime = nowSec;
            }

            double GetDps(string id)
            {
                if (!_isBenchmarking) return 0.0;
                lock (_liveTestDps)
                {
                    return _liveTestDps.TryGetValue(id, out double d) ? d : 0.0;
                }
            }

            double fp32Tflops = _testRegistry.IsActive("alu_fp32") ? GetWorkloadTops("alu_fp32", GetDps("alu_fp32")) : 0.0;
            double fp16Tflops = _testRegistry.IsActive("alu_fp16") ? GetWorkloadTops("alu_fp16", GetDps("alu_fp16")) : 0.0;
            double bf16Tflops = _testRegistry.IsActive("alu_bf16") ? GetWorkloadTops("alu_bf16", GetDps("alu_bf16")) : 0.0;
            double fp64Tflops = _testRegistry.IsActive("alu_fp64") ? GetWorkloadTops("alu_fp64", GetDps("alu_fp64")) : 0.0;
            double int32Tiops = _testRegistry.IsActive("alu_int32") ? GetWorkloadTops("alu_int32", GetDps("alu_int32")) : 0.0;
            double int16Tiops = _testRegistry.IsActive("alu_int16") ? GetWorkloadTops("alu_int16", GetDps("alu_int16")) : 0.0;
            double int64Tiops = _testRegistry.IsActive("alu_int64") ? GetWorkloadTops("alu_int64", GetDps("alu_int64")) : 0.0;
            double int8Tiops  = _testRegistry.IsActive("alu_int8")  ? GetWorkloadTops("alu_int8", GetDps("alu_int8")) : 0.0;
            double dp4aTops   = _testRegistry.IsActive("alu_dp4a")  ? GetWorkloadTops("alu_dp4a", GetDps("alu_dp4a")) : 0.0;
            double dp2aTops   = _testRegistry.IsActive("alu_dp2a")  ? GetWorkloadTops("alu_dp2a", GetDps("alu_dp2a")) : 0.0;

            double matFp16Tops = _testRegistry.IsActive("mat_fp16") ? GetWorkloadTops("mat_fp16", GetDps("mat_fp16")) : 0.0;
            double matBf16Tops = _testRegistry.IsActive("mat_bf16") ? GetWorkloadTops("mat_bf16", GetDps("mat_bf16")) : 0.0;
            double matInt8Tops = _testRegistry.IsActive("mat_int8") ? GetWorkloadTops("mat_int8", GetDps("mat_int8")) : 0.0;

            double displayedTops = 0.0;
            if (_testRegistry.IsActive("mat_fp16")) displayedTops = matFp16Tops;
            else if (_testRegistry.IsActive("mat_bf16")) displayedTops = matBf16Tops;
            else if (_testRegistry.IsActive("mat_int8")) displayedTops = matInt8Tops;
            else if (_testRegistry.IsActive("alu_fp32")) displayedTops = fp32Tflops;
            else if (_testRegistry.IsActive("alu_fp16")) displayedTops = fp16Tflops;
            else if (_testRegistry.IsActive("alu_bf16")) displayedTops = bf16Tflops;
            else if (_testRegistry.IsActive("alu_dp4a")) displayedTops = dp4aTops;

            double l1l2BytesPerDispatch = _wgAluAndL1 * 512.0;
            double cacheL1L2Tbs = (_testRegistry.IsActive("mem_l1l2") && HasPipelineFor("mem_l1l2"))
                ? (GetDps("mem_l1l2") * l1l2BytesPerDispatch) / 1_000_000_000_000.0
                : 0.0;

            double l3BytesPerDispatch = _wgL3Cache * 512.0;
            double cacheL3Tbs = (_testRegistry.IsActive("mem_l3") && _capabilityProbe.HasL3InfinityCache && HasPipelineFor("mem_l3"))
                ? (GetDps("mem_l3") * l3BytesPerDispatch) / 1_000_000_000_000.0
                : 0.0;

            double vramBytesPerDispatch = _wgVramBus * 256.0;
            double memBandwidthGbs = (_testRegistry.IsActive("mem_bw") && HasPipelineFor("mem_bw"))
                ? (GetDps("mem_bw") * vramBytesPerDispatch) / 1_000_000_000.0
                : 0.0;

            double latDps = GetDps("mem_lat");
            double latencyNs = (_testRegistry.IsActive("mem_lat") && latDps > 0)
                ? (1_000_000_000.0 / (latDps * 1024.0))
                : 0.0;

            double totalActiveDps = 0;
            lock (_liveTestDps)
            {
                foreach (var d in _liveTestDps.Values) totalActiveDps += d;
            }

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "Vulkan 1.0 Compute", _deviceName, _isBenchmarking, _targetDurationSec,
                    _customInputBuffer, _isCustomFocused, elapsed, totalActiveDps, displayedTops, null, ThemePalette.Vulkan,
                    _mouseX, _mouseY, animTime, _isSettingsOpen, _testRegistry,
                    fp32Tflops, fp64Tflops, fp16Tflops, bf16Tflops, int32Tiops, int64Tiops, int16Tiops, int8Tiops,
                    dp2aTops, dp4aTops, 0.0, memBandwidthGbs, cacheL1L2Tbs, cacheL3Tbs, latencyNs,
                    matFp16Tops, matBf16Tops, matInt8Tops);

                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, uiTexture);
                gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)WinWidth, (uint)WinHeight, PixelFormat.Bgra, PixelType.UnsignedByte, pUi);
            }

            gl.Clear(ClearBufferMask.ColorBufferBit);
            gl.UseProgram(uiProgram);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, uiTexture);
            gl.BindVertexArray(vao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        };

        window.Run();
    }

    private void StartBenchmark()
    {
        if (!_testRegistry.HasActiveTests()) return;
        lock (_queueLock)
        {
            RecordCommandBuffers();
            _totalSubmits = 0;
            _liveTestDps.Clear();
            lock (_totalDispatchesByTest)
            {
                _totalDispatchesByTest.Clear();
                _lastDispatchesSnapshot.Clear();
            }
            _lastDpsMeasureTime = 0.0;
            _benchTimer.Restart();
            _isBenchmarking = true;
        }
    }

    private void StopBenchmark()
    {
        _isBenchmarking = false;
        _benchTimer.Stop();
        lock (_queueLock)
        {
            if (_device.Handle != 0) _vk.QueueWaitIdle(_computeQueue);
            _liveTestDps.Clear();
            lock (_totalDispatchesByTest)
            {
                _totalDispatchesByTest.Clear();
                _lastDispatchesSnapshot.Clear();
            }
        }
    }

    private void ToggleBenchmark()
    {
        if (_isBenchmarking) StopBenchmark();
        else StartBenchmark();
    }

    private void HandleMouseClick(int x, int y)
    {
        if (_isSettingsOpen)
        {
            PixelUiEngine.HandleSettingsClick(x, y, _testRegistry, ref _isSettingsOpen, _isBenchmarking);
            return;
        }

        if (PixelUiEngine.BtnSettings.Contains(x, y)) { _isSettingsOpen = true; return; }

        if (PixelUiEngine.BtnStartStop.Contains(x, y)) { _isCustomFocused = false; ToggleBenchmark(); }
        else if (PixelUiEngine.Btn10s.Contains(x, y)) { _targetDurationSec = 10; _customInputBuffer = "10"; _isCustomFocused = false; }
        else if (PixelUiEngine.Btn30s.Contains(x, y)) { _targetDurationSec = 30; _customInputBuffer = "30"; _isCustomFocused = false; }
        else if (PixelUiEngine.Btn60s.Contains(x, y)) { _targetDurationSec = 60; _customInputBuffer = "60"; _isCustomFocused = false; }
        else if (PixelUiEngine.BtnUnlimited.Contains(x, y)) { _targetDurationSec = 0; _customInputBuffer = ""; _isCustomFocused = false; }
        else if (PixelUiEngine.InputCustom.Contains(x, y)) { _isCustomFocused = true; }
        else if (PixelUiEngine.BtnMinus.Contains(x, y)) { _targetDurationSec = Math.Max(1, _targetDurationSec - 5); _customInputBuffer = _targetDurationSec.ToString(); _isCustomFocused = false; }
        else if (PixelUiEngine.BtnPlus.Contains(x, y)) { _targetDurationSec += 5; _customInputBuffer = _targetDurationSec.ToString(); _isCustomFocused = false; }
        else { _isCustomFocused = false; }
    }

    private static uint CreateGlProgram(GL gl, string vsSrc, string fsSrc)
    {
        uint vs = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vs, vsSrc);
        gl.CompileShader(vs);

        uint fs = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fs, fsSrc);
        gl.CompileShader(fs);

        uint program = gl.CreateProgram();
        gl.AttachShader(program, vs);
        gl.AttachShader(program, fs);
        gl.LinkProgram(program);

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return program;
    }

    public void Dispose()
    {
        _isBenchmarking = false;
        _computeCts?.Cancel();
        _computeThread?.Join();
        _computeCts?.Dispose();
        _inputContext?.Dispose();

        lock (_queueLock)
        {
            if (_device.Handle != 0) _vk.DeviceWaitIdle(_device);
        }

        if (_device.Handle != 0)
        {
            for (int i = 0; i < InFlightFrames; i++)
            {
                if (_inFlightFences[i].Handle != 0) _vk.DestroyFence(_device, _inFlightFences[i], null);
            }

            foreach (var pipeEntry in _pipelines.Values)
            {
                if (pipeEntry.Pipeline.Handle != 0) _vk.DestroyPipeline(_device, pipeEntry.Pipeline, null);
                if (pipeEntry.Module.Handle != 0) _vk.DestroyShaderModule(_device, pipeEntry.Module, null);
            }
            _pipelines.Clear();

            if (_pipeLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _pipeLayout, null);
            if (_descPool.Handle != 0) _vk.DestroyDescriptorPool(_device, _descPool, null);
            if (_descLayout.Handle != 0) _vk.DestroyDescriptorSetLayout(_device, _descLayout, null);
            if (_cmdPool.Handle != 0) _vk.DestroyCommandPool(_device, _cmdPool, null);
            if (_ssboBuffer.Handle != 0) _vk.DestroyBuffer(_device, _ssboBuffer, null);
            if (_ssboMemory.Handle != 0) _vk.FreeMemory(_device, _ssboMemory, null);

            _vk.DestroyDevice(_device, null);
        }

        if (_debugMessenger != 0)
        {
            nint pDestroy = SilkMarshal.StringToPtr("vkDestroyDebugUtilsMessengerEXT");
            var pfnDestroy = (delegate* unmanaged[Cdecl]<Instance, ulong, AllocationCallbacks*, void>)(void*)_vk.GetInstanceProcAddr(_instance, (byte*)pDestroy);
            SilkMarshal.Free(pDestroy);
            if (pfnDestroy != null) pfnDestroy(_instance, _debugMessenger, null);
        }

        if (_instance.Handle != 0) _vk.DestroyInstance(_instance, null);
        GC.SuppressFinalize(this);
    }
}