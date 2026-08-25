using System.Diagnostics;
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
/// Native Vulkan 1.0 baseline compute queue stress benchmark engine.
/// </summary>
public sealed unsafe class VulkanStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550
    private const uint Workgroups = 16384;
    private const nuint BufferSize = Workgroups * 64 * sizeof(uint); // 4 MB SSBO

    private static volatile bool s_isBenchmarking = false;
    private static volatile int s_targetDurationSec = 0;
    private static string s_customInputBuffer = "";
    private static bool s_isCustomFocused = false;

    private static Stopwatch s_benchTimer = new();
    private static ulong s_totalDispatches = 0;
    private static double s_currentDps = 0;

    private static int s_mouseX = 0;
    private static int s_mouseY = 0;

    // Vulkan Objects
    private static Vk _vk = null!;
    private static Instance _instance;
    private static PhysicalDevice _physicalDevice;
    private static Device _device;
    private static Queue _computeQueue;
    private static uint _computeQueueFamilyIndex;

    private static CommandPool _cmdPool;
    private static CommandBuffer _cmdBuffer;
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
    /// <param name="hostCt">Host cancellation token.</param>
    /// <param name="initialDuration">Initial duration in seconds (0 = unlimited).</param>
    /// <param name="selectedGpuIndex">Physical GPU index.</param>
    public static void Run(CancellationToken hostCt, int initialDuration = 0, int selectedGpuIndex = 0)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        _vk = Vk.GetApi();

        // 1. Create Vulkan 1.0 Instance
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

        Result instRes = _vk.CreateInstance(&instInfo, null, out _instance);
        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);

        if (instRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan 1.0 Instance: {instRes}");

        // 2. Select Physical Device
        uint devCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, &devCount, null);
        if (devCount == 0) throw new InvalidOperationException("No Vulkan physical devices found.");

        PhysicalDevice* pDevs = stackalloc PhysicalDevice[(int)devCount];
        _vk.EnumeratePhysicalDevices(_instance, &devCount, pDevs);

        int gpuIdx = Math.Clamp(selectedGpuIndex, 0, (int)devCount - 1);
        _physicalDevice = pDevs[gpuIdx];

        PhysicalDeviceProperties props;
        _vk.GetPhysicalDeviceProperties(_physicalDevice, &props);
        string devName = Marshal.PtrToStringAnsi((nint)props.DeviceName) ?? "Vulkan GPU";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[VulkanEngine] API Target:      Vulkan 1.0 Compute Pipeline");
        Console.WriteLine($"[VulkanEngine] Selected Device: [{gpuIdx}] {devName}");
        Console.ResetColor();

        // 3. Find Compute Queue Family
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

        // 4. Check for VK_KHR_performance_query Device Extension support safely
        uint extCount = 0;
        _vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &extCount, null);
        ExtensionProperties* pExts = stackalloc ExtensionProperties[(int)extCount];
        _vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &extCount, pExts);

        bool perfQuerySupported = false;
        for (uint i = 0; i < extCount; i++)
        {
            string extName = Marshal.PtrToStringAnsi((nint)pExts[i].ExtensionName) ?? "";
            if (extName == "VK_KHR_performance_query")
            {
                perfQuerySupported = true;
                break;
            }
        }

        // 5. Create Logical Device
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

        nint pExtName = nint.Zero;
        if (perfQuerySupported)
        {
            pExtName = SilkMarshal.StringToPtr("VK_KHR_performance_query");
            byte** ppExtNames = stackalloc byte*[1];
            ppExtNames[0] = (byte*)pExtName;
            devInfo.EnabledExtensionCount = 1;
            devInfo.PpEnabledExtensionNames = ppExtNames;
        }

        Result devRes = _vk.CreateDevice(_physicalDevice, &devInfo, null, out _device);
        if (pExtName != nint.Zero) SilkMarshal.Free(pExtName);

        if (devRes != Result.Success)
            throw new InvalidOperationException($"Failed to create Vulkan logical device: {devRes}");

        _vk.GetDeviceQueue(_device, _computeQueueFamilyIndex, 0, out _computeQueue);

        // 6. Initialize Performance Query Manager only if extension was enabled
        if (perfQuerySupported)
        {
            s_perfQueryManager = new VulkanPerfQueryManager(_vk, _instance, _device, _physicalDevice, _computeQueueFamilyIndex, 16, true);
        }

        // 7. Allocate SSBO Buffer in GPU Device-Local Memory
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

        // 8. Create Compute Pipeline
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

        // 9. Allocate & Update Descriptor Set
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

        // 10. Record Compute Command Buffer
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
        Result cbRes = _vk.AllocateCommandBuffers(_device, &cbAlloc, out _cmdBuffer);
        if (cbRes != Result.Success) throw new InvalidOperationException($"vkAllocateCommandBuffers failed: {cbRes}");

        CommandBufferBeginInfo cbBegin = new() { SType = StructureType.CommandBufferBeginInfo };
        _vk.BeginCommandBuffer(_cmdBuffer, &cbBegin);
        _vk.CmdBindPipeline(_cmdBuffer, PipelineBindPoint.Compute, _pipeline);
        _vk.CmdBindDescriptorSets(_cmdBuffer, PipelineBindPoint.Compute, _pipeLayout, 0, 1, &ds, 0, null);
        _vk.CmdDispatch(_cmdBuffer, Workgroups, 1, 1);
        _vk.EndCommandBuffer(_cmdBuffer);

        // 11. Dedicated Compute Thread for continuous queue saturation
        using var computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var computeThread = new Thread(() =>
        {
            var cb = _cmdBuffer;
            SubmitInfo submit = new()
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &cb
            };

            while (!computeCts.Token.IsCancellationRequested)
            {
                if (!s_isBenchmarking)
                {
                    Thread.Sleep(15);
                    continue;
                }

                for (int p = 0; p < 16; p++)
                {
                    _vk.QueueSubmit(_computeQueue, 1, &submit, default);
                    s_totalDispatches++;
                }

                _vk.QueueWaitIdle(_computeQueue);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };
        computeThread.Start();

        // 12. UI Frontend Window (OpenGL 3.3 Core with Vulkan Theme)
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Render Test & Vulkan Compute Agent";
        winOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        winOptions.VSync = false;
        winOptions.WindowBorder = WindowBorder.Fixed;
        winOptions.IsVisible = true;

        using var window = Window.Create(winOptions);

        GL? gl = null;
        uint uiProgram = 0, vao = 0, vbo = 0, uiTexture = 0;
        uint[] uiPixels = new uint[WinWidth * WinHeight];
        var perfSw = Stopwatch.StartNew();
        ulong lastDispatches = 0;
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

            if (s_isBenchmarking) animTime += 0.02f;

            double elapsed = s_isBenchmarking ? s_benchTimer.Elapsed.TotalSeconds : 0.0;
            double tflops = s_isBenchmarking ? (s_currentDps * 1.048576 * 64.0) / 1000.0 : 0.0;

            string? hwSensor = s_perfQueryManager?.IsSupported == true ? s_perfQueryManager.FormattedCounterValue : null;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "Vulkan 1.0 Compute", devName, s_isBenchmarking, s_targetDurationSec,
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

            if (perfSw.ElapsedMilliseconds >= 250)
            {
                double sec = perfSw.Elapsed.TotalSeconds;
                s_currentDps = s_isBenchmarking ? (s_totalDispatches - lastDispatches) / sec : 0.0;
                lastDispatches = s_totalDispatches;
                perfSw.Restart();
            }

            if (!s_isBenchmarking) Thread.Sleep(16);
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
                _vk.DeviceWaitIdle(_device);

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

            if (_instance.Handle != 0)
            {
                _vk.DestroyInstance(_instance, null);
            }

            Console.WriteLine("[VulkanEngine] Vulkan context destroyed cleanly. Exit 0.");
        };

        window.Run();
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

    private static void ToggleBenchmark()
    {
        if (s_isBenchmarking) StopBenchmark();
        else StartBenchmark();
    }

    private static void StartBenchmark()
    {
        s_isBenchmarking = true;
        s_benchTimer.Restart();
        s_perfQueryManager?.AcquireLock();
        Console.WriteLine("[UI] >> BENCHMARK STARTED <<");
    }

    private static void StopBenchmark()
    {
        s_isBenchmarking = false;
        s_benchTimer.Stop();
        s_perfQueryManager?.ReleaseLock();
        if (_device.Handle != 0) _vk.QueueWaitIdle(_computeQueue);
        Console.WriteLine("[UI] >> BENCHMARK STOPPED <<");
    }
}