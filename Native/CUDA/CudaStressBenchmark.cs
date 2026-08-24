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

namespace GpuT.Agent.Native.CUDA;

public sealed unsafe class CudaStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550
    private const uint GridDimX = 4096;
    private const uint BlockDimX = 256; // 4096 * 256 = 1 048 576 параллельных CUDA потоков
    private const nuint BufferSize = GridDimX * BlockDimX * 16; // 16 MB VRAM

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
    private static nint s_dBuffer = nint.Zero;
    private static nint s_module = nint.Zero;
    private static nint s_function = nint.Zero;

    public static void Run(CancellationToken hostCt, int initialDuration = 0)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        // 1. Инициализация CUDA Driver API
        int initRes = CudaNative.cuInit(0);
        if (initRes != CudaNative.CUDA_SUCCESS)
            throw new InvalidOperationException($"cuInit failed ({initRes}). Is NVIDIA driver or ZLUDA installed?");

        int devCount = 0;
        CudaNative.cuDeviceGetCount(&devCount);
        if (devCount == 0) throw new InvalidOperationException("No CUDA-capable devices found.");

        int devHandle = 0;
        CudaNative.cuDeviceGet(&devHandle, 0);

        byte* pDevName = stackalloc byte[256];
        CudaNative.cuDeviceGetName(pDevName, 256, devHandle);
        string devName = Marshal.PtrToStringAnsi((nint)pDevName) ?? "NVIDIA CUDA Device";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[CUDAEngine] API Target:   NVIDIA CUDA Driver API");
        Console.WriteLine($"[CUDAEngine] CUDA Device:  {devName}");
        Console.ResetColor();

        // 2. Создание контекста и выделение VRAM
        nint ctx;
        int ctxRes = CudaNative.cuCtxCreate(&ctx, 0, devHandle);
        if (ctxRes != CudaNative.CUDA_SUCCESS) throw new InvalidOperationException($"cuCtxCreate failed: {ctxRes}");
        s_context = ctx;

        nint dptr;
        int memRes = CudaNative.cuMemAlloc(&dptr, BufferSize);
        if (memRes != CudaNative.CUDA_SUCCESS) throw new InvalidOperationException($"cuMemAlloc failed: {memRes}");
        s_dBuffer = dptr;

        // 3. Загрузка встроенного PTX-байткода ядра стресс-теста
        string ptxSource = GetStressPtxSource();
        s_module = LoadPtxModule(ptxSource);

        fixed (byte* pKernelName = "cuda_stress_kernel"u8)
        {
            nint func;
            int funcRes = CudaNative.cuModuleGetFunction(&func, s_module, pKernelName);
            if (funcRes != CudaNative.CUDA_SUCCESS) throw new InvalidOperationException($"cuModuleGetFunction failed: {funcRes}");
            s_function = func;
        }

        // 4. Запуск выделенного потока 100% вычислений CUDA
        using var computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var computeThread = new Thread(() =>
        {
            float timeVal = 0f;
            while (!computeCts.Token.IsCancellationRequested)
            {
                if (!s_isBenchmarking)
                {
                    Thread.Sleep(15);
                    continue;
                }

                timeVal += 0.05f;

                // Передача аргументов: (CUdeviceptr d_data, float time)
                nint bufPtr = s_dBuffer;
                float t = timeVal;
                void** kernelArgs = stackalloc void*[2];
                kernelArgs[0] = &bufPtr;
                kernelArgs[1] = &t;

                for (int p = 0; p < 16; p++)
                {
                    CudaNative.cuLaunchKernel(
                        s_function,
                        GridDimX, 1, 1,
                        BlockDimX, 1, 1,
                        0, nint.Zero,
                        kernelArgs, null);

                    s_totalDispatches++;
                }

                CudaNative.cuCtxSynchronize(); // Непрерывное 100% насыщение SM блоков
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };

        computeThread.Start();

        // 5. Окно UI в фирменной лаймово-зеленой палитре NVIDIA (ThemePalette.Cuda)
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Render Test & CUDA Driver Agent";
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
            if (hostCt.IsCancellationRequested)
            {
                window.Close();
                return;
            }

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
            // 1 048 576 потоков * 4 float * 256 FMA * 2 FLOPs ≈ 2.15 GFLOP на диспатч
            double tflops = s_isBenchmarking ? (s_currentDps * 2.15) / 1000.0 : 0.0;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "NVIDIA CUDA Driver", devName, s_isBenchmarking, s_targetDurationSec,
                    s_customInputBuffer, s_isCustomFocused,
                    elapsed, s_currentDps, tflops,
                    null,
                    ThemePalette.Cuda, // Зеленый стиль NVIDIA
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

            if (!s_isBenchmarking)
            {
                Thread.Sleep(16);
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

            if (s_context != nint.Zero)
            {
                CudaNative.cuCtxSynchronize();
                if (s_dBuffer != nint.Zero) CudaNative.cuMemFree(s_dBuffer);
                if (s_module != nint.Zero) CudaNative.cuModuleUnload(s_module);
                CudaNative.cuCtxDestroy(s_context);

                s_dBuffer = nint.Zero;
                s_module = nint.Zero;
                s_function = nint.Zero;
                s_context = nint.Zero;
            }

            Console.WriteLine("[CUDAEngine] CUDA Driver context destroyed. Exit 0.");
        };

        window.Run();
    }

    private static nint LoadPtxModule(string ptxSource)
    {
        byte* pPtx = (byte*)Marshal.StringToHGlobalAnsi(ptxSource);
        nint module;
        int res = CudaNative.cuModuleLoadData(&module, pPtx);
        Marshal.FreeHGlobal((nint)pPtx);

        if (res != CudaNative.CUDA_SUCCESS)
            throw new InvalidOperationException($"cuModuleLoadData failed ({res})");

        return module;
    }

    // Универсальный ассемблерный PTX-код (совместим с SM 5.0+ и ZLUDA)
    private static string GetStressPtxSource() => @"
.version 6.0
.target sm_50
.address_size 64

.visible .entry cuda_stress_kernel(
    .param .u64 d_data,
    .param .f32 time
)
{
    .reg .pred %p<2>;
    .reg .b32 %r<8>;
    .reg .b64 %rd<8>;
    .reg .f32 %f<32>;

    ld.param.u64 %rd1, [d_data];
    ld.param.f32 %f1, [time];

    mov.u32 %r1, %ctaid.x;
    mov.u32 %r2, %ntid.x;
    mov.u32 %r3, %tid.x;
    mad.lo.s32 %r4, %r1, %r2, %r3;

    mul.wide.s32 %rd2, %r4, 16;
    add.s64 %rd3, %rd1, %rd2;

    ld.global.v4.f32 {%f2, %f3, %f4, %f5}, [%rd3];

    mov.u32 %r5, 0;
$loop_start:
    fma.rn.f32 %f2, %f2, %f1, 0f3F800000;
    fma.rn.f32 %f3, %f3, %f1, 0f3F800000;
    fma.rn.f32 %f4, %f4, %f1, 0f3F800000;
    fma.rn.f32 %f5, %f5, %f1, 0f3F800000;

    sin.approx.f32 %f2, %f2;
    cos.approx.f32 %f3, %f3;
    sin.approx.f32 %f4, %f4;
    cos.approx.f32 %f5, %f5;

    add.u32 %r5, %r5, 1;
    setp.lt.u32 %p1, %r5, 256;
    @%p1 bra $loop_start;

    st.global.v4.f32 [%rd3], {%f2, %f3, %f4, %f5};
    ret;
}
";

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
        else if (PixelUiEngine.Btn10s.Contains(x, y))
        {
            s_targetDurationSec = 10;
            s_customInputBuffer = "10";
            s_isCustomFocused = false;
        }
        else if (PixelUiEngine.Btn30s.Contains(x, y))
        {
            s_targetDurationSec = 30;
            s_customInputBuffer = "30";
            s_isCustomFocused = false;
        }
        else if (PixelUiEngine.Btn60s.Contains(x, y))
        {
            s_targetDurationSec = 60;
            s_customInputBuffer = "60";
            s_isCustomFocused = false;
        }
        else if (PixelUiEngine.BtnUnlimited.Contains(x, y))
        {
            s_targetDurationSec = 0;
            s_customInputBuffer = "";
            s_isCustomFocused = false;
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
        Console.WriteLine("[UI] >> BENCHMARK STARTED <<");
    }

    private static void StopBenchmark()
    {
        s_isBenchmarking = false;
        s_benchTimer.Stop();
        Console.WriteLine("[UI] >> BENCHMARK STOPPED <<");
    }
}