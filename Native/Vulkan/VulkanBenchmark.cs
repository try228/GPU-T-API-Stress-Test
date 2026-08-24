using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

using VkSemaphore = Silk.NET.Vulkan.Semaphore;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkImage = Silk.NET.Vulkan.Image;
using MouseButton = Silk.NET.Input.MouseButton;

namespace GpuT.Agent.Native.Vulkan;

public sealed unsafe class VulkanStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550
    private const uint Workgroups = 8192;
    private const ulong BufferSizeBytes = 16 * 1024 * 1024;
    private const ulong StagingSizeBytes = WinWidth * WinHeight * 4;

    private static volatile bool s_isBenchmarking = false;
    private static volatile int s_targetDurationSec = 0;
    private static string s_customInputBuffer = "";
    private static bool s_isCustomFocused = false;

    private static Stopwatch s_benchTimer = new();
    private static ulong s_totalDispatches = 0;
    private static double s_currentDps = 0;
    private static IWindow? s_windowInstance = null;
    private static VulkanPerfQueryManager? s_perfQuery = null;

    private static int s_mouseX = 0;
    private static int s_mouseY = 0;

    public static void Run(CancellationToken hostCt, int initialDuration = 0)
    {
        // 1. Статическая регистрация GLFW для Native AOT
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        // Фиксированное окно 900x550 (Non-resizable)
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Render Test & Vulkan Stress Agent";
        winOptions.API = GraphicsAPI.None;
        winOptions.VSync = false;
        winOptions.WindowBorder = WindowBorder.Fixed;

        using var window = Window.Create(winOptions);
        s_windowInstance = window;
        window.Initialize();

        var vk = Vk.GetApi();
        var glfw = Glfw.GetApi();

        // 2. Получение расширений от GLFW
        uint glfwExtCount = 0;
        byte** glfwExts = glfw.GetRequiredInstanceExtensions(out glfwExtCount);

        List<nint> instExtList = new();
        for (int i = 0; i < glfwExtCount; i++) instExtList.Add((nint)glfwExts[i]);

        // 3. Динамическая проверка VK_KHR_get_physical_device_properties2 (для старых драйверов 2016 г.)
        uint instPropExtCount = 0;
        vk.EnumerateInstanceExtensionProperties((byte*)null, &instPropExtCount, null);
        ExtensionProperties* pInstExtProps = stackalloc ExtensionProperties[(int)instPropExtCount];
        vk.EnumerateInstanceExtensionProperties((byte*)null, &instPropExtCount, pInstExtProps);

        bool hasProp2 = false;
        for (uint i = 0; i < instPropExtCount; i++)
        {
            string ext = Marshal.PtrToStringAnsi((nint)pInstExtProps[i].ExtensionName) ?? "";
            if (ext == "VK_KHR_get_physical_device_properties2")
            {
                hasProp2 = true;
                break;
            }
        }

        nint prop2Ext = nint.Zero;
        if (hasProp2)
        {
            prop2Ext = SilkMarshal.StringToPtr("VK_KHR_get_physical_device_properties2");
            instExtList.Add(prop2Ext);
        }

        nint* pInstExts = stackalloc nint[instExtList.Count];
        for (int i = 0; i < instExtList.Count; i++) pInstExts[i] = instExtList[i];

        InstanceCreateInfo instInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            EnabledExtensionCount = (uint)instExtList.Count,
            PpEnabledExtensionNames = (byte**)pInstExts
        };

        Instance instance;
        Result instRes = vk.CreateInstance(&instInfo, null, &instance);
        if (prop2Ext != nint.Zero) SilkMarshal.Free(prop2Ext);

        if (instRes != Result.Success)
            throw new InvalidOperationException($"vkCreateInstance failed with code {instRes}");

        // 4. Создание Vulkan Surface
        SurfaceKHR surface;
        int surfRes = glfw.CreateWindowSurface(
            new VkHandle(instance.Handle),
            (WindowHandle*)window.Handle,
            null,
            (VkNonDispatchableHandle*)&surface);

        if (surfRes != 0)
            throw new InvalidOperationException($"glfw.CreateWindowSurface failed with code {surfRes}");

        var khrSurface = new KhrSurface(vk.Context);

        // 5. Physical Device & Queue Family (Строго GraphicsBit)
        uint devCount = 0;
        vk.EnumeratePhysicalDevices(instance, &devCount, null);
        if (devCount == 0) throw new InvalidOperationException("No Vulkan physical devices found.");

        PhysicalDevice* pDevices = stackalloc PhysicalDevice[(int)devCount];
        vk.EnumeratePhysicalDevices(instance, &devCount, pDevices);
        PhysicalDevice physicalDevice = pDevices[0];

        PhysicalDeviceProperties* pProps = stackalloc PhysicalDeviceProperties[1];
        vk.GetPhysicalDeviceProperties(physicalDevice, pProps);
        string deviceName = Marshal.PtrToStringAnsi((nint)pProps->DeviceName) ?? "Vulkan GPU";

        uint queueFamilyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
        QueueFamilyProperties* qProps = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, qProps);

        uint queueFamilyIndex = uint.MaxValue;
        for (uint i = 0; i < queueFamilyCount; i++)
        {
            Bool32 supported = false;
            khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface, &supported);
            if (supported && qProps[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamilyIndex = i;
                break;
            }
        }

        if (queueFamilyIndex == uint.MaxValue)
            throw new InvalidOperationException("No suitable Graphics + Present queue family found on this GPU.");

        // 6. Logical Device
        uint devExtCount = 0;
        vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &devExtCount, null);
        ExtensionProperties* pExtProps = stackalloc ExtensionProperties[(int)devExtCount];
        vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &devExtCount, pExtProps);

        bool hasSwapchain = false;
        bool hasPerfQuery = false;
        for (uint i = 0; i < devExtCount; i++)
        {
            string ext = Marshal.PtrToStringAnsi((nint)pExtProps[i].ExtensionName) ?? "";
            if (ext == "VK_KHR_swapchain") hasSwapchain = true;
            if (ext == "VK_KHR_performance_query") hasPerfQuery = true;
        }

        if (!hasSwapchain) throw new InvalidOperationException("VK_KHR_swapchain is not supported.");

        float priority = 1.0f;
        DeviceQueueCreateInfo queueCreateInfo = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = queueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &priority
        };

        List<nint> devExtList = new();
        nint scExtName = SilkMarshal.StringToPtr("VK_KHR_swapchain");
        devExtList.Add(scExtName);

        nint pqExtName = nint.Zero;
        if (hasPerfQuery && hasProp2)
        {
            pqExtName = SilkMarshal.StringToPtr("VK_KHR_performance_query");
            devExtList.Add(pqExtName);
        }

        nint* pDevExts = stackalloc nint[devExtList.Count];
        for (int i = 0; i < devExtList.Count; i++) pDevExts[i] = devExtList[i];

        PhysicalDevicePerformanceQueryFeaturesKHR perfFeatures = new()
        {
            SType = StructureType.PhysicalDevicePerformanceQueryFeaturesKhr,
            PerformanceCounterQueryPools = true,
            PerformanceCounterMultipleQueryPools = true
        };

        DeviceCreateInfo devInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            EnabledExtensionCount = (uint)devExtList.Count,
            PpEnabledExtensionNames = (byte**)pDevExts,
            PNext = (hasPerfQuery && hasProp2) ? &perfFeatures : null
        };

        Device device;
        bool perfQueryActive = hasPerfQuery && hasProp2;
        Result devRes = vk.CreateDevice(physicalDevice, &devInfo, null, &device);
        SilkMarshal.Free(scExtName);
        if (pqExtName != nint.Zero) SilkMarshal.Free(pqExtName);

        if (devRes != Result.Success)
        {
            perfQueryActive = false;
            nint fallbackExt = SilkMarshal.StringToPtr("VK_KHR_swapchain");
            devInfo.EnabledExtensionCount = 1;
            devInfo.PpEnabledExtensionNames = (byte**)&fallbackExt;
            devInfo.PNext = null;
            vk.CreateDevice(physicalDevice, &devInfo, null, &device);
            SilkMarshal.Free(fallbackExt);
        }

        Queue queue;
        vk.GetDeviceQueue(device, queueFamilyIndex, 0, &queue);

        // 7. Динамический опрос и выбор формата поверхности (GetPhysicalDeviceSurfaceFormats)
        uint surfFormatCount = 0;
        khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &surfFormatCount, null);
        SurfaceFormatKHR* pSurfFormats = stackalloc SurfaceFormatKHR[(int)surfFormatCount];
        khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &surfFormatCount, pSurfFormats);

        SurfaceFormatKHR selectedFormat = pSurfFormats[0];
        for (uint i = 0; i < surfFormatCount; i++)
        {
            if (pSurfFormats[i].Format is Format.B8G8R8A8Unorm or Format.B8G8R8A8Srgb)
            {
                selectedFormat = pSurfFormats[i];
                break;
            }
        }

        // 8. Swapchain
        var khrSwapchain = new KhrSwapchain(vk.Context);
        SurfaceCapabilitiesKHR surfCaps;
        khrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, &surfCaps);

        Extent2D swapExtent = surfCaps.CurrentExtent.Width != uint.MaxValue
            ? surfCaps.CurrentExtent
            : new Extent2D((uint)WinWidth, (uint)WinHeight);

        SwapchainCreateInfoKHR scInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = Math.Max(2, surfCaps.MinImageCount),
            ImageFormat = selectedFormat.Format,
            ImageColorSpace = selectedFormat.ColorSpace,
            ImageExtent = swapExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = surfCaps.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = PresentModeKHR.ImmediateKhr,
            Clipped = true
        };

        SwapchainKHR swapchain;
        if (khrSwapchain.CreateSwapchain(device, &scInfo, null, &swapchain) != Result.Success)
        {
            scInfo.PresentMode = PresentModeKHR.FifoKhr;
            khrSwapchain.CreateSwapchain(device, &scInfo, null, &swapchain);
        }

        uint scImageCount = 0;
        khrSwapchain.GetSwapchainImages(device, swapchain, &scImageCount, null);
        VkImage* scImages = stackalloc VkImage[(int)scImageCount];
        khrSwapchain.GetSwapchainImages(device, swapchain, &scImageCount, scImages);

        // 9. PerfQuery Manager
        using var perfQueryManager = new VulkanPerfQueryManager(vk, instance, device, physicalDevice, queueFamilyIndex, scImageCount, perfQueryActive);
        s_perfQuery = perfQueryManager;

        // 10. SSBO Buffer (Universal Allocator)
        PhysicalDeviceMemoryProperties memProps;
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProps);

        BufferCreateInfo ssboInfo = new() { SType = StructureType.BufferCreateInfo, Size = BufferSizeBytes, Usage = BufferUsageFlags.StorageBufferBit };
        VkBuffer ssboBuffer;
        vk.CreateBuffer(device, &ssboInfo, null, &ssboBuffer);
        MemoryRequirements ssboReqs = default;
        vk.GetBufferMemoryRequirements(device, ssboBuffer, &ssboReqs);

        uint ssboMemType = FindMemoryType(ssboReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit, memProps);

        MemoryAllocateInfo ssboAlloc = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = ssboReqs.Size, MemoryTypeIndex = ssboMemType };
        DeviceMemory ssboMemory;
        vk.AllocateMemory(device, &ssboAlloc, null, &ssboMemory);
        vk.BindBufferMemory(device, ssboBuffer, ssboMemory, 0);

        // 11. Staging Buffers
        VkBuffer* stageBuffers = stackalloc VkBuffer[(int)scImageCount];
        DeviceMemory* stageMemories = stackalloc DeviceMemory[(int)scImageCount];
        uint** pUiBuffers = stackalloc uint*[(int)scImageCount];

        for (int i = 0; i < scImageCount; i++)
        {
            BufferCreateInfo stageInfo = new() { SType = StructureType.BufferCreateInfo, Size = StagingSizeBytes, Usage = BufferUsageFlags.TransferSrcBit };
            vk.CreateBuffer(device, &stageInfo, null, &stageBuffers[i]);

            MemoryRequirements stageReqs = default;
            vk.GetBufferMemoryRequirements(device, stageBuffers[i], &stageReqs);

            uint stageMemType = FindMemoryType(stageReqs.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, memProps);

            MemoryAllocateInfo stageAlloc = new() { SType = StructureType.MemoryAllocateInfo, AllocationSize = stageReqs.Size, MemoryTypeIndex = stageMemType };
            vk.AllocateMemory(device, &stageAlloc, null, &stageMemories[i]);
            vk.BindBufferMemory(device, stageBuffers[i], stageMemories[i], 0);

            void* pMap = null;
            vk.MapMemory(device, stageMemories[i], 0, StagingSizeBytes, 0, &pMap);
            pUiBuffers[i] = (uint*)pMap;
        }

        // 12. Compute Pipeline
        var spirv = VulkanShaders.StressComputeSpirV;
        ShaderModule shaderModule;
        fixed (uint* pCode = spirv)
        {
            ShaderModuleCreateInfo smInfo = new() { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)(spirv.Length * sizeof(uint)), PCode = pCode };
            vk.CreateShaderModule(device, &smInfo, null, &shaderModule);
        }

        DescriptorSetLayoutBinding dsBinding = new() { Binding = 0, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };
        DescriptorSetLayoutCreateInfo dslInfo = new() { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &dsBinding };
        DescriptorSetLayout dsLayout;
        vk.CreateDescriptorSetLayout(device, &dslInfo, null, &dsLayout);

        PipelineLayoutCreateInfo plInfo = new() { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &dsLayout };
        PipelineLayout pipelineLayout;
        vk.CreatePipelineLayout(device, &plInfo, null, &pipelineLayout);

        byte* pMain = (byte*)SilkMarshal.StringToPtr("main");
        ComputePipelineCreateInfo compPipelineInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = shaderModule, PName = pMain },
            Layout = pipelineLayout,
            BasePipelineIndex = -1
        };
        Pipeline pipeline;
        Result compRes = vk.CreateComputePipelines(device, default, 1, &compPipelineInfo, null, &pipeline);
        SilkMarshal.Free((nint)pMain);

        if (compRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Compute Pipeline: {compRes}");

        DescriptorPoolSize poolSize = new() { Type = DescriptorType.StorageBuffer, DescriptorCount = 1 };
        DescriptorPoolCreateInfo poolInfo = new() { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 1, PPoolSizes = &poolSize };
        DescriptorPool descPool;
        vk.CreateDescriptorPool(device, &poolInfo, null, &descPool);

        DescriptorSet descSet;
        DescriptorSetAllocateInfo dsAllocInfo = new() { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = descPool, DescriptorSetCount = 1, PSetLayouts = &dsLayout };
        vk.AllocateDescriptorSets(device, &dsAllocInfo, &descSet);

        DescriptorBufferInfo descBufInfo = new() { Buffer = ssboBuffer, Offset = 0, Range = BufferSizeBytes };
        WriteDescriptorSet writeDs = new() { SType = StructureType.WriteDescriptorSet, DstSet = descSet, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &descBufInfo };
        vk.UpdateDescriptorSets(device, 1, &writeDs, 0, null);

        // 13. Command Buffers & Fences
        CommandPoolCreateInfo cmdPoolInfo = new() { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = queueFamilyIndex, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        CommandPool cmdPool;
        vk.CreateCommandPool(device, &cmdPoolInfo, null, &cmdPool);

        CommandBuffer* cmdBuffers = stackalloc CommandBuffer[(int)scImageCount];
        CommandBufferAllocateInfo cmdAllocInfo = new() { SType = StructureType.CommandBufferAllocateInfo, CommandPool = cmdPool, Level = CommandBufferLevel.Primary, CommandBufferCount = scImageCount };
        vk.AllocateCommandBuffers(device, &cmdAllocInfo, cmdBuffers);

        Fence* fences = stackalloc Fence[(int)scImageCount];
        FenceCreateInfo fenceInfo = new() { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
        VkSemaphore* imageAvailableSemaphores = stackalloc VkSemaphore[(int)scImageCount];
        VkSemaphore* renderFinishedSemaphores = stackalloc VkSemaphore[(int)scImageCount];
        SemaphoreCreateInfo semInfo = new() { SType = StructureType.SemaphoreCreateInfo };

        for (int i = 0; i < scImageCount; i++)
        {
            vk.CreateFence(device, &fenceInfo, null, &fences[i]);
            vk.CreateSemaphore(device, &semInfo, null, &imageAvailableSemaphores[i]);
            vk.CreateSemaphore(device, &semInfo, null, &renderFinishedSemaphores[i]);
        }

        // 14. Ввод
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
                    else if (key is Key.Enter or Key.Escape)
                    {
                        s_isCustomFocused = false;
                    }
                }
                else
                {
                    if (key == Key.Space) ToggleBenchmark();
                    else if (key == Key.Escape) s_windowInstance?.Close();
                }
            };
        }

        // 15. Render Loop
        uint frameSemaphoreIndex = 0;
        var perfSw = Stopwatch.StartNew();
        ulong lastDispatches = 0;
        float animTime = 0f;

        while (!window.IsClosing && !hostCt.IsCancellationRequested)
        {
            window.DoEvents();

            if (s_isBenchmarking && s_targetDurationSec > 0 && s_benchTimer.Elapsed.TotalSeconds >= s_targetDurationSec)
            {
                StopBenchmark();
                Console.WriteLine($"[Benchmark] Target {s_targetDurationSec}s completed. Stopped.");
            }

            if (s_isBenchmarking)
            {
                animTime += 0.02f;
            }

            uint imageIndex = 0;
            Result acqRes = khrSwapchain.AcquireNextImage(device, swapchain, ulong.MaxValue, imageAvailableSemaphores[frameSemaphoreIndex], default, &imageIndex);
            if (acqRes != Result.Success) continue;

            Fence imageFence = fences[imageIndex];
            vk.WaitForFences(device, 1, &imageFence, true, ulong.MaxValue);
            vk.ResetFences(device, 1, &imageFence);

            if (s_isBenchmarking && perfQueryManager.IsQueryPoolReady)
            {
                perfQueryManager.FetchResults(imageIndex);
            }

            double elapsed = s_isBenchmarking ? s_benchTimer.Elapsed.TotalSeconds : 0.0;
            double tflops = s_isBenchmarking ? (s_currentDps * 0.524288 * 64.0) / 1000.0 : 0.0;

            // Отрисовка UI с передачей заголовка "VULKAN 1.0"
            PixelUiEngine.Render(
                pUiBuffers[imageIndex], WinWidth, WinHeight,
                "VULKAN 1.0", deviceName, s_isBenchmarking, s_targetDurationSec,
                s_customInputBuffer, s_isCustomFocused,
                elapsed, s_currentDps, tflops,
                perfQueryManager.FormattedCounterValue,
                ThemePalette.Vulkan,
                s_mouseX, s_mouseY, animTime);

            CommandBuffer cmd = cmdBuffers[imageIndex];
            CommandBufferBeginInfo beginInfo = new() { SType = StructureType.CommandBufferBeginInfo };
            vk.BeginCommandBuffer(cmd, &beginInfo);

            if (s_isBenchmarking)
            {
                if (perfQueryManager.IsQueryPoolReady)
                {
                    vk.CmdResetQueryPool(cmd, perfQueryManager.QueryPoolHandle, imageIndex, 1);
                    vk.CmdBeginQuery(cmd, perfQueryManager.QueryPoolHandle, imageIndex, 0);
                }

                vk.CmdBindPipeline(cmd, PipelineBindPoint.Compute, pipeline);
                DescriptorSet ds = descSet;
                vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Compute, pipelineLayout, 0, 1, &ds, 0, null);
                vk.CmdDispatch(cmd, Workgroups, 1, 1);
                s_totalDispatches++;

                if (perfQueryManager.IsQueryPoolReady)
                {
                    vk.CmdEndQuery(cmd, perfQueryManager.QueryPoolHandle, imageIndex);
                }
            }

            ImageMemoryBarrier barrierToDst = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.TransferWriteBit,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                Image = scImages[imageIndex],
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &barrierToDst);

            BufferImageCopy copyRegion = new()
            {
                BufferOffset = 0,
                BufferRowLength = (uint)WinWidth,
                BufferImageHeight = (uint)WinHeight,
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D((uint)WinWidth, (uint)WinHeight, 1)
            };
            vk.CmdCopyBufferToImage(cmd, stageBuffers[imageIndex], scImages[imageIndex], ImageLayout.TransferDstOptimal, 1, &copyRegion);

            ImageMemoryBarrier barrierToPresent = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.MemoryReadBit,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.PresentSrcKhr,
                Image = scImages[imageIndex],
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.BottomOfPipeBit, 0, 0, null, 0, null, 1, &barrierToPresent);

            vk.EndCommandBuffer(cmd);

            PipelineStageFlags waitStage = PipelineStageFlags.TransferBit;
            VkSemaphore imgAvail = imageAvailableSemaphores[frameSemaphoreIndex];
            VkSemaphore rndFin = renderFinishedSemaphores[frameSemaphoreIndex];

            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &imgAvail,
                PWaitDstStageMask = &waitStage,
                CommandBufferCount = 1,
                PCommandBuffers = &cmd,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = &rndFin
            };
            vk.QueueSubmit(queue, 1, &submitInfo, imageFence);

            SwapchainKHR sc = swapchain;
            PresentInfoKHR presentInfo = new()
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &rndFin,
                SwapchainCount = 1,
                PSwapchains = &sc,
                PImageIndices = &imageIndex
            };
            khrSwapchain.QueuePresent(queue, &presentInfo);

            frameSemaphoreIndex = (frameSemaphoreIndex + 1) % scImageCount;

            if (perfSw.ElapsedMilliseconds >= 250)
            {
                double sec = perfSw.Elapsed.TotalSeconds;
                s_currentDps = s_isBenchmarking ? (s_totalDispatches - lastDispatches) / sec : 0.0;
                lastDispatches = s_totalDispatches;
                perfSw.Restart();
            }

            if (!s_isBenchmarking)
            {
                Thread.Sleep(16);
            }
        }

        // 16. Освобождение
        vk.DeviceWaitIdle(device);

        for (int i = 0; i < scImageCount; i++)
        {
            vk.UnmapMemory(device, stageMemories[i]);
            vk.FreeMemory(device, stageMemories[i], null);
            vk.DestroyBuffer(device, stageBuffers[i], null);

            vk.DestroyFence(device, fences[i], null);
            vk.DestroySemaphore(device, imageAvailableSemaphores[i], null);
            vk.DestroySemaphore(device, renderFinishedSemaphores[i], null);
        }

        khrSwapchain.DestroySwapchain(device, swapchain, null);
        khrSurface.DestroySurface(instance, surface, null);
        vk.DestroyCommandPool(device, cmdPool, null);
        vk.DestroyPipeline(device, pipeline, null);
        vk.DestroyPipelineLayout(device, pipelineLayout, null);
        vk.DestroyDescriptorPool(device, descPool, null);
        vk.DestroyDescriptorSetLayout(device, dsLayout, null);
        vk.DestroyShaderModule(device, shaderModule, null);
        vk.FreeMemory(device, ssboMemory, null);
        vk.DestroyBuffer(device, ssboBuffer, null);
        vk.DestroyDevice(device, null);
        vk.DestroyInstance(instance, null);

        s_perfQuery = null;
        s_windowInstance = null;
        Console.WriteLine("[Window] Clean exit 0.");
    }

    private static uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties, PhysicalDeviceMemoryProperties memProps)
    {
        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 && (memProps.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }
        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0)
            {
                return i;
            }
        }
        return 0;
    }

    private static void HandleMouseClick(int x, int y)
    {
        if (PixelUiEngine.BtnStartStop.Contains(x, y))
        {
            s_isCustomFocused = false;
            ToggleBenchmark();
        }
        else if (PixelUiEngine.Btn10s.Contains(x, y))
        {
            s_targetDurationSec = 10;
            s_customInputBuffer = "10";
            s_isCustomFocused = false;
            Console.WriteLine("[UI] Timer: 10s");
        }
        else if (PixelUiEngine.Btn30s.Contains(x, y))
        {
            s_targetDurationSec = 30;
            s_customInputBuffer = "30";
            s_isCustomFocused = false;
            Console.WriteLine("[UI] Timer: 30s");
        }
        else if (PixelUiEngine.Btn60s.Contains(x, y))
        {
            s_targetDurationSec = 60;
            s_customInputBuffer = "60";
            s_isCustomFocused = false;
            Console.WriteLine("[UI] Timer: 60s");
        }
        else if (PixelUiEngine.BtnUnlimited.Contains(x, y))
        {
            s_targetDurationSec = 0;
            s_customInputBuffer = "";
            s_isCustomFocused = false;
            Console.WriteLine("[UI] Timer: Unlimited");
        }
        else if (PixelUiEngine.InputCustom.Contains(x, y))
        {
            s_isCustomFocused = true;
        }
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
        else
        {
            s_isCustomFocused = false;
        }
    }

    private static void ToggleBenchmark()
    {
        if (s_isBenchmarking) StopBenchmark();
        else StartBenchmark();
    }

    private static void StartBenchmark()
    {
        s_isBenchmarking = true;
        s_benchTimer.Restart();
        s_perfQuery?.AcquireLock();
        Console.WriteLine("[UI] >> BENCHMARK STARTED <<");
    }

    private static void StopBenchmark()
    {
        s_isBenchmarking = false;
        s_benchTimer.Stop();
        s_perfQuery?.ReleaseLock();
        Console.WriteLine("[UI] >> BENCHMARK STOPPED <<");
    }
}