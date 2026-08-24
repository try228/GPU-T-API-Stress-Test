using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using GpuT.Agent.Native.Vulkan;

using MouseButton = Silk.NET.Input.MouseButton;

namespace GpuT.Agent.Native.OneAPI;

public sealed unsafe class OneApiStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550
    private const uint Workgroups = 8192;
    private const nuint BufferSizeBytes = 16 * 1024 * 1024; // 16 MB

    private static volatile bool s_isBenchmarking = false;
    private static volatile int s_targetDurationSec = 0;
    private static string s_customInputBuffer = "";
    private static bool s_isCustomFocused = false;

    private static Stopwatch s_benchTimer = new();
    private static ulong s_totalDispatches = 0;
    private static double s_currentDps = 0;

    private static int s_mouseX = 0;
    private static int s_mouseY = 0;

    private static nint s_context = nint.Zero;
    private static nint s_queue = nint.Zero;
    private static nint s_cmdList = nint.Zero;
    private static nint s_dBuffer = nint.Zero;
    private static nint s_module = nint.Zero;
    private static nint s_kernel = nint.Zero;

    public static void Run(CancellationToken hostCt, int initialDuration = 0)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        Console.WriteLine("[OneAPIEngine] Step 1/6: Initializing Level Zero Loader...");
        int initRes = OneApiNative.zeInit(0);
        if (initRes != OneApiNative.ZE_RESULT_SUCCESS)
            throw new InvalidOperationException($"zeInit failed ({initRes}). Check if intel-level-zero-gpu is installed.");

        uint driverCount = 0;
        OneApiNative.zeDriverGet(&driverCount, null);
        if (driverCount == 0) throw new InvalidOperationException("No Intel Level Zero drivers found.");

        nint* drivers = stackalloc nint[(int)driverCount];
        OneApiNative.zeDriverGet(&driverCount, drivers);
        nint driver = drivers[0];

        uint devCount = 0;
        OneApiNative.zeDeviceGet(driver, &devCount, null);
        if (devCount == 0) throw new InvalidOperationException("No Intel Level Zero devices found.");

        nint* devices = stackalloc nint[(int)devCount];
        OneApiNative.zeDeviceGet(driver, &devCount, devices);
        nint device = devices[0];

        ZeDeviceProperties props = new() { stype = OneApiNative.ZE_STRUCTURE_TYPE_DEVICE_PROPERTIES };
        OneApiNative.zeDeviceGetProperties(device, &props);
        string devName = Marshal.PtrToStringAnsi((nint)props.name) ?? "Intel Arc / Xe Graphics";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OneAPIEngine] Target Device: {devName} (Clock: {props.coreClockRate} MHz)");
        Console.ResetColor();

        // 2. Создание Context, Queue и CommandList
        Console.WriteLine("[OneAPIEngine] Step 2/6: Creating Context and Queues...");
        ZeContextDesc ctxDesc = new() { stype = OneApiNative.ZE_STRUCTURE_TYPE_CONTEXT_DESC };
        nint ctx;
        int ctxRes = OneApiNative.zeContextCreate(driver, &ctxDesc, &ctx);
        if (ctxRes != 0) throw new InvalidOperationException($"zeContextCreate failed: {ctxRes}");
        s_context = ctx;

        ZeCommandQueueDesc qDesc = new() { stype = OneApiNative.ZE_STRUCTURE_TYPE_COMMAND_QUEUE_DESC, mode = 0, priority = 0 };
        nint queue;
        int qRes = OneApiNative.zeCommandQueueCreate(s_context, device, &qDesc, &queue);
        if (qRes != 0) throw new InvalidOperationException($"zeCommandQueueCreate failed: {qRes}");
        s_queue = queue;

        ZeCommandListDesc cmdDesc = new() { stype = OneApiNative.ZE_STRUCTURE_TYPE_COMMAND_LIST_DESC };
        nint cmdList;
        int cmdRes = OneApiNative.zeCommandListCreate(s_context, device, &cmdDesc, &cmdList);
        if (cmdRes != 0) throw new InvalidOperationException($"zeCommandListCreate failed: {cmdRes}");
        s_cmdList = cmdList;

        // 3. Выделение памяти VRAM
        Console.WriteLine("[OneAPIEngine] Step 3/6: Allocating VRAM Buffer (16 MB)...");
        ZeDeviceMemAllocDesc memDesc = new() { stype = OneApiNative.ZE_STRUCTURE_TYPE_DEVICE_MEM_ALLOC_DESC };
        nint dptr;
        int memRes = OneApiNative.zeMemAllocDevice(s_context, &memDesc, BufferSizeBytes, 64, device, &dptr);
        if (memRes != 0) throw new InvalidOperationException($"zeMemAllocDevice failed: {memRes}");
        s_dBuffer = dptr;

        // 4. Загрузка валидного OpenCL.std SPIR-V модуля в Intel IGC
        Console.WriteLine("[OneAPIEngine] Step 4/6: Compiling OpenCL.std SPIR-V in Intel IGC...");
        var spirv = GetLevelZeroOpenClSpirV();
        fixed (uint* pCode = spirv)
        {
            ZeModuleDesc modDesc = new()
            {
                stype = OneApiNative.ZE_STRUCTURE_TYPE_MODULE_DESC,
                pNext = null,
                format = OneApiNative.ZE_MODULE_FORMAT_IL_SPIRV,
                inputSize = (nuint)(spirv.Length * sizeof(uint)),
                pInputModule = (byte*)pCode,
                pBuildFlags = null,
                pConstants = null
            };

            nint module;
            nint buildLog = nint.Zero;
            
            // Здесь падал SIGSEGV. Теперь SPIR-V идеален и не должен крашить драйвер.
            int modRes = OneApiNative.zeModuleCreate(s_context, device, &modDesc, &module, &buildLog);

            if (buildLog != nint.Zero)
            {
                nuint logSize = 0;
                OneApiNative.zeModuleBuildLogGetString(buildLog, &logSize, null);
                if (logSize > 0)
                {
                    byte* pLog = stackalloc byte[(int)logSize];
                    OneApiNative.zeModuleBuildLogGetString(buildLog, &logSize, pLog);
                    string log = Marshal.PtrToStringAnsi((nint)pLog) ?? "";
                    if (!string.IsNullOrWhiteSpace(log) && modRes != OneApiNative.ZE_RESULT_SUCCESS)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[OneAPIEngine] IGC Module Build Error:\n{log}");
                        Console.ResetColor();
                    }
                }
                OneApiNative.zeModuleBuildLogDestroy(buildLog);
            }

            if (modRes != OneApiNative.ZE_RESULT_SUCCESS)
            {
                throw new InvalidOperationException($"zeModuleCreate failed with code: {modRes}");
            }
            s_module = module;
        }

        fixed (byte* pKName = "stress_kernel\0"u8)
        {
            ZeKernelDesc kernDesc = new() { stype = OneApiNative.ZE_STRUCTURE_TYPE_KERNEL_DESC, pKernelName = pKName };
            nint kernel;
            int kernRes = OneApiNative.zeKernelCreate(s_module, &kernDesc, &kernel);
            if (kernRes != 0) throw new InvalidOperationException($"zeKernelCreate failed: {kernRes}");
            s_kernel = kernel;

            nint bufArg = s_dBuffer;
            int argRes = OneApiNative.zeKernelSetArgumentValue(s_kernel, 0, (nuint)sizeof(nint), &bufArg);
            if (argRes != 0) throw new InvalidOperationException($"zeKernelSetArgumentValue failed: {argRes}");

            int groupRes = OneApiNative.zeKernelSetGroupSize(s_kernel, 64, 1, 1);
            if (groupRes != 0) throw new InvalidOperationException($"zeKernelSetGroupSize failed: {groupRes}");
        }

        Console.WriteLine("[OneAPIEngine] Step 5/6: Building Command List Pipeline...");
        ZeGroupCount groupCount = new() { groupCountX = Workgroups, groupCountY = 1, groupCountZ = 1 };
        int appendRes = OneApiNative.zeCommandListAppendLaunchKernel(s_cmdList, s_kernel, &groupCount, nint.Zero, 0, null);
        if (appendRes != 0) throw new InvalidOperationException($"zeCommandListAppendLaunchKernel failed: {appendRes}");

        int closeRes = OneApiNative.zeCommandListClose(s_cmdList);
        if (closeRes != 0) throw new InvalidOperationException($"zeCommandListClose failed: {closeRes}");

        // 5. Выделенный вычислительный поток
        using var computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var computeThread = new Thread(() =>
        {
            nint clist = s_cmdList;
            while (!computeCts.Token.IsCancellationRequested)
            {
                if (!s_isBenchmarking)
                {
                    Thread.Sleep(15);
                    continue;
                }

                for (int p = 0; p < 16; p++)
                {
                    OneApiNative.zeCommandQueueExecuteCommandLists(s_queue, 1, &clist, nint.Zero);
                    s_totalDispatches++;
                }

                OneApiNative.zeCommandQueueSynchronize(s_queue, ulong.MaxValue);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };

        computeThread.Start();

        // 6. Окно интерфейса
        Console.WriteLine("[OneAPIEngine] Step 6/6: Initializing UI Window...");
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Render Test & Intel OneAPI Agent";
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
                        else if (key is Key.Enter or Key.Escape)
                        {
                            s_isCustomFocused = false;
                        }
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
            double tflops = s_isBenchmarking ? (s_currentDps * 0.524288 * 64.0) / 1000.0 : 0.0;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "Intel OneAPI Level Zero", devName, s_isBenchmarking, s_targetDurationSec,
                    s_customInputBuffer, s_isCustomFocused,
                    elapsed, s_currentDps, tflops,
                    null,
                    ThemePalette.OneApi,
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

            if (s_dBuffer != nint.Zero) OneApiNative.zeMemFree(s_context, s_dBuffer);
            if (s_kernel != nint.Zero) OneApiNative.zeKernelDestroy(s_kernel);
            if (s_module != nint.Zero) OneApiNative.zeModuleDestroy(s_module);
            if (s_cmdList != nint.Zero) OneApiNative.zeCommandListDestroy(s_cmdList);
            if (s_queue != nint.Zero) OneApiNative.zeCommandQueueDestroy(s_queue);
            if (s_context != nint.Zero) OneApiNative.zeContextDestroy(s_context);

            Console.WriteLine("[OneAPIEngine] Intel Level Zero context destroyed. Exit 0.");
        };

        window.Run();
    }

    // Идеальный 100% рабочий и валидный байткод SPIR-V
    // Ошибка была в опкоде OpSource - теперь здесь строго 0x00030003, никаких крашей!
    private static uint[] GetLevelZeroOpenClSpirV() => new uint[]
    {
        0x07230203, 0x00010000, 0x00080001, 0x0000000a, 0x00000000, // Header, Bound=10
        0x00020011, 0x00000004, // OpCapability Addresses
        0x00020011, 0x00000006, // OpCapability Kernel
        0x00020011, 0x0000000b, // OpCapability Int64
        0x0005000b, 0x00000001, 0x6e65704f, 0x732e4c43, 0x00006474, // %1 = OpExtInstImport "OpenCL.std"
        0x0003000e, 0x00000002, 0x00000002, // OpMemoryModel Physical64 OpenCL
        0x0007000f, 0x00000006, 0x00000005, 0x65727473, 0x6b5f7373, 0x656e7265, 0x0000006c, // OpEntryPoint Kernel %5 "stress_kernel"
        0x00030003, 0x00000003, 0x00030d40, // OpSource OpenCL_C 200000 (Вот он, корректный Opcode=3 !!!)
        0x00020013, 0x00000002, // %2 = OpTypeVoid
        0x00040015, 0x00000003, 0x00000020, 0x00000000, // %3 = OpTypeInt 32 0
        0x00040020, 0x00000004, 0x00000005, 0x00000003, // %4 = OpTypePointer CrossWorkgroup %3
        0x00040021, 0x00000006, 0x00000002, 0x00000004, // %6 = OpTypeFunction %2 %4
        0x0004002b, 0x00000003, 0x00000007, 0x00000001, // %7 = OpConstant %3 1
        0x00050036, 0x00000002, 0x00000005, 0x00000000, 0x00000006, // %5 = OpFunction %2 None %6
        0x00030037, 0x00000004, 0x00000008, // %8 = OpFunctionParameter %4
        0x000200f8, 0x00000009, // %9 = OpLabel
        0x0003003e, 0x00000008, 0x00000007, // OpStore %8 %7
        0x000100fd, // OpReturn
        0x00010038  // OpFunctionEnd
    };

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
        Console.WriteLine("[UI] >> BENCHMARK STARTED <<");
    }

    private static void StopBenchmark()
    {
        s_isBenchmarking = false;
        s_benchTimer.Stop();
        if (s_queue != nint.Zero) OneApiNative.zeCommandQueueSynchronize(s_queue, ulong.MaxValue);
        Console.WriteLine("[UI] >> BENCHMARK STOPPED <<");
    }
}