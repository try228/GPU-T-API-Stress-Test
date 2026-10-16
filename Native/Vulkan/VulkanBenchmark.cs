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
using Sampler = Silk.NET.Vulkan.Sampler;

using MouseButton = Silk.NET.Input.MouseButton;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace GPU_T.StressTest.Native.Vulkan;

/// <summary>
/// Native feature structure for VK_NV_cooperative_matrix.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceCooperativeMatrixFeaturesNV
{
    public StructureType sType;
    public void* pNext;
    public uint cooperativeMatrix;
}

/// <summary>
/// Native feature structure for VK_NV_cooperative_matrix2.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceCooperativeMatrix2FeaturesNV
{
    public StructureType sType;
    public void* pNext;
    public uint cooperativeMatrixWorkgroupScope;
    public uint cooperativeMatrixFlexibleDimensions;
    public uint cooperativeMatrixAddressing;
}

/// <summary>
/// Native feature structure for VK_EXT_shader_float8.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkPhysicalDeviceShaderFloat8FeaturesEXT
{
    public StructureType sType;
    public void* pNext;
    public uint shaderFloat8;
    public uint shaderFloat8CooperativeMatrix;
}

/// <summary>
/// Execution orchestrator for Vulkan compute and hardware Ray Tracing stress testing.
/// Adaptive to physical silicon architecture, memory hierarchy, and Vulkan 1.0 specifications.
/// </summary>
public sealed unsafe class VulkanStressBenchmark : IDisposable
{
    private const int WinWidth = PixelUiEngine.BaseWidth;
    private const int WinHeight = PixelUiEngine.BaseHeight;
    private const int InFlightFrames = 2;

    private ulong _totalVramBytes;
    private ulong _vramSweepSizeBytes;

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
    private ulong _totalCompletedDispatches = 0;
    private double _smoothedDps = 0.0;

    private readonly Dictionary<string, double> _liveTestDps = new();
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

    private Image _tmuImage2D;
    private DeviceMemory _tmuMem2D;
    private ImageView _tmuView2D;

    private Image _tmuImageBC;
    private DeviceMemory _tmuMemBC;
    private ImageView _tmuViewBC;

    private Image _tmuImage3D;
    private DeviceMemory _tmuMem3D;
    private ImageView _tmuView3D;

    private Sampler _samplerLinear;
    private Sampler _samplerAniso;
    private Sampler _sampler3D;

    private VulkanCapabilityProbe _capabilityProbe = null!;
    private VulkanTestRegistry _testRegistry = null!;
    private VulkanRtManager? _rtManager;

    private CancellationTokenSource? _computeCts;
    private Thread? _computeThread;
    private IInputContext? _inputContext;

    private readonly record struct WorkloadInfo(string Id, double OpsPerInvocation, string Unit);

    private static readonly Dictionary<string, WorkloadInfo> _workloads = new()
    {
        ["alu_fp32"]     = new("alu_fp32",     VulkanShaders.FP32_OPS_PER_INVOCATION,     "FLOP"),
        ["alu_fp16"]     = new("alu_fp16",     VulkanShaders.FP16_OPS_PER_INVOCATION,     "FLOP"),
        ["alu_bf16"]     = new("alu_bf16",     VulkanShaders.BF16_OPS_PER_INVOCATION,     "FLOP"),
        ["alu_fp64"]     = new("alu_fp64",     VulkanShaders.FP64_OPS_PER_INVOCATION,     "FLOP"),

        ["alu_int32"]    = new("alu_int32",    VulkanShaders.INT32_OPS_PER_INVOCATION,    "IOP"),
        ["alu_int16"]    = new("alu_int16",    VulkanShaders.INT16_OPS_PER_INVOCATION,    "IOP"),
        ["alu_int64"]    = new("alu_int64",    VulkanShaders.INT64_OPS_PER_INVOCATION,    "IOP"),
        ["alu_int8"]     = new("alu_int8",     VulkanShaders.INT8_OPS_PER_INVOCATION,     "IOP"),
        ["alu_dp4a"]     = new("alu_dp4a",     VulkanShaders.DP4A_OPS_PER_INVOCATION,     "IOP"),
        ["alu_dp2a"]     = new("alu_dp2a",     VulkanShaders.DP2A_OPS_PER_INVOCATION,     "IOP"),

        ["mat_fp32"]     = new("mat_fp32",     VulkanShaders.MAT_FP32_OPS_PER_INVOCATION, "FLOP"),
        ["mat_fp64"]     = new("mat_fp64",     VulkanShaders.MAT_FP64_OPS_PER_INVOCATION, "FLOP"),
        ["mat_fp16"]     = new("mat_fp16",     VulkanShaders.MAT_FP16_OPS_PER_INVOCATION, "FLOP"),
        ["mat_bf16"]     = new("mat_bf16",     VulkanShaders.MAT_BF16_OPS_PER_INVOCATION, "FLOP"),
        ["mat_fp8_e4m3"] = new("mat_fp8_e4m3", VulkanShaders.MAT_FP8_OPS_PER_INVOCATION,  "FLOP"),
        ["mat_fp8_e5m2"] = new("mat_fp8_e5m2", VulkanShaders.MAT_FP8_OPS_PER_INVOCATION,  "FLOP"),
        ["mat_int8"]     = new("mat_int8",     VulkanShaders.MAT_INT8_OPS_PER_INVOCATION, "IOP"),
        ["mat_int4"]     = new("mat_int4",     VulkanShaders.MAT_INT4_OPS_PER_INVOCATION, "IOP"),
        ["mat_int16"]    = new("mat_int16",    VulkanShaders.MAT_INT16_OPS_PER_INVOCATION,"IOP"),
        ["mat_int32"]    = new("mat_int32",    VulkanShaders.MAT_INT32_OPS_PER_INVOCATION,"IOP"),
        ["mat_int64"]    = new("mat_int64",    VulkanShaders.MAT_INT64_OPS_PER_INVOCATION,"IOP"),

        ["mat_mod_nv1"]  = new("mat_mod_nv1",  VulkanShaders.MAT_FP16_OPS_PER_INVOCATION, "FLOP"),
        ["mat_mod_nv2"]  = new("mat_mod_nv2",  VulkanShaders.MAT_FP16_OPS_PER_INVOCATION, "FLOP"),

        ["tmu_bdt"]      = new("tmu_bdt",      VulkanShaders.TMU_SAMPLES_PER_INVOCATION, "TEXEL"),
        ["tmu_aniso"]    = new("tmu_aniso",    VulkanShaders.TMU_SAMPLES_PER_INVOCATION, "TEXEL"),
        ["tmu_bc"]       = new("tmu_bc",       VulkanShaders.TMU_SAMPLES_PER_INVOCATION, "TEXEL"),
        ["tmu_3d"]       = new("tmu_3d",       VulkanShaders.TMU_SAMPLES_PER_INVOCATION, "TEXEL"),

        // Ray Tracing Workloads
        ["rt_query"]     = new("rt_query",     VulkanShaders.RT_RAYS_PER_INVOCATION,     "RAY"),
        ["rt_pipe"]      = new("rt_pipe",      VulkanShaders.RT_RAYS_PER_INVOCATION,     "RAY"),
        ["rt_ser"]       = new("rt_ser",       VulkanShaders.RT_RAYS_PER_INVOCATION,     "RAY"),
        ["rt_omm"]       = new("rt_omm",       VulkanShaders.RT_RAYS_PER_INVOCATION,     "RAY"),
        ["rt_pos"]       = new("rt_pos",       VulkanShaders.RT_RAYS_PER_INVOCATION,     "RAY"),
        ["rt_nv"]        = new("rt_nv",        VulkanShaders.RT_RAYS_PER_INVOCATION,     "RAY")
    };

    private static int GetBatchCount(string id) => id switch
    {
        "alu_fp32" or "alu_fp16" or "alu_bf16" or "alu_int32" or "alu_int16" or "alu_int8" => 256,
        "alu_dp4a" or "alu_dp2a" => 256,
        "mat_fp32" or "mat_fp16" or "mat_bf16" or "mat_int8" or "mat_int16" or "mat_int32" or "mat_fp8_e4m3" or "mat_fp8_e5m2" or "mat_int4" => 128,
        "alu_fp64" or "alu_int64" or "mat_fp64" or "mat_int64" or "mat_mod_nv1" or "mat_mod_nv2" => 128,
        "rt_query" or "rt_pipe" or "rt_nv" => 64,
        "tmu_bdt" or "tmu_aniso" or "tmu_bc" or "tmu_3d" => 32,
        "mem_l1l2" => 256,
        "mem_l3"   => 128,
        "mem_bw"   => 64,
        "mem_lat"  => 8,
        _ => 128
    };

    private bool HasPipelineFor(string id)
    {
        if (id.StartsWith("rt_") && id != "rt_nv")
        {
            if (_rtManager == null || !_rtManager.IsInitialized) return false;
        }

        if (_pipelines.ContainsKey(id)) return true;
        if (_pipelines.ContainsKey($"{id}_khr")) return true;
        if (_pipelines.ContainsKey($"{id}_nv1")) return true;
        if (_pipelines.ContainsKey($"{id}_nv2")) return true;

        if (id == "rt_query" && (_pipelines.ContainsKey("rt_query_omm") || _pipelines.ContainsKey("rt_query_pos"))) return true;
        if (id == "rt_pipe" && (_pipelines.ContainsKey("rt_pipe_ser") || _pipelines.ContainsKey("rt_pipe_omm") || _pipelines.ContainsKey("rt_pipe_pos"))) return true;

        return false;
    }

    /// <summary>
    /// Dynamically resolves the active pipeline based on runtime UI switches, modifiers, and silicon support.
    /// </summary>
    private Pipeline ResolvePipeline(string testId)
    {
        if (testId == "rt_query")
        {
            if (_testRegistry.IsActive("rt_omm") && _pipelines.TryGetValue("rt_query_omm", out var pOmm))
                return pOmm.Pipeline;
            if (_testRegistry.IsActive("rt_pos") && _pipelines.TryGetValue("rt_query_pos", out var pPos))
                return pPos.Pipeline;
            if (_pipelines.TryGetValue("rt_query", out var pDef))
                return pDef.Pipeline;
        }

        if (testId == "rt_pipe")
        {
            if (_testRegistry.IsActive("rt_ser") && _pipelines.TryGetValue("rt_pipe_ser", out var pSer))
                return pSer.Pipeline;
            if (_testRegistry.IsActive("rt_omm") && _pipelines.TryGetValue("rt_pipe_omm", out var pOmm))
                return pOmm.Pipeline;
            if (_testRegistry.IsActive("rt_pos") && _pipelines.TryGetValue("rt_pipe_pos", out var pPos))
                return pPos.Pipeline;
            if (_pipelines.TryGetValue("rt_pipe", out var pDef))
                return pDef.Pipeline;
        }

        if (testId.StartsWith("mat_") && testId != "mat_mod_nv1" && testId != "mat_mod_nv2")
        {
            bool forceNv1 = _testRegistry.IsActive("mat_mod_nv1");
            bool forceNv2 = _testRegistry.IsActive("mat_mod_nv2");

            if (forceNv1 && _pipelines.TryGetValue($"{testId}_nv1", out var pNv1))
                return pNv1.Pipeline;

            if (forceNv2 && _pipelines.TryGetValue($"{testId}_nv2", out var pNv2))
                return pNv2.Pipeline;

            if (_pipelines.TryGetValue($"{testId}_khr", out var pKhr))
                return pKhr.Pipeline;

            if (_pipelines.TryGetValue($"{testId}_nv2", out var pNv2Auto))
                return pNv2Auto.Pipeline;

            if (_pipelines.TryGetValue($"{testId}_nv1", out var pNv1Auto))
                return pNv1Auto.Pipeline;
        }

        return _pipelines.TryGetValue(testId, out var direct) ? direct.Pipeline : default;
    }

    private static int EvaluateDeviceScore(PhysicalDeviceProperties props, string devName)
    {
        bool isSoftware = props.DeviceType == PhysicalDeviceType.Cpu
            || devName.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase)
            || devName.Contains("lavapipe", StringComparison.OrdinalIgnoreCase)
            || devName.Contains("swiftshader", StringComparison.OrdinalIgnoreCase)
            || devName.Contains("virgl", StringComparison.OrdinalIgnoreCase)
            || devName.Contains("venus", StringComparison.OrdinalIgnoreCase);

        if (isSoftware) return -100;

        return props.DeviceType switch
        {
            PhysicalDeviceType.DiscreteGpu   => 1000,
            PhysicalDeviceType.IntegratedGpu => 500,
            PhysicalDeviceType.VirtualGpu    => -10,
            PhysicalDeviceType.Cpu           => -100,
            _                                => -50
        };
    }

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

        uint instExtCount = 0;
        _vk.EnumerateInstanceExtensionProperties((byte*)null, &instExtCount, null);
        var availInstExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (instExtCount > 0)
        {
            ExtensionProperties* pExtProps = stackalloc ExtensionProperties[(int)instExtCount];
            Unsafe.InitBlock(pExtProps, 0, (uint)(sizeof(ExtensionProperties) * instExtCount));
            _vk.EnumerateInstanceExtensionProperties((byte*)null, &instExtCount, pExtProps);
            for (uint i = 0; i < instExtCount; i++)
            {
                string name = Marshal.PtrToStringAnsi((nint)pExtProps[i].ExtensionName) ?? "";
                if (!string.IsNullOrEmpty(name)) availInstExts.Add(name);
            }
        }

        List<string> instExtensions = new();
        if (enableDebug && availInstExts.Contains("VK_EXT_debug_utils"))
            instExtensions.Add("VK_EXT_debug_utils");

        if (availInstExts.Contains("VK_KHR_get_physical_device_properties2"))
            instExtensions.Add("VK_KHR_get_physical_device_properties2");

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
            ApiVersion = Vk.Version10
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

        PhysicalDevice bestDevice = default;
        int bestScore = -1;
        string bestDeviceName = "";

        for (uint i = 0; i < devCount; i++)
        {
            PhysicalDeviceProperties props;
            _vk.GetPhysicalDeviceProperties(pDevs[i], &props);
            string devName = SilkMarshal.PtrToString((nint)props.DeviceName) ?? "";

            int score = EvaluateDeviceScore(props, devName);

            if (i == (uint)selectedGpuIndex)
            {
                if (score > 0)
                {
                    bestDevice = pDevs[i];
                    bestScore = score;
                    bestDeviceName = devName;
                    break;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestDevice = pDevs[i];
                bestDeviceName = devName;
            }
        }

        if (bestScore <= 0 || bestDevice.Handle == 0)
        {
            throw new NotSupportedException("No compatible physical GPU found! GPU-T requires dedicated hardware.");
        }

        _physicalDevice = bestDevice;
        _capabilityProbe = new VulkanCapabilityProbe(_vk, _physicalDevice, _instance, mockTarget);
        _testRegistry = new VulkanTestRegistry(_capabilityProbe);
        _deviceName = _capabilityProbe.DeviceName;

        PhysicalDeviceMemoryProperties memProps;
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, &memProps);
        ulong maxLocalHeap = 0;
        for (uint i = 0; i < memProps.MemoryHeapCount; i++)
        {
            if ((memProps.MemoryHeaps[(int)i].Flags & MemoryHeapFlags.DeviceLocalBit) != 0)
            {
                if (memProps.MemoryHeaps[(int)i].Size > maxLocalHeap)
                    maxLocalHeap = memProps.MemoryHeaps[(int)i].Size;
            }
        }
        _totalVramBytes = maxLocalHeap > 0 ? maxLocalHeap : 4UL * 1024 * 1024 * 1024;

        ulong maxCacheBytes = _capabilityProbe.L3CacheSizeBytes > 0
            ? _capabilityProbe.L3CacheSizeBytes
            : (_capabilityProbe.L2CacheSizeBytes > 0 ? _capabilityProbe.L2CacheSizeBytes : 4UL * 1024 * 1024);

        ulong targetSweep = Math.Max(256UL * 1024 * 1024, maxCacheBytes * 4UL);
        ulong maxAllowedSweep = Math.Clamp(_totalVramBytes / 4UL, 64UL * 1024 * 1024, 1024UL * 1024 * 1024);
        _vramSweepSizeBytes = Math.Min(targetSweep, maxAllowedSweep);
        _vramSweepSizeBytes = (_vramSweepSizeBytes + 0x00FFFFFFUL) & ~0x00FFFFFFUL;

        uint cuEstimate = Math.Clamp(_capabilityProbe.L2CacheSizeBytes / (64u * 1024u), 20u, 160u);
        _wgAluAndL1 = cuEstimate * 64u;
        _wgMatrix   = cuEstimate * 32u;
        _wgL3Cache  = cuEstimate * 64u;

        uint halfSweepBytes = (uint)(_vramSweepSizeBytes / 2UL);
        uint threadsNeeded = halfSweepBytes / 128u;
        _wgVramBus = (threadsNeeded + 63u) / 64u;

        uint qfCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &qfCount, null);
        QueueFamilyProperties* pQf = stackalloc QueueFamilyProperties[(int)qfCount];
        Unsafe.InitBlock(pQf, 0, (uint)(sizeof(QueueFamilyProperties) * qfCount));
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &qfCount, pQf);

        int bestQueue = -1;
        for (uint i = 0; i < qfCount; i++)
        {
            var flags = pQf[i].QueueFlags;
            if (flags.HasFlag(QueueFlags.ComputeBit) && flags.HasFlag(QueueFlags.GraphicsBit))
            {
                bestQueue = (int)i;
                break;
            }
            if (flags.HasFlag(QueueFlags.ComputeBit) && bestQueue == -1)
                bestQueue = (int)i;
        }

        _computeQueueFamilyIndex = (uint)bestQueue;

        // 1. Create Logical Device with Ray Tracing & BDA Features Chained
        InitializeLogicalDevice();

        // 2. Allocate SSBO Buffer Memory
        nuint allocBufferSize = (nuint)_vramSweepSizeBytes;
        BufferCreateInfo bufInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            PNext = null,
            Size = allocBufferSize,
            Usage = BufferUsageFlags.StorageBufferBit,
            SharingMode = SharingMode.Exclusive
        };

        _vk.CreateBuffer(_device, &bufInfo, null, out _ssboBuffer);

        MemoryRequirements memReqs;
        _vk.GetBufferMemoryRequirements(_device, _ssboBuffer, &memReqs);

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

        // 3. Create Command Pool & Fences early so RT Manager can execute build commands
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

        // 4. Initialize Hardware Acceleration Structure Manager (before creating Descriptor Set Layout)
        bool hasAs = false;
        if (_capabilityProbe.HasAccelerationStructure && _capabilityProbe.IsExtensionSupported("VK_KHR_acceleration_structure"))
        {
            _rtManager = new VulkanRtManager(_vk, _device, _physicalDevice, _computeQueue, _cmdPool, memProps);
            hasAs = _rtManager.Initialize();
        }

        // 5. Create Descriptor Set Layout (3 SSBO + 4 TMU + 1 TLAS if supported and built)
        uint totalBindings = hasAs ? 8u : 7u;

        DescriptorSetLayoutBinding* bindings = stackalloc DescriptorSetLayoutBinding[(int)totalBindings];
        for (uint b = 0; b < 3; b++)
        {
            bindings[b] = new()
            {
                Binding = b,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };
        }
        for (uint b = 3; b < 7; b++)
        {
            bindings[b] = new()
            {
                Binding = b,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };
        }

        if (hasAs)
        {
            bindings[7] = new()
            {
                Binding = 7,
                DescriptorType = (DescriptorType)1000150000, // VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };
        }

        DescriptorSetLayoutCreateInfo dslInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            PNext = null,
            BindingCount = totalBindings,
            PBindings = bindings
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

        // 6. Create Descriptor Pool
        uint poolCount = hasAs ? 3u : 2u;
        DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[(int)poolCount];
        poolSizes[0] = new() { Type = DescriptorType.StorageBuffer, DescriptorCount = 3 };
        poolSizes[1] = new() { Type = DescriptorType.CombinedImageSampler, DescriptorCount = 4 };
        if (hasAs)
        {
            poolSizes[2] = new() { Type = (DescriptorType)1000150000, DescriptorCount = 1 };
        }

        DescriptorPoolCreateInfo dpInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PNext = null,
            MaxSets = 1,
            PoolSizeCount = poolCount,
            PPoolSizes = poolSizes
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

        // 7. Write SSBO Descriptors (Bindings 0..2)
        nuint halfRange = allocBufferSize / 2;
        DescriptorBufferInfo* descBufInfos = stackalloc DescriptorBufferInfo[3];
        descBufInfos[0] = new() { Buffer = _ssboBuffer, Offset = 0, Range = allocBufferSize };
        descBufInfos[1] = new() { Buffer = _ssboBuffer, Offset = halfRange, Range = halfRange };
        descBufInfos[2] = new() { Buffer = _ssboBuffer, Offset = 0, Range = allocBufferSize };

        WriteDescriptorSet* writeDs = stackalloc WriteDescriptorSet[3];
        for (uint b = 0; b < 3; b++)
        {
            writeDs[b] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                PNext = null,
                DstSet = _descSet,
                DstBinding = b,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                PBufferInfo = &descBufInfos[b]
            };
        }

        _vk.UpdateDescriptorSets(_device, 3, writeDs, 0, null);

        // 8. Initialize Textures (Bindings 3..6)
        InitializeTmuResources(memProps);

        // 9. Write Acceleration Structure Descriptor (Binding 7)
        if (hasAs && _rtManager != null && _rtManager.IsInitialized)
        {
            var tlasHandle = _rtManager.Tlas;
            VkWriteDescriptorSetAccelerationStructureKHR asWrite = new()
            {
                sType = VulkanConstants.StructureTypeWriteDescriptorSetAccelerationStructureKHR,
                pNext = null,
                accelerationStructureCount = 1,
                pAccelerationStructures = &tlasHandle
            };

            WriteDescriptorSet write = new()
            {
                SType = StructureType.WriteDescriptorSet,
                PNext = &asWrite,
                DstSet = _descSet,
                DstBinding = 7,
                DescriptorType = (DescriptorType)1000150000,
                DescriptorCount = 1
            };

            _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
        }

        // 10. Register Compute & Ray Tracing Pipelines
        InitializePipelines();
        InitializeBufferOnGpu();
    }

    /// <summary>
    /// Configures device extensions and physical device features including Hardware Ray Tracing, then creates the VkDevice handle.
    /// </summary>
    private void InitializeLogicalDevice()
    {
        List<string> devExtensions = new();
        void* pNextChain = null;

        // Subgroup Size Control
        VkPhysicalDeviceSubgroupSizeControlFeaturesEXT subCtrlFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_EXT_subgroup_size_control"))
        {
            subCtrlFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceSubgroupSizeControlFeaturesEXT;
            subCtrlFeatures.subgroupSizeControl = 1u;
            subCtrlFeatures.computeFullSubgroups = 1u;
            subCtrlFeatures.pNext = pNextChain;
            pNextChain = &subCtrlFeatures;
            devExtensions.Add("VK_EXT_subgroup_size_control");
        }

        // Buffer Device Address (Required by KHR RT / Acceleration Structure)
        VkPhysicalDeviceBufferDeviceAddressFeaturesKHR bdaFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_buffer_device_address"))
        {
            bdaFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceBufferDeviceAddressFeaturesKHR;
            bdaFeatures.bufferDeviceAddress = 1u;
            bdaFeatures.pNext = pNextChain;
            pNextChain = &bdaFeatures;
            devExtensions.Add("VK_KHR_buffer_device_address");
        }

        // Acceleration Structure
        VkPhysicalDeviceAccelerationStructureFeaturesKHR asFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_acceleration_structure") && _capabilityProbe.HasAccelerationStructure)
        {
            asFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceAccelerationStructureFeaturesKHR;
            asFeatures.accelerationStructure = 1u;
            asFeatures.pNext = pNextChain;
            pNextChain = &asFeatures;
            devExtensions.Add("VK_KHR_acceleration_structure");

            if (_capabilityProbe.IsExtensionSupported("VK_KHR_deferred_host_operations"))
                devExtensions.Add("VK_KHR_deferred_host_operations");
        }

        // Ray Query (Inline HW RT)
        VkPhysicalDeviceRayQueryFeaturesKHR rqFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_ray_query") && _capabilityProbe.HasRayQuery)
        {
            rqFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceRayQueryFeaturesKHR;
            rqFeatures.rayQuery = 1u;
            rqFeatures.pNext = pNextChain;
            pNextChain = &rqFeatures;
            devExtensions.Add("VK_KHR_ray_query");
        }

        // Ray Tracing Pipeline
        VkPhysicalDeviceRayTracingPipelineFeaturesKHR rtpFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_ray_tracing_pipeline") && _capabilityProbe.HasRTPipeline)
        {
            rtpFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceRayTracingPipelineFeaturesKHR;
            rtpFeatures.rayTracingPipeline = 1u;
            rtpFeatures.pNext = pNextChain;
            pNextChain = &rtpFeatures;
            devExtensions.Add("VK_KHR_ray_tracing_pipeline");
        }

        // Shader Execution Reordering (SER)
        VkPhysicalDeviceRayTracingInvocationReorderFeaturesNV serFeatures = default;
        if (_capabilityProbe.HasSER)
        {
            serFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceRayTracingInvocationReorderFeaturesNV;
            serFeatures.rayTracingInvocationReorder = 1u;
            serFeatures.pNext = pNextChain;
            pNextChain = &serFeatures;

            if (_capabilityProbe.IsExtensionSupported("VK_KHR_ray_tracing_invocation_reorder"))
                devExtensions.Add("VK_KHR_ray_tracing_invocation_reorder");
            else if (_capabilityProbe.IsExtensionSupported("VK_NV_ray_tracing_invocation_reorder"))
                devExtensions.Add("VK_NV_ray_tracing_invocation_reorder");
        }

        // Opacity Micromap (OMM)
        VkPhysicalDeviceOpacityMicromapFeaturesEXT ommFeatures = default;
        if (_capabilityProbe.HasOMM)
        {
            ommFeatures.sType = VulkanConstants.StructureTypeOpacityMicromapFeaturesEXT;
            ommFeatures.micromap = 1u;
            ommFeatures.pNext = pNextChain;
            pNextChain = &ommFeatures;

            if (_capabilityProbe.IsExtensionSupported("VK_EXT_opacity_micromap"))
                devExtensions.Add("VK_EXT_opacity_micromap");
            else if (_capabilityProbe.IsExtensionSupported("VK_KHR_opacity_micromap"))
                devExtensions.Add("VK_KHR_opacity_micromap");
        }

        // Ray Tracing Position Fetch
        VkPhysicalDeviceRayTracingPositionFetchFeaturesKHR posFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_ray_tracing_position_fetch") && _capabilityProbe.HasRTPositionFetch)
        {
            posFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceRayTracingPositionFetchFeaturesKHR;
            posFeatures.rayTracingPositionFetch = 1u;
            posFeatures.pNext = pNextChain;
            pNextChain = &posFeatures;
            devExtensions.Add("VK_KHR_ray_tracing_position_fetch");
        }

        // Legacy NVIDIA Ray Tracing (2018)
        if (_capabilityProbe.IsExtensionSupported("VK_NV_ray_tracing") && _capabilityProbe.HasNVRayTracing)
        {
            devExtensions.Add("VK_NV_ray_tracing");
        }

        // SPIR-V 1.4 Optional Driver Activation
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_spirv_1_4"))
        {
            devExtensions.Add("VK_KHR_spirv_1_4");
            if (_capabilityProbe.IsExtensionSupported("VK_KHR_shader_float_controls"))
                devExtensions.Add("VK_KHR_shader_float_controls");
        }

        // 16-Bit Storage
        VkPhysicalDevice16BitStorageFeaturesKHR storage16 = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_16bit_storage"))
        {
            storage16.sType = VulkanConstants.StructureTypePhysicalDevice16BitStorageFeaturesKHR;
            storage16.storageBuffer16BitAccess = 1u;
            storage16.uniformAndStorageBuffer16BitAccess = 1u;
            storage16.pNext = pNextChain;
            pNextChain = &storage16;
            devExtensions.Add("VK_KHR_16bit_storage");
        }

        // 8-Bit Storage
        VkPhysicalDevice8BitStorageFeaturesKHR storage8 = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_8bit_storage"))
        {
            storage8.sType = VulkanConstants.StructureTypePhysicalDevice8BitStorageFeaturesKHR;
            storage8.storageBuffer8BitAccess = 1u;
            storage8.uniformAndStorageBuffer8BitAccess = 1u;
            storage8.pNext = pNextChain;
            pNextChain = &storage8;
            devExtensions.Add("VK_KHR_8bit_storage");
        }

        if (_capabilityProbe.IsExtensionSupported("VK_KHR_storage_buffer_storage_class"))
        {
            devExtensions.Add("VK_KHR_storage_buffer_storage_class");
        }

        VkPhysicalDeviceShaderFloat16Int8FeaturesKHR float16Features = default;
        if (_capabilityProbe.HasFloat16 || _capabilityProbe.HasInt8)
        {
            float16Features.sType = VulkanConstants.StructureTypePhysicalDeviceShaderFloat16Int8FeaturesKHR;
            float16Features.shaderFloat16 = _capabilityProbe.HasFloat16 ? 1u : 0u;
            float16Features.shaderInt8 = _capabilityProbe.HasInt8 ? 1u : 0u;
            float16Features.pNext = pNextChain;
            pNextChain = &float16Features;
            devExtensions.Add("VK_KHR_shader_float16_int8");
        }

        VkPhysicalDeviceShaderIntegerDotProductFeaturesKHR dotFeatures = default;
        if (_capabilityProbe.HasIntegerDotProduct)
        {
            dotFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceShaderIntegerDotProductFeaturesKHR;
            dotFeatures.shaderIntegerDotProduct = 1u;
            dotFeatures.pNext = pNextChain;
            pNextChain = &dotFeatures;
            devExtensions.Add("VK_KHR_shader_integer_dot_product");
        }

        // KHR Cooperative Matrix
        VkPhysicalDeviceCooperativeMatrixFeaturesKHR coopFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_cooperative_matrix") && _capabilityProbe.HasCoopMatrixKHR)
        {
            coopFeatures.sType = VulkanConstants.StructureTypePhysicalDeviceCooperativeMatrixFeaturesKHR;
            coopFeatures.cooperativeMatrix = 1u;
            coopFeatures.pNext = pNextChain;
            pNextChain = &coopFeatures;
            devExtensions.Add("VK_KHR_cooperative_matrix");
        }

        // NVIDIA Cooperative Matrix 1
        VkPhysicalDeviceCooperativeMatrixFeaturesNV coopNvFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_NV_cooperative_matrix"))
        {
            coopNvFeatures.sType = (StructureType)1000249000;
            coopNvFeatures.cooperativeMatrix = 1u;
            coopNvFeatures.pNext = pNextChain;
            pNextChain = &coopNvFeatures;
            devExtensions.Add("VK_NV_cooperative_matrix");
        }

        // NVIDIA Cooperative Matrix 2
        VkPhysicalDeviceCooperativeMatrix2FeaturesNV coopNv2Features = default;
        if (_capabilityProbe.IsExtensionSupported("VK_NV_cooperative_matrix2"))
        {
            coopNv2Features.sType = (StructureType)1000491000;
            coopNv2Features.cooperativeMatrixWorkgroupScope = 1u;
            coopNv2Features.pNext = pNextChain;
            pNextChain = &coopNv2Features;
            devExtensions.Add("VK_NV_cooperative_matrix2");
        }

        // Vulkan Memory Model
        VkPhysicalDeviceVulkanMemoryModelFeaturesKHR memModelFeatures = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_vulkan_memory_model"))
        {
            memModelFeatures.sType = (StructureType)1000211000;
            memModelFeatures.vulkanMemoryModel = 1u;
            memModelFeatures.vulkanMemoryModelDeviceScope = 1u;
            memModelFeatures.pNext = pNextChain;
            pNextChain = &memModelFeatures;
            devExtensions.Add("VK_KHR_vulkan_memory_model");
        }

        // BFloat16 Hardware Arithmetic
        VkPhysicalDeviceShaderBfloat16FeaturesKHR bf16Features = default;
        if (_capabilityProbe.IsExtensionSupported("VK_KHR_shader_bfloat16"))
        {
            bf16Features.sType = (StructureType)1000570000;
            bf16Features.shaderBFloat16Type = 1u;
            bf16Features.shaderBFloat16DotProduct = 1u;
            bf16Features.shaderBFloat16CooperativeMatrix = 1u;
            bf16Features.pNext = pNextChain;
            pNextChain = &bf16Features;
            devExtensions.Add("VK_KHR_shader_bfloat16");
        }

        // Float8 Hardware Types
        VkPhysicalDeviceShaderFloat8FeaturesEXT fp8Features = default;
        if (_capabilityProbe.IsExtensionSupported("VK_EXT_shader_float8"))
        {
            fp8Features.sType = (StructureType)1000588000;
            fp8Features.shaderFloat8 = 1u;
            fp8Features.pNext = pNextChain;
            fp8Features.shaderFloat8CooperativeMatrix = 1u;
            pNextChain = &fp8Features;
            devExtensions.Add("VK_EXT_shader_float8");
        }

        VkPhysicalDeviceFeatures2KHR feat2 = default;
        feat2.sType = VulkanConstants.StructureTypePhysicalDeviceFeatures2KHR;
        feat2.features.ShaderFloat64 = _capabilityProbe.HasFloat64;
        feat2.features.ShaderInt64 = _capabilityProbe.HasInt64;
        feat2.features.ShaderInt16 = _capabilityProbe.HasInt16;
        feat2.features.SamplerAnisotropy = _capabilityProbe.HasSamplerAnisotropy;
        feat2.features.TextureCompressionBC = _capabilityProbe.HasTextureCompressionBC;
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

    private void InitializeTmuResources(PhysicalDeviceMemoryProperties memProps)
    {
        uint FindMemoryType(uint bits, MemoryPropertyFlags preferredFlags)
        {
            for (uint i = 0; i < memProps.MemoryTypeCount; i++)
            {
                if ((bits & (1u << (int)i)) != 0 && (memProps.MemoryTypes[(int)i].PropertyFlags & preferredFlags) == preferredFlags)
                    return i;
            }
            for (uint i = 0; i < memProps.MemoryTypeCount; i++)
            {
                if ((bits & (1u << (int)i)) != 0)
                    return i;
            }
            throw new InvalidOperationException("Failed to find a compatible Vulkan memory type.");
        }

        SamplerCreateInfo sampInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MaxLod = 11.0f
        };
        _vk.CreateSampler(_device, &sampInfo, null, out _samplerLinear);

        sampInfo.AnisotropyEnable = _capabilityProbe.HasSamplerAnisotropy;
        sampInfo.MaxAnisotropy = _capabilityProbe.HasSamplerAnisotropy ? 16.0f : 1.0f;
        _vk.CreateSampler(_device, &sampInfo, null, out _samplerAniso);

        sampInfo.AnisotropyEnable = false;
        _vk.CreateSampler(_device, &sampInfo, null, out _sampler3D);

        ImageCreateInfo img2DInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D(1024, 1024, 1),
            MipLevels = 11,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit
        };
        _vk.CreateImage(_device, &img2DInfo, null, out _tmuImage2D);
        MemoryRequirements reqs;
        _vk.GetImageMemoryRequirements(_device, _tmuImage2D, &reqs);
        MemoryAllocateInfo alloc = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = reqs.Size, MemoryTypeIndex = FindMemoryType(reqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit) };
        _vk.AllocateMemory(_device, &alloc, null, out _tmuMem2D);
        _vk.BindImageMemory(_device, _tmuImage2D, _tmuMem2D, 0);

        ImageViewCreateInfo view2DInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _tmuImage2D,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 11, 0, 1)
        };
        _vk.CreateImageView(_device, &view2DInfo, null, out _tmuView2D);

        Format bcFormat = _capabilityProbe.HasTextureCompressionBC ? (Format)145 : Format.R8G8B8A8Unorm;
        ImageCreateInfo imgBCInfo = img2DInfo;
        imgBCInfo.Format = bcFormat;
        imgBCInfo.MipLevels = 1;
        _vk.CreateImage(_device, &imgBCInfo, null, out _tmuImageBC);
        _vk.GetImageMemoryRequirements(_device, _tmuImageBC, &reqs);
        alloc.AllocationSize = reqs.Size;
        alloc.MemoryTypeIndex = FindMemoryType(reqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);
        _vk.AllocateMemory(_device, &alloc, null, out _tmuMemBC);
        _vk.BindImageMemory(_device, _tmuImageBC, _tmuMemBC, 0);

        ImageViewCreateInfo viewBCInfo = view2DInfo;
        viewBCInfo.Image = _tmuImageBC;
        viewBCInfo.Format = bcFormat;
        viewBCInfo.SubresourceRange.LevelCount = 1;
        _vk.CreateImageView(_device, &viewBCInfo, null, out _tmuViewBC);

        ImageCreateInfo img3DInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type3D,
            Format = Format.R8G8B8A8Unorm,
            Extent = new Extent3D(128, 128, 128),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit
        };
        _vk.CreateImage(_device, &img3DInfo, null, out _tmuImage3D);
        _vk.GetImageMemoryRequirements(_device, _tmuImage3D, &reqs);
        alloc.AllocationSize = reqs.Size;
        alloc.MemoryTypeIndex = FindMemoryType(reqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);
        _vk.AllocateMemory(_device, &alloc, null, out _tmuMem3D);
        _vk.BindImageMemory(_device, _tmuImage3D, _tmuMem3D, 0);

        ImageViewCreateInfo view3DInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _tmuImage3D,
            ViewType = ImageViewType.Type3D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CreateImageView(_device, &view3DInfo, null, out _tmuView3D);

        CommandBufferAllocateInfo cbAlloc = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _cmdPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        CommandBuffer cb;
        _vk.AllocateCommandBuffers(_device, &cbAlloc, &cb);
        CommandBufferBeginInfo cbBegin = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _vk.BeginCommandBuffer(cb, &cbBegin);

        ImageMemoryBarrier* barriers = stackalloc ImageMemoryBarrier[3];
        barriers[0] = new() { SType = StructureType.ImageMemoryBarrier, Image = _tmuImage2D, OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.ShaderReadOnlyOptimal, SrcAccessMask = 0, DstAccessMask = AccessFlags.ShaderReadBit, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 11, 0, 1) };
        barriers[1] = new() { SType = StructureType.ImageMemoryBarrier, Image = _tmuImageBC, OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.ShaderReadOnlyOptimal, SrcAccessMask = 0, DstAccessMask = AccessFlags.ShaderReadBit, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1) };
        barriers[2] = new() { SType = StructureType.ImageMemoryBarrier, Image = _tmuImage3D, OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.ShaderReadOnlyOptimal, SrcAccessMask = 0, DstAccessMask = AccessFlags.ShaderReadBit, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1) };
        _vk.CmdPipelineBarrier(cb, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.ComputeShaderBit, 0, 0, null, 0, null, 3, barriers);

        _vk.EndCommandBuffer(cb);
        SubmitInfo submit = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cb };
        _vk.QueueSubmit(_computeQueue, 1, &submit, default);
        _vk.QueueWaitIdle(_computeQueue);
        _vk.FreeCommandBuffers(_device, _cmdPool, 1, &cb);

        DescriptorImageInfo* imgInfos = stackalloc DescriptorImageInfo[4];
        imgInfos[0] = new() { Sampler = _samplerLinear, ImageView = _tmuView2D, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };
        imgInfos[1] = new() { Sampler = _samplerAniso,  ImageView = _tmuView2D, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };
        imgInfos[2] = new() { Sampler = _samplerLinear, ImageView = _tmuViewBC, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };
        imgInfos[3] = new() { Sampler = _sampler3D,      ImageView = _tmuView3D, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };

        WriteDescriptorSet* writes = stackalloc WriteDescriptorSet[4];
        for (uint i = 0; i < 4; i++)
        {
            writes[i] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _descSet,
                DstBinding = 3 + i,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &imgInfos[i]
            };
        }
        _vk.UpdateDescriptorSets(_device, 4, writes, 0, null);
    }

    private void InitializeBufferOnGpu()
    {
        uint[] initSpv = VulkanShaders.GetShaderInitBuffer();
        var initPipe = CreateComputePipeline(initSpv, isCoopMat: false, out var initModule);
        if (initPipe.Handle == 0) return;

        CommandBufferAllocateInfo cbAlloc = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _cmdPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        CommandBuffer cb;
        _vk.AllocateCommandBuffers(_device, &cbAlloc, &cb);

        CommandBufferBeginInfo cbBegin = new() { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _vk.BeginCommandBuffer(cb, &cbBegin);
        _vk.CmdBindPipeline(cb, PipelineBindPoint.Compute, initPipe);
        var ds = _descSet;
        _vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Compute, _pipeLayout, 0, 1, &ds, 0, null);
        _vk.CmdDispatch(cb, (uint)(_vramSweepSizeBytes / (sizeof(uint) * 64)), 1, 1);
        _vk.EndCommandBuffer(cb);

        SubmitInfo submit = new() { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cb };
        _vk.QueueSubmit(_computeQueue, 1, &submit, default);
        _vk.QueueWaitIdle(_computeQueue);

        _vk.FreeCommandBuffers(_device, _cmdPool, 1, &cb);
        _vk.DestroyPipeline(_device, initPipe, null);
        _vk.DestroyShaderModule(_device, initModule, null);
    }

    private Pipeline CreateComputePipeline(uint[] spirvCode, bool isCoopMat, out ShaderModule shaderModule)
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

        bool hasSubgroupControl = _capabilityProbe.IsExtensionSupported("VK_EXT_subgroup_size_control");
        PipelineShaderStageCreateFlags flags = (isCoopMat && hasSubgroupControl) ? (PipelineShaderStageCreateFlags)8 : 0;

        PipelineShaderStageCreateInfo stageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            PNext = null,
            Stage = ShaderStageFlags.ComputeBit,
            Module = shaderModule,
            PName = pMain,
            Flags = flags
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

    private void SafeRegisterPipeline(string id, uint[]? spirv, bool isCoopMat = false)
    {
        if (spirv == null) return;
        try
        {
            var p = CreateComputePipeline(spirv, isCoopMat, out var sm);
            if (p.Handle != 0)
            {
                _pipelines[id] = (p, sm);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Pipeline Exception] Failed to initialize '{id}': {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Compiles and registers all compute pipeline variants across ALU, Ray Tracing, Matrix, and Memory domains.
    /// </summary>
    private void InitializePipelines()
    {
        // 1. ALU Compute Pipelines (SPIR-V 1.0)
        SafeRegisterPipeline("alu_fp32", VulkanShaders.GetShaderFP32());
        SafeRegisterPipeline("alu_fp16", VulkanShaders.GetShaderFP16(_capabilityProbe));
        SafeRegisterPipeline("alu_bf16", VulkanShaders.GetShaderBF16(_capabilityProbe));
        SafeRegisterPipeline("alu_fp64", VulkanShaders.GetShaderFP64(_capabilityProbe));

        SafeRegisterPipeline("alu_int32", VulkanShaders.GetShaderINT32());
        SafeRegisterPipeline("alu_int16", VulkanShaders.GetShaderINT16(_capabilityProbe));
        SafeRegisterPipeline("alu_int64", VulkanShaders.GetShaderINT64(_capabilityProbe));
        SafeRegisterPipeline("alu_int8",  VulkanShaders.GetShaderINT8(_capabilityProbe));

        if (_capabilityProbe.HasIntegerDotProduct)
        {
            SafeRegisterPipeline("alu_dp4a", VulkanShaders.GetShaderDP4A(_capabilityProbe));
            if (_capabilityProbe.HasDotProduct16Bit)
                SafeRegisterPipeline("alu_dp2a", VulkanShaders.GetShaderDP2A(_capabilityProbe));
        }

        // 2. Hardware Ray Tracing Pipelines (SPIR-V 1.4 / Extension-Accelerated)
        if (_capabilityProbe.HasRayQuery)
        {
            SafeRegisterPipeline("rt_query",     VulkanShaders.GetShaderRtQuery(_capabilityProbe));
            SafeRegisterPipeline("rt_query_omm", VulkanShaders.GetShaderRtQueryOmm(_capabilityProbe));
            SafeRegisterPipeline("rt_query_pos", VulkanShaders.GetShaderRtQueryPos(_capabilityProbe));
        }

        if (_capabilityProbe.HasRTPipeline)
        {
            SafeRegisterPipeline("rt_pipe",     VulkanShaders.GetShaderRtPipe(_capabilityProbe));
            SafeRegisterPipeline("rt_pipe_ser", VulkanShaders.GetShaderRtPipeSer(_capabilityProbe));
            SafeRegisterPipeline("rt_pipe_omm", VulkanShaders.GetShaderRtPipeOmm(_capabilityProbe));
            SafeRegisterPipeline("rt_pipe_pos", VulkanShaders.GetShaderRtPipePos(_capabilityProbe));
        }

        if (_capabilityProbe.HasNVRayTracing)
        {
            SafeRegisterPipeline("rt_nv", VulkanShaders.GetShaderRtNv(_capabilityProbe));
        }

        // 3. Standard KHR Cooperative Matrix Pipelines
        if (_capabilityProbe.HasCoopMatrixKHR)
        {
            if (_capabilityProbe.HasMatFP16)
                SafeRegisterPipeline("mat_fp16_khr", VulkanShaders.GetShaderMatFP16(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatBF16)
                SafeRegisterPipeline("mat_bf16_khr", VulkanShaders.GetShaderMatBF16(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatFP32)
                SafeRegisterPipeline("mat_fp32_khr", VulkanShaders.GetShaderMatFP32(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatFP64)
                SafeRegisterPipeline("mat_fp64_khr", VulkanShaders.GetShaderMatFP64(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatFP8_E4M3)
                SafeRegisterPipeline("mat_fp8_e4m3_khr", VulkanShaders.GetShaderMatFP8_E4M3(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatFP8_E5M2)
                SafeRegisterPipeline("mat_fp8_e5m2_khr", VulkanShaders.GetShaderMatFP8_E5M2(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatINT8)
                SafeRegisterPipeline("mat_int8_khr", VulkanShaders.GetShaderMatINT8(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasIntelInt4)
                SafeRegisterPipeline("mat_int4_khr", VulkanShaders.GetShaderMatINT4(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatINT16)
                SafeRegisterPipeline("mat_int16_khr", VulkanShaders.GetShaderMatINT16(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatINT32)
                SafeRegisterPipeline("mat_int32_khr", VulkanShaders.GetShaderMatINT32(_capabilityProbe), isCoopMat: true);
            if (_capabilityProbe.HasMatINT64)
                SafeRegisterPipeline("mat_int64_khr", VulkanShaders.GetShaderMatINT64(_capabilityProbe), isCoopMat: true);
        }

        // 4. NVIDIA Cooperative Matrix 1 Pipelines
        if (_capabilityProbe.HasCoopMatrixNV)
        {
            SafeRegisterPipeline("mat_fp16_nv1", VulkanNvMatrixShaders.GetShaderNV1_FP16(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_fp32_nv1", VulkanNvMatrixShaders.GetShaderNV1_FP32(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_fp64_nv1", VulkanNvMatrixShaders.GetShaderNV1_FP64(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int8_nv1", VulkanNvMatrixShaders.GetShaderNV1_INT8(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int16_nv1", VulkanNvMatrixShaders.GetShaderNV1_INT16(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int32_nv1", VulkanNvMatrixShaders.GetShaderNV1_INT32(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int64_nv1", VulkanNvMatrixShaders.GetShaderNV1_INT64(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_mod_nv1", VulkanNvMatrixShaders.GetShaderNV1_FP16(_capabilityProbe), isCoopMat: true);
        }

        // 5. NVIDIA Cooperative Matrix 2 Pipelines
        if (_capabilityProbe.HasCoopMatrixNV2)
        {
            SafeRegisterPipeline("mat_fp16_nv2", VulkanNvMatrixShaders.GetShaderNV2_FP16(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_bf16_nv2", VulkanNvMatrixShaders.GetShaderNV2_BF16(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_fp8_e4m3_nv2", VulkanNvMatrixShaders.GetShaderNV2_FP8_E4M3(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_fp8_e5m2_nv2", VulkanNvMatrixShaders.GetShaderNV2_FP8_E5M2(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_fp32_nv2", VulkanNvMatrixShaders.GetShaderNV2_FP32(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_fp64_nv2", VulkanNvMatrixShaders.GetShaderNV2_FP64(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int8_nv2", VulkanNvMatrixShaders.GetShaderNV2_INT8(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int16_nv2", VulkanNvMatrixShaders.GetShaderNV2_INT16(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int32_nv2", VulkanNvMatrixShaders.GetShaderNV2_INT32(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_int64_nv2", VulkanNvMatrixShaders.GetShaderNV2_INT64(_capabilityProbe), isCoopMat: true);
            SafeRegisterPipeline("mat_mod_nv2", VulkanNvMatrixShaders.GetShaderNV2_FP16(_capabilityProbe), isCoopMat: true);
        }

        // 6. Cache Hierarchy & Memory Pipelines
        SafeRegisterPipeline("mem_l1l2", VulkanShaders.GetShaderL2Cache());
        SafeRegisterPipeline("mem_l3",   VulkanShaders.GetShaderL3Cache());
        SafeRegisterPipeline("mem_bw",   VulkanShaders.GetShaderMem());
        SafeRegisterPipeline("mem_lat",  VulkanShaders.GetShaderLatency());

        // 7. TMU Texture Stress Pipelines
        SafeRegisterPipeline("tmu_bdt",   VulkanShaders.GetShaderTmuBilinear());
        if (_capabilityProbe.HasSamplerAnisotropy)
            SafeRegisterPipeline("tmu_aniso", VulkanShaders.GetShaderTmuAniso());
        SafeRegisterPipeline("tmu_bc",    VulkanShaders.GetShaderTmuBC());
        SafeRegisterPipeline("tmu_3d",    VulkanShaders.GetShaderTmu3D());
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
                if (item.Id == "mem_l1l2" && _capabilityProbe.L2CacheSizeBytes == 0) continue;
                if (item.Id is "rt_ser" or "rt_omm" or "rt_pos" or "mat_mod_nv1" or "mat_mod_nv2") continue;
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
            Pipeline pipe = ResolvePipeline(testId);
            if (pipe.Handle == 0) continue;

            uint wgCount = testId switch
            {
                "mem_l1l2" => _wgAluAndL1,
                "mem_l3"   => _wgL3Cache,
                "mem_bw"   => _wgVramBus,
                "mem_lat"  => 1,
                "mat_fp16" or "mat_bf16" or "mat_int8" or "mat_fp32" or "mat_fp64"
                    or "mat_fp8_e4m3" or "mat_fp8_e5m2" or "mat_int4"
                    or "mat_int16" or "mat_int32" or "mat_int64"
                    or "mat_mod_nv1" or "mat_mod_nv2" => _wgMatrix,
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

        bool isMatrix = id.StartsWith("mat_");
        uint wgCount = isMatrix ? _wgMatrix : _wgAluAndL1;

        double opsPerInvocation = isMatrix
            ? _capabilityProbe.GetMatrixOpsPerInvocation(id)
            : workload.OpsPerInvocation;

        double totalInvocationsPerDispatch = wgCount * VulkanShaders.LocalSizeX;
        double operationsPerDispatch = opsPerInvocation * totalInvocationsPerDispatch;
        return (dispatchesPerSecond * operationsPerDispatch) / 1e12;
    }

    private double GetWorkloadMRays(string id, double dispatchesPerSecond)
    {
        if (!_workloads.TryGetValue(id, out var workload)) return 0.0;
        double totalInvocations = _wgAluAndL1 * VulkanShaders.LocalSizeX;
        double raysPerDispatch = workload.OpsPerInvocation * totalInvocations;
        return (dispatchesPerSecond * raysPerDispatch) / 1e6;
    }

    private void Execute(CancellationToken hostCt)
    {
        _computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);

        _computeThread = new Thread(() =>
        {
            int testIdx = 0;
            int burstCounter = 0;
            const int BurstsPerTest = 4;

            string[] frameTestId = new string[InFlightFrames];
            long[] frameSubmitTimestamp = new long[InFlightFrames];
            int frameIndex = 0;
            int warmup = 0;

            while (!_computeCts.Token.IsCancellationRequested)
            {
                var currentTests = _activeTestSnapshot;
                if (!_isBenchmarking || currentTests.Length == 0)
                {
                    Thread.Sleep(10);
                    Array.Clear(frameTestId);
                    burstCounter = 0;
                    warmup = 0;
                    continue;
                }

                lock (_queueLock)
                {
                    var fence = _inFlightFences[frameIndex];
                    _vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue);
                    _vk.ResetFences(_device, 1, &fence);

                    long now = Stopwatch.GetTimestamp();
                    string completedTest = frameTestId[frameIndex];
                    long submitTime = frameSubmitTimestamp[frameIndex];

                    if (submitTime > 0 && !string.IsNullOrEmpty(completedTest))
                    {
                        double deltaSec = (double)(now - submitTime) / Stopwatch.Frequency;
                        warmup++;

                        if (warmup > InFlightFrames * 2 && deltaSec > 0.0001)
                        {
                            int batch = GetBatchCount(completedTest);
                            double instDps = batch / deltaSec;

                            lock (_liveTestDps)
                            {
                                if (_liveTestDps.TryGetValue(completedTest, out double prev) && prev > 0)
                                    _liveTestDps[completedTest] = prev * 0.85 + instDps * 0.15;
                                else
                                    _liveTestDps[completedTest] = instDps;
                            }

                            _smoothedDps = (_smoothedDps == 0.0) ? instDps : (_smoothedDps * 0.9 + instDps * 0.1);
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
                            frameSubmitTimestamp[frameIndex] = Stopwatch.GetTimestamp();

                            Result submitResult = _vk.QueueSubmit(_computeQueue, 1, &stressSubmit, fence);
                            if (submitResult == Result.Success)
                            {
                                Interlocked.Add(ref _totalCompletedDispatches, (ulong)GetBatchCount(nextTest));
                            }
                            else
                            {
                                Console.WriteLine($"[Vulkan QueueSubmit Error]: {submitResult}");
                                break;
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
        winOptions.Title = "GPU-T Vulkan Compute & Hardware RT Benchmark";
        winOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        winOptions.VSync = true;

        using var window = Window.Create(winOptions);
        GL? gl = null;

        uint uiProgram = 0, vao = 0, vbo = 0, uiTexture = 0;
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

            double GetDps(string id)
            {
                if (!_isBenchmarking) return 0.0;
                lock (_liveTestDps)
                {
                    return _liveTestDps.TryGetValue(id, out double d) ? d : 0.0;
                }
            }

            // ALU Metrics
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

            // Matrix Metrics
            double matFp16Tops = _testRegistry.IsActive("mat_fp16") ? GetWorkloadTops("mat_fp16", GetDps("mat_fp16")) : 0.0;
            double matBf16Tops = _testRegistry.IsActive("mat_bf16") ? GetWorkloadTops("mat_bf16", GetDps("mat_bf16")) : 0.0;
            double matInt8Tops = _testRegistry.IsActive("mat_int8") ? GetWorkloadTops("mat_int8", GetDps("mat_int8")) : 0.0;

            // Hardware Ray Tracing Metrics (MRays/s)
            double rtMRaysSec = 0.0;
            if (_testRegistry.IsActive("rt_query")) rtMRaysSec = Math.Max(rtMRaysSec, GetWorkloadMRays("rt_query", GetDps("rt_query")));
            if (_testRegistry.IsActive("rt_pipe"))  rtMRaysSec = Math.Max(rtMRaysSec, GetWorkloadMRays("rt_pipe", GetDps("rt_pipe")));
            if (_testRegistry.IsActive("rt_nv"))    rtMRaysSec = Math.Max(rtMRaysSec, GetWorkloadMRays("rt_nv", GetDps("rt_nv")));

            // Memory Hierarchy Metrics
            const double l2BytesPerGroup = 64.0 * 544.0;
            double cacheL1L2Tbs = (_testRegistry.IsActive("mem_l1l2") && HasPipelineFor("mem_l1l2") && _capabilityProbe.L2CacheSizeBytes > 0)
                ? (GetDps("mem_l1l2") * (_wgAluAndL1 * l2BytesPerGroup)) / 1_000_000_000_000.0
                : 0.0;

            const double l3BytesPerGroup = 64.0 * 288.0;
            double cacheL3Tbs = (_testRegistry.IsActive("mem_l3") && _capabilityProbe.HasL3InfinityCache && HasPipelineFor("mem_l3"))
                ? (GetDps("mem_l3") * (_wgL3Cache * l3BytesPerGroup)) / 1_000_000_000_000.0
                : 0.0;

            double memBandwidthGbs = (_testRegistry.IsActive("mem_bw") && HasPipelineFor("mem_bw"))
                ? (GetDps("mem_bw") * (double)_vramSweepSizeBytes) / 1_000_000_000.0
                : 0.0;

            double latDps = GetDps("mem_lat");
            double latencyNs = (_testRegistry.IsActive("mem_lat") && latDps > 0)
                ? (1_000_000_000.0 / (latDps * 4096.0))
                : 0.0;

            // TMU Texture Metrics
            double tmuGtexels = 0.0;
            if (_testRegistry.IsActive("tmu_bdt"))   tmuGtexels = Math.Max(tmuGtexels, (GetDps("tmu_bdt") * _wgAluAndL1 * 64.0 * VulkanShaders.TMU_SAMPLES_PER_INVOCATION) / 1e9);
            if (_testRegistry.IsActive("tmu_aniso")) tmuGtexels = Math.Max(tmuGtexels, (GetDps("tmu_aniso") * _wgAluAndL1 * 64.0 * VulkanShaders.TMU_SAMPLES_PER_INVOCATION * 16.0) / 1e9);
            if (_testRegistry.IsActive("tmu_bc"))    tmuGtexels = Math.Max(tmuGtexels, (GetDps("tmu_bc") * _wgAluAndL1 * 64.0 * VulkanShaders.TMU_SAMPLES_PER_INVOCATION) / 1e9);
            if (_testRegistry.IsActive("tmu_3d"))    tmuGtexels = Math.Max(tmuGtexels, (GetDps("tmu_3d") * _wgAluAndL1 * 64.0 * VulkanShaders.TMU_SAMPLES_PER_INVOCATION) / 1e9);

            double totalActiveDps = _isBenchmarking ? _smoothedDps : 0.0;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "Vulkan 1.0 Stress-test", _deviceName, _isBenchmarking, _targetDurationSec,
                    _customInputBuffer, _isCustomFocused, elapsed, totalActiveDps, 0.0, null, ThemePalette.Vulkan,
                    _mouseX, _mouseY, animTime, _isSettingsOpen, _testRegistry,
                    fp32Tflops, fp64Tflops, fp16Tflops, bf16Tflops, int32Tiops, int64Tiops, int16Tiops, int8Tiops,
                    dp2aTops, dp4aTops, tmuGtexels, memBandwidthGbs, cacheL1L2Tbs, cacheL3Tbs, latencyNs,
                    matFp16Tops, matBf16Tops, matInt8Tops, rtMRaysSec);

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
            _totalCompletedDispatches = 0;
            _smoothedDps = 0.0;
            _liveTestDps.Clear();
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
            _smoothedDps = 0.0;
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

            _rtManager?.Dispose();
            _rtManager = null;

            if (_pipeLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _pipeLayout, null);
            if (_descPool.Handle != 0) _vk.DestroyDescriptorPool(_device, _descPool, null);
            if (_descLayout.Handle != 0) _vk.DestroyDescriptorSetLayout(_device, _descLayout, null);
            if (_cmdPool.Handle != 0) _vk.DestroyCommandPool(_device, _cmdPool, null);
            if (_ssboBuffer.Handle != 0) _vk.DestroyBuffer(_device, _ssboBuffer, null);
            if (_ssboMemory.Handle != 0) _vk.FreeMemory(_device, _ssboMemory, null);

            if (_tmuView2D.Handle != 0) _vk.DestroyImageView(_device, _tmuView2D, null);
            if (_tmuImage2D.Handle != 0) _vk.DestroyImage(_device, _tmuImage2D, null);
            if (_tmuMem2D.Handle != 0) _vk.FreeMemory(_device, _tmuMem2D, null);

            if (_tmuViewBC.Handle != 0) _vk.DestroyImageView(_device, _tmuViewBC, null);
            if (_tmuImageBC.Handle != 0) _vk.DestroyImage(_device, _tmuImageBC, null);
            if (_tmuMemBC.Handle != 0) _vk.FreeMemory(_device, _tmuMemBC, null);

            if (_tmuView3D.Handle != 0) _vk.DestroyImageView(_device, _tmuView3D, null);
            if (_tmuImage3D.Handle != 0) _vk.DestroyImage(_device, _tmuImage3D, null);
            if (_tmuMem3D.Handle != 0) _vk.FreeMemory(_device, _tmuMem3D, null);

            if (_samplerLinear.Handle != 0) _vk.DestroySampler(_device, _samplerLinear, null);
            if (_samplerAniso.Handle != 0) _vk.DestroySampler(_device, _samplerAniso, null);
            if (_sampler3D.Handle != 0) _vk.DestroySampler(_device, _sampler3D, null);

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