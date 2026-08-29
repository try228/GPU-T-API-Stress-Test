using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
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
/// Native Vulkan debug utils messenger descriptor compliant with C-ABI and .NET NativeAOT.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct VkDebugUtilsMessengerCreateInfoEXT
{
    public StructureType sType;
    public void* pNext;
    public uint flags;
    public DebugUtilsMessageSeverityFlagsEXT messageSeverity;
    public DebugUtilsMessageTypeFlagsEXT messageType;
    public delegate* unmanaged[Cdecl]<DebugUtilsMessageSeverityFlagsEXT, DebugUtilsMessageTypeFlagsEXT, DebugUtilsMessengerCallbackDataEXT*, void*, uint> pfnUserCallback;
    public void* pUserData;
}

/// <summary>
/// Native Vulkan 1.0 compute stress benchmark engine targeting Linux systems.
/// Features dynamic profiling lock management, true 0% GPU idle standby power consumption, and glitch-free telemetry.
/// </summary>
public sealed unsafe class VulkanStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550

    private const uint Workgroups = 16384;
    private const nuint BufferSize = Workgroups * VulkanShaders.LocalSizeX * sizeof(float); // 4 MB FP32 SSBO

    private static volatile bool s_isBenchmarking = false;
    private static volatile int s_targetDurationSec = 0;
    private static string s_customInputBuffer = "";
    private static bool s_isCustomFocused = false;

    private static readonly Stopwatch s_benchTimer = new();
    private static ulong s_totalDispatches = 0;
    private static ulong s_lastDispatches = 0;
    private static double s_currentDps = 0;

    private static int s_mouseX = 0;
    private static int s_mouseY = 0;

    // Vulkan Core Objects
    private static Vk _vk = null!;
    private static Instance _instance;
    private static PhysicalDevice _physicalDevice;
    private static Device _device;
    private static Queue _computeQueue;
    private static uint _computeQueueFamilyIndex;

    private static DebugUtilsMessengerEXT _debugMessenger;
    private static delegate* unmanaged[Cdecl]<Instance, VkDebugUtilsMessengerCreateInfoEXT*, AllocationCallbacks*, DebugUtilsMessengerEXT*, Result> _pfnCreateDebugMessenger;
    private static delegate* unmanaged[Cdecl]<Instance, DebugUtilsMessengerEXT, AllocationCallbacks*, void> _pfnDestroyDebugMessenger;

    private static readonly object _queueLock = new();

    private static CommandPool _cmdPool;
    private static CommandBuffer _perfCmdBuffer;
    private static CommandBuffer _stressCmdBuffer;
    private static CommandBuffer _resetCmdBuffer;

    private static DescriptorPool _descPool;
    private static DescriptorSetLayout _descLayout;
    private static DescriptorSet _descSet;
    private static PipelineLayout _pipeLayout;
    private static Pipeline _pipeline;
    private static ShaderModule _shaderModule;

    private static VkBuffer _ssboBuffer;
    private static DeviceMemory _ssboMemory;

    private static VulkanPerfQueryManager? s_perfQueryManager;

    /// <summary>
    /// Executes the Vulkan 1.0 compute stress test pipeline and runs the OpenGL UI frontend.
    /// </summary>
    public static void Run(CancellationToken hostCt, int initialDuration = 0, int selectedGpuIndex = 0)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        _vk = Vk.GetApi();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[VulkanEngine] Starting Vulkan subsystem initialization (NativeAOT)...");
        Console.ResetColor();

        // 1. Probe Instance Layers
        uint layerCount = 0;
        _vk.EnumerateInstanceLayerProperties(&layerCount, null);
        LayerProperties* pLayers = stackalloc LayerProperties[(int)layerCount];
        _vk.EnumerateInstanceLayerProperties(&layerCount, pLayers);

        bool validationLayerFound = false;
        for (uint i = 0; i < layerCount; i++)
        {
            string lName = Marshal.PtrToStringAnsi((nint)pLayers[i].LayerName) ?? "";
            if (lName == "VK_LAYER_KHRONOS_validation")
            {
                validationLayerFound = true;
                break;
            }
        }

        // 2. Probe Instance Extensions
        uint instExtCount = 0;
        _vk.EnumerateInstanceExtensionProperties((byte*)null, &instExtCount, null);
        ExtensionProperties* pInstExts = stackalloc ExtensionProperties[(int)instExtCount];
        _vk.EnumerateInstanceExtensionProperties((byte*)null, &instExtCount, pInstExts);

        bool hasPhysProps2 = false;
        bool hasDebugUtils = false;
        for (uint i = 0; i < instExtCount; i++)
        {
            string extName = Marshal.PtrToStringAnsi((nint)pInstExts[i].ExtensionName) ?? "";
            if (extName == "VK_KHR_get_physical_device_properties2") hasPhysProps2 = true;
            if (extName == "VK_EXT_debug_utils") hasDebugUtils = true;
        }

        Console.WriteLine($"[VulkanEngine] [Init: 1/12] Extensions: VK_KHR_get_physical_device_properties2: {(hasPhysProps2 ? "YES" : "NO")}, VK_EXT_debug_utils: {(hasDebugUtils ? "YES" : "NO")}, Validation Layer: {(validationLayerFound ? "YES" : "NO")}");

        List<string> enabledInstExts = new();
        if (hasPhysProps2) enabledInstExts.Add("VK_KHR_get_physical_device_properties2");
        if (hasDebugUtils) enabledInstExts.Add("VK_EXT_debug_utils");

        List<string> enabledLayers = new();
        if (validationLayerFound) enabledLayers.Add("VK_LAYER_KHRONOS_validation");

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("GPU-T.StressTest"),
            ApplicationVersion = Vk.MakeVersion(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("GPU-T"),
            EngineVersion = Vk.MakeVersion(1, 0, 0),
            ApiVersion = Vk.Version10
        };

        InstanceCreateInfo instInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };

        byte** ppInstExts = (byte**)SilkMarshal.StringArrayToPtr(enabledInstExts.ToArray());
        instInfo.EnabledExtensionCount = (uint)enabledInstExts.Count;
        instInfo.PpEnabledExtensionNames = ppInstExts;

        byte** ppLayers = (byte**)SilkMarshal.StringArrayToPtr(enabledLayers.ToArray());
        instInfo.EnabledLayerCount = (uint)enabledLayers.Count;
        instInfo.PpEnabledLayerNames = ppLayers;

        Result instRes = _vk.CreateInstance(&instInfo, null, out _instance);
        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);
        SilkMarshal.Free((nint)ppInstExts);
        SilkMarshal.Free((nint)ppLayers);

        if (instRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan 1.0 Instance: {instRes}");

        // 3. Register NativeAOT Debug Messenger
        if (hasDebugUtils)
        {
            nint pCreate = GetInstanceProc("vkCreateDebugUtilsMessengerEXT");
            nint pDestroy = GetInstanceProc("vkDestroyDebugUtilsMessengerEXT");

            if (pCreate != nint.Zero && pDestroy != nint.Zero)
            {
                _pfnCreateDebugMessenger = (delegate* unmanaged[Cdecl]<Instance, VkDebugUtilsMessengerCreateInfoEXT*, AllocationCallbacks*, DebugUtilsMessengerEXT*, Result>)pCreate;
                _pfnDestroyDebugMessenger = (delegate* unmanaged[Cdecl]<Instance, DebugUtilsMessengerEXT, AllocationCallbacks*, void>)pDestroy;

                const uint severityMask = 0x00000100 | 0x00001000;
                const uint typeMask = 0x00000001 | 0x00000002 | 0x00000004;

                VkDebugUtilsMessengerCreateInfoEXT dbgInfo = new()
                {
                    sType = StructureType.DebugUtilsMessengerCreateInfoExt,
                    messageSeverity = (DebugUtilsMessageSeverityFlagsEXT)severityMask,
                    messageType = (DebugUtilsMessageTypeFlagsEXT)typeMask,
                    pfnUserCallback = &DebugCallback
                };

                DebugUtilsMessengerEXT messenger;
                Result dbgRes = _pfnCreateDebugMessenger(_instance, &dbgInfo, null, &messenger);
                if (dbgRes == Result.Success)
                {
                    _debugMessenger = messenger;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[VulkanEngine] Khronos Validation Messenger active and listening (NativeAOT).");
                    Console.ResetColor();
                }
            }
        }

        // 4. Select Physical Device
        uint devCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, &devCount, null);
        if (devCount == 0) throw new InvalidOperationException("No Vulkan physical devices found on Linux host.");

        PhysicalDevice* pDevs = stackalloc PhysicalDevice[(int)devCount];
        _vk.EnumeratePhysicalDevices(_instance, &devCount, pDevs);

        int gpuIdx = Math.Clamp(selectedGpuIndex, 0, (int)devCount - 1);
        _physicalDevice = pDevs[gpuIdx];

        PhysicalDeviceProperties props;
        _vk.GetPhysicalDeviceProperties(_physicalDevice, &props);
        string devName = Marshal.PtrToStringAnsi((nint)props.DeviceName) ?? "Vulkan GPU";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[VulkanEngine] [Init: 2/12] Selected Device: [{gpuIdx}] {devName}");
        Console.ResetColor();

        // 5. Select Compute Queue Family
        uint qfCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &qfCount, null);
        QueueFamilyProperties* pQf = stackalloc QueueFamilyProperties[(int)qfCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &qfCount, pQf);

        _computeQueueFamilyIndex = uint.MaxValue;
        for (uint i = 0; i < qfCount; i++)
        {
            if (pQf[i].QueueFlags.HasFlag(QueueFlags.ComputeBit))
            {
                _computeQueueFamilyIndex = i;
                break;
            }
        }
        if (_computeQueueFamilyIndex == uint.MaxValue)
            throw new InvalidOperationException("No compute-capable queue family found on selected device.");

        Console.WriteLine($"[VulkanEngine] [Init: 3/12] Found Compute Queue Family: #{_computeQueueFamilyIndex}");

        // 6. Check VK_KHR_performance_query Device Extension support
        uint devExtCount = 0;
        _vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &devExtCount, null);
        ExtensionProperties* pDevExts = stackalloc ExtensionProperties[(int)devExtCount];
        _vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &devExtCount, pDevExts);

        bool perfQuerySupported = false;
        if (hasPhysProps2)
        {
            for (uint i = 0; i < devExtCount; i++)
            {
                if (Marshal.PtrToStringAnsi((nint)pDevExts[i].ExtensionName) == "VK_KHR_performance_query")
                {
                    perfQuerySupported = true;
                    break;
                }
            }
        }

        Console.WriteLine($"[VulkanEngine] [Init: 4/12] VK_KHR_performance_query supported: {(perfQuerySupported ? "YES" : "NO")}");

        // 7. Create Logical Device
        float queuePriority = 1.0f;
        DeviceQueueCreateInfo qInfo = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _computeQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        DeviceCreateInfo devInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &qInfo
        };

        PhysicalDevicePerformanceQueryFeaturesKHR perfFeatures = new()
        {
            SType = (StructureType)1000116000,
            PerformanceCounterQueryPools = true
        };

        nint pExtName = nint.Zero;
        if (perfQuerySupported)
        {
            pExtName = SilkMarshal.StringToPtr("VK_KHR_performance_query");
            byte** ppExtNames = stackalloc byte*[1];
            ppExtNames[0] = (byte*)pExtName;
            devInfo.EnabledExtensionCount = 1;
            devInfo.PpEnabledExtensionNames = ppExtNames;
            devInfo.PNext = &perfFeatures;
        }

        Result devRes = _vk.CreateDevice(_physicalDevice, &devInfo, null, out _device);
        if (pExtName != nint.Zero) SilkMarshal.Free(pExtName);

        if (devRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan logical device: {devRes}");

        _vk.GetDeviceQueue(_device, _computeQueueFamilyIndex, 0, out _computeQueue);
        Console.WriteLine($"[VulkanEngine] [Init: 5/12] Logical Device created successfully.");

        // 8. Initialize Performance Query Manager
        if (perfQuerySupported)
        {
            s_perfQueryManager = new VulkanPerfQueryManager(_vk, _instance, _device, _physicalDevice, _computeQueueFamilyIndex, 1);
        }
        Console.WriteLine($"[VulkanEngine] [Init: 6/12] Performance Query Manager active: {s_perfQueryManager?.IsSupported == true}");

        // 9. Allocate SSBO Buffer in Device-Local Memory
        BufferCreateInfo bufInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = BufferSize,
            Usage = BufferUsageFlags.StorageBufferBit,
            SharingMode = SharingMode.Exclusive
        };

        Result bufRes = _vk.CreateBuffer(_device, &bufInfo, null, out _ssboBuffer);
        if (bufRes != Result.Success) throw new InvalidOperationException($"vkCreateBuffer failed: {bufRes}");

        MemoryRequirements memReqs;
        _vk.GetBufferMemoryRequirements(_device, _ssboBuffer, &memReqs);

        PhysicalDeviceMemoryProperties memProps;
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, &memProps);

        uint memTypeIndex = uint.MaxValue;
        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((memReqs.MemoryTypeBits & (1 << (int)i)) != 0 &&
                (memProps.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0)
            {
                memTypeIndex = i;
                break;
            }
        }
        if (memTypeIndex == uint.MaxValue)
        {
            for (uint i = 0; i < memProps.MemoryTypeCount; i++)
            {
                if ((memReqs.MemoryTypeBits & (1 << (int)i)) != 0)
                {
                    memTypeIndex = i;
                    break;
                }
            }
        }

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = memTypeIndex
        };

        Result memRes = _vk.AllocateMemory(_device, &allocInfo, null, out _ssboMemory);
        if (memRes != Result.Success) throw new InvalidOperationException($"vkAllocateMemory failed: {memRes}");

        _vk.BindBufferMemory(_device, _ssboBuffer, _ssboMemory, 0);
        Console.WriteLine($"[VulkanEngine] [Init: 7/12] SSBO Buffer ({BufferSize / 1024 / 1024} MB) bound in Device-Local memory.");

        // 10. Create Compute Pipeline
        var spirvCode = VulkanShaders.StressComputeSpirV;
        fixed (uint* pCode = spirvCode)
        {
            ShaderModuleCreateInfo smInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)(spirvCode.Length * sizeof(uint)),
                PCode = pCode
            };
            Result smRes = _vk.CreateShaderModule(_device, &smInfo, null, out _shaderModule);
            if (smRes != Result.Success) throw new InvalidOperationException($"vkCreateShaderModule failed: {smRes}");
        }

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
            BindingCount = 1,
            PBindings = &binding
        };
        Result dslRes = _vk.CreateDescriptorSetLayout(_device, &dslInfo, null, out _descLayout);
        if (dslRes != Result.Success) throw new InvalidOperationException($"vkCreateDescriptorSetLayout failed: {dslRes}");

        var dsl = _descLayout;
        PipelineLayoutCreateInfo plInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &dsl
        };
        Result plRes = _vk.CreatePipelineLayout(_device, &plInfo, null, out _pipeLayout);
        if (plRes != Result.Success) throw new InvalidOperationException($"vkCreatePipelineLayout failed: {plRes}");

        nint pMain = SilkMarshal.StringToPtr("main");
        PipelineShaderStageCreateInfo stageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = _shaderModule,
            PName = (byte*)pMain
        };

        ComputePipelineCreateInfo pipeInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = stageInfo,
            Layout = _pipeLayout
        };

        Result pipeRes = _vk.CreateComputePipelines(_device, default, 1, &pipeInfo, null, out _pipeline);
        SilkMarshal.Free(pMain);
        if (pipeRes != Result.Success) throw new InvalidOperationException($"vkCreateComputePipelines failed: {pipeRes}");

        Console.WriteLine($"[VulkanEngine] [Init: 8/12] FP32 FMA Compute Pipeline compiled successfully.");

        // 11. Allocate & Update Descriptor Set
        DescriptorPoolSize poolSize = new() { Type = DescriptorType.StorageBuffer, DescriptorCount = 1 };
        DescriptorPoolCreateInfo dpInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 1,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize
        };
        Result dpRes = _vk.CreateDescriptorPool(_device, &dpInfo, null, out _descPool);
        if (dpRes != Result.Success) throw new InvalidOperationException($"vkCreateDescriptorPool failed: {dpRes}");

        var setDsl = _descLayout;
        DescriptorSetAllocateInfo dsAlloc = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descPool,
            DescriptorSetCount = 1,
            PSetLayouts = &setDsl
        };

        fixed (DescriptorSet* pSet = &_descSet)
        {
            Result dsRes = _vk.AllocateDescriptorSets(_device, &dsAlloc, pSet);
            if (dsRes != Result.Success) throw new InvalidOperationException($"vkAllocateDescriptorSets failed: {dsRes}");
        }

        DescriptorBufferInfo descBufInfo = new()
        {
            Buffer = _ssboBuffer,
            Offset = 0,
            Range = BufferSize
        };

        var ds = _descSet;
        WriteDescriptorSet writeDs = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = ds,
            DstBinding = 0,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            PBufferInfo = &descBufInfo
        };
        _vk.UpdateDescriptorSets(_device, 1, &writeDs, 0, null);
        Console.WriteLine($"[VulkanEngine] [Init: 9/12] Descriptor sets allocated and updated.");

        // 12. Allocate Command Buffers
        CommandPoolCreateInfo cpInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _computeQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };
        Result cpRes = _vk.CreateCommandPool(_device, &cpInfo, null, out _cmdPool);
        if (cpRes != Result.Success) throw new InvalidOperationException($"vkCreateCommandPool failed: {cpRes}");

        CommandBufferAllocateInfo cbAlloc = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _cmdPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        _vk.AllocateCommandBuffers(_device, &cbAlloc, out _perfCmdBuffer);
        _vk.AllocateCommandBuffers(_device, &cbAlloc, out _stressCmdBuffer);
        _vk.AllocateCommandBuffers(_device, &cbAlloc, out _resetCmdBuffer);
        Console.WriteLine($"[VulkanEngine] [Init: 10/12] Command buffer pools prepared.");

        // 13. Dedicated Compute Thread (True 0% GPU load in idle)
        using var computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var computeThread = new Thread(() =>
        {
            var pcb = _perfCmdBuffer;
            var scb = _stressCmdBuffer;
            var rcb = _resetCmdBuffer;

            PerformanceQuerySubmitInfoKHR perfSubmitInfo = new()
            {
                SType = (StructureType)1000116003,
                CounterPassIndex = 0
            };

            SubmitInfo perfSubmit = new()
            {
                SType = StructureType.SubmitInfo,
                PNext = s_perfQueryManager?.IsSupported == true ? &perfSubmitInfo : null,
                CommandBufferCount = 1,
                PCommandBuffers = &pcb
            };

            SubmitInfo stressSubmit = new()
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &scb
            };

            SubmitInfo resetSubmit = new()
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &rcb
            };

            while (!computeCts.Token.IsCancellationRequested)
            {
                if (!s_isBenchmarking)
                {
                    Thread.Sleep(50);
                    continue;
                }

                lock (_queueLock)
                {
                    if (s_perfQueryManager?.IsSupported == true && s_perfQueryManager.IsLockAcquired)
                    {
                        _vk.QueueSubmit(_computeQueue, 1, &resetSubmit, default);
                        _vk.QueueWaitIdle(_computeQueue);

                        _vk.QueueSubmit(_computeQueue, 1, &perfSubmit, default);
                    }

                    for (int p = 0; p < 15; p++)
                    {
                        _vk.QueueSubmit(_computeQueue, 1, &stressSubmit, default);
                    }
                    s_totalDispatches += 16;

                    _vk.QueueWaitIdle(_computeQueue);

                    s_perfQueryManager?.FetchResults(0);
                }
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };
        computeThread.Start();
        Console.WriteLine($"[VulkanEngine] [Init: 11/12] Compute execution worker thread spawned.");

        // 14. OpenGL UI Frontend Window with VSync
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Render Test & Vulkan Compute Agent (Linux Native)";
        winOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        winOptions.VSync = true;
        winOptions.FramesPerSecond = 60;
        winOptions.UpdatesPerSecond = 60;
        winOptions.WindowBorder = WindowBorder.Fixed;
        winOptions.IsVisible = true;

        using var window = Window.Create(winOptions);

        GL? gl = null;
        uint uiProgram = 0, vao = 0, vbo = 0, uiTexture = 0;
        uint[] uiPixels = new uint[WinWidth * WinHeight];
        var perfSw = Stopwatch.StartNew();
        float animTime = 0f;

        window.Load += () =>
        {
            gl = GL.GetApi(window);
            gl.Disable(EnableCap.DepthTest);
            gl.Disable(EnableCap.CullFace);
            gl.ClearColor(0.08f, 0.09f, 0.12f, 1.0f);

            string vsSource = @"#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() { gl_Position = vec4(aPos, 0.0, 1.0); TexCoord = aTexCoord; }";

            string fsSource = @"#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uUiTexture;
void main() { FragColor = texture(uUiTexture, TexCoord); }";

            uiProgram = CreateGlProgram(gl, vsSource, fsSource);
            gl.UseProgram(uiProgram);

            float[] quadVertices = [
                -1.0f, -1.0f,  0.0f, 1.0f,
                 1.0f, -1.0f,  1.0f, 1.0f,
                 1.0f,  1.0f,  1.0f, 0.0f,

                -1.0f, -1.0f,  0.0f, 1.0f,
                 1.0f,  1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f,  0.0f, 0.0f
            ];

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
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)WinWidth, (uint)WinHeight, 0, PixelFormat.Bgra, PixelType.UnsignedByte, (void*)0);

            IInputContext input = window.CreateInput();
            foreach (var mouse in input.Mice)
            {
                mouse.MouseMove += (m, pos) => { s_mouseX = (int)pos.X; s_mouseY = (int)pos.Y; };
                mouse.MouseDown += (m, btn) => { if (btn == MouseButton.Left) HandleMouseClick(s_mouseX, s_mouseY); };
            }
            foreach (var kb in input.Keyboards)
            {
                kb.KeyChar += (k, c) =>
                {
                    if (s_isCustomFocused && char.IsDigit(c) && s_customInputBuffer.Length < 5)
                    {
                        s_customInputBuffer += c;
                        if (int.TryParse(s_customInputBuffer, out int val)) s_targetDurationSec = val;
                    }
                };
                kb.KeyDown += (k, key, code) =>
                {
                    if (s_isCustomFocused)
                    {
                        if (key == Key.Backspace && s_customInputBuffer.Length > 0)
                        {
                            s_customInputBuffer = s_customInputBuffer[..^1];
                            s_targetDurationSec = int.TryParse(s_customInputBuffer, out int val) ? val : 0;
                        }
                        else if (key is Key.Enter or Key.Escape) s_isCustomFocused = false;
                    }
                    else
                    {
                        if (key == Key.Space) ToggleBenchmark();
                        else if (key == Key.Escape) window.Close();
                    }
                };
            }
        };

        window.Update += (delta) =>
        {
            if (hostCt.IsCancellationRequested) window.Close();

            if (s_isBenchmarking && s_targetDurationSec > 0 && s_benchTimer.Elapsed.TotalSeconds >= s_targetDurationSec)
            {
                StopBenchmark();
                Console.WriteLine($"[Benchmark] Target {s_targetDurationSec}s completed. Stopped.");
            }
        };

        window.Render += (delta) =>
        {
            if (gl == null) return;

            if (s_isBenchmarking)
            {
                animTime += 0.02f;
            }

            double elapsed = s_isBenchmarking ? s_benchTimer.Elapsed.TotalSeconds : 0.0;

            const double totalThreads = Workgroups * VulkanShaders.LocalSizeX;
            const double flopsPerDispatch = totalThreads * VulkanShaders.FlopsPerInvocation;
            double tflops = s_isBenchmarking ? (s_currentDps * flopsPerDispatch) / 1_000_000_000_000.0 : 0.0;

            string? hwSensor = s_perfQueryManager?.IsSupported == true ? s_perfQueryManager.FormattedCounterValue : null;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "Vulkan 1.0 Compute (Linux)", devName, s_isBenchmarking, s_targetDurationSec,
                    s_customInputBuffer, s_isCustomFocused,
                    elapsed, s_currentDps, tflops,
                    hwSensor,
                    ThemePalette.Vulkan,
                    s_mouseX, s_mouseY, animTime);

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

            // Glitch-free DPS calculation with strict unsigned underflow protection
            if (perfSw.ElapsedMilliseconds >= 250)
            {
                double sec = Math.Max(0.001, perfSw.Elapsed.TotalSeconds);
                ulong currentTotal = s_totalDispatches;

                if (s_isBenchmarking && currentTotal >= s_lastDispatches)
                {
                    s_currentDps = (currentTotal - s_lastDispatches) / sec;
                }
                else
                {
                    s_currentDps = 0.0;
                }

                s_lastDispatches = currentTotal;
                perfSw.Restart();
            }
        };

        window.Closing += () =>
        {
            computeCts.Cancel();
            computeThread.Join(500);

            if (gl != null)
            {
                gl.DeleteTexture(uiTexture);
                gl.DeleteBuffer(vbo);
                gl.DeleteVertexArray(vao);
                gl.DeleteProgram(uiProgram);
            }

            s_perfQueryManager?.Dispose();

            if (_device.Handle != 0)
            {
                lock (_queueLock)
                {
                    _vk.DeviceWaitIdle(_device);
                }

                _vk.DestroyPipeline(_device, _pipeline, null);
                _vk.DestroyPipelineLayout(_device, _pipeLayout, null);
                _vk.DestroyDescriptorPool(_device, _descPool, null);
                _vk.DestroyDescriptorSetLayout(_device, _descLayout, null);
                _vk.DestroyShaderModule(_device, _shaderModule, null);

                _vk.DestroyCommandPool(_device, _cmdPool, null);

                _vk.DestroyBuffer(_device, _ssboBuffer, null);
                _vk.FreeMemory(_device, _ssboMemory, null);

                _vk.DestroyDevice(_device, null);
            }

            if (_debugMessenger.Handle != 0 && _pfnDestroyDebugMessenger != null)
            {
                _pfnDestroyDebugMessenger(_instance, _debugMessenger, null);
            }

            if (_instance.Handle != 0)
            {
                _vk.DestroyInstance(_instance, null);
            }

            Console.WriteLine("[VulkanEngine] Vulkan context destroyed cleanly. Exit 0.");
        };

        Console.WriteLine($"[VulkanEngine] [Init: 12/12] Launching window rendering loop...");
        window.Run();
    }

    private static void RecordCommandBuffers()
    {
        CommandBufferBeginInfo cbBegin = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.SimultaneousUseBit
        };

        // 1. Reset Command Buffer
        if (s_perfQueryManager?.IsSupported == true)
        {
            _vk.BeginCommandBuffer(_resetCmdBuffer, &cbBegin);
            _vk.CmdResetQueryPool(_resetCmdBuffer, s_perfQueryManager.QueryPool, 0, 1);
            _vk.EndCommandBuffer(_resetCmdBuffer);
        }

        // 2. Profiled Command Buffer (Query Begin/End around dispatch)
        var ds = _descSet;
        _vk.BeginCommandBuffer(_perfCmdBuffer, &cbBegin);
        if (s_perfQueryManager?.IsSupported == true)
        {
            _vk.CmdBeginQuery(_perfCmdBuffer, s_perfQueryManager.QueryPool, 0, 0);
        }
        _vk.CmdBindPipeline(_perfCmdBuffer, PipelineBindPoint.Compute, _pipeline);
        _vk.CmdBindDescriptorSets(_perfCmdBuffer, PipelineBindPoint.Compute, _pipeLayout, 0, 1, &ds, 0, null);
        _vk.CmdDispatch(_perfCmdBuffer, Workgroups, 1, 1);
        if (s_perfQueryManager?.IsSupported == true)
        {
            _vk.CmdEndQuery(_perfCmdBuffer, s_perfQueryManager.QueryPool, 0);
        }
        _vk.EndCommandBuffer(_perfCmdBuffer);

        // 3. Pure Stress Command Buffer (100% compute ALU saturation)
        _vk.BeginCommandBuffer(_stressCmdBuffer, &cbBegin);
        _vk.CmdBindPipeline(_stressCmdBuffer, PipelineBindPoint.Compute, _pipeline);
        _vk.CmdBindDescriptorSets(_stressCmdBuffer, PipelineBindPoint.Compute, _pipeLayout, 0, 1, &ds, 0, null);
        _vk.CmdDispatch(_stressCmdBuffer, Workgroups, 1, 1);
        _vk.EndCommandBuffer(_stressCmdBuffer);
    }

    private static void StartBenchmark()
    {
        lock (_queueLock)
        {
            s_perfQueryManager?.AcquireLock();
            RecordCommandBuffers();
            s_totalDispatches = 0;
            s_lastDispatches = 0;
            s_currentDps = 0.0;
            s_benchTimer.Restart();
            s_isBenchmarking = true;
        }
        Console.WriteLine("[UI] >> BENCHMARK STARTED <<");
    }

    private static void StopBenchmark()
    {
        s_isBenchmarking = false;
        s_currentDps = 0.0;
        s_benchTimer.Stop();
        lock (_queueLock)
        {
            if (_device.Handle != 0) _vk.QueueWaitIdle(_computeQueue);
            s_perfQueryManager?.ReleaseLock();
        }
        Console.WriteLine("[UI] >> BENCHMARK STOPPED <<");
    }

    private static void ToggleBenchmark()
    {
        if (s_isBenchmarking) StopBenchmark();
        else StartBenchmark();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData)
    {
        string msg = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage) ?? "Validation event";
        string id = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessageIdName) ?? "VUID";

        if (((uint)messageSeverity & 0x00001000) != 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[VK-VALIDATION-ERROR] [{id}]\n--> {msg}\n");
            Console.ResetColor();
        }
        else if (((uint)messageSeverity & 0x00000100) != 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[VK-VALIDATION-WARN]  [{id}] {msg}");
            Console.ResetColor();
        }

        return Vk.False;
    }

    private static nint GetInstanceProc(string name)
    {
        nint strPtr = SilkMarshal.StringToPtr(name);
        nint fnPtr = _vk.GetInstanceProcAddr(_instance, (byte*)strPtr);
        SilkMarshal.Free(strPtr);
        return fnPtr;
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

    private static void HandleMouseClick(int x, int y)
    {
        if (PixelUiEngine.BtnStartStop.Contains(x, y))
        {
            s_isCustomFocused = false;
            ToggleBenchmark();
        }
        else if (PixelUiEngine.Btn10s.Contains(x, y)) { s_targetDurationSec = 10; s_customInputBuffer = "10"; s_isCustomFocused = false; }
        else if (PixelUiEngine.Btn30s.Contains(x, y)) { s_targetDurationSec = 30; s_customInputBuffer = "30"; s_isCustomFocused = false; }
        else if (PixelUiEngine.Btn60s.Contains(x, y)) { s_targetDurationSec = 60; s_customInputBuffer = "60"; s_isCustomFocused = false; }
        else if (PixelUiEngine.BtnUnlimited.Contains(x, y)) { s_targetDurationSec = 0; s_customInputBuffer = ""; s_isCustomFocused = false; }
        else if (PixelUiEngine.InputCustom.Contains(x, y)) s_isCustomFocused = true;
        else if (PixelUiEngine.BtnMinus.Contains(x, y))
        {
            s_targetDurationSec = Math.Max(1, s_targetDurationSec - 5);
            s_customInputBuffer = s_targetDurationSec.ToString();
            s_isCustomFocused = false;
        }
        else if (PixelUiEngine.BtnPlus.Contains(x, y))
        {
            s_targetDurationSec += 5;
            s_customInputBuffer = s_targetDurationSec.ToString();
            s_isCustomFocused = false;
        }
        else s_isCustomFocused = false;
    }
}