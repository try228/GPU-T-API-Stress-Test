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

namespace GpuT.Agent.Native.OpenCL;

public sealed unsafe class OpenClStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550
    private const nuint GlobalThreads = 1048576; // 1 048 576 float4 потоков (~4.2M float значений)
    private const nuint LocalThreads = 256;      // Оптимальный размер воркгруппы для NVIDIA (8 варпов) / AMD / Intel
    private const nuint BufferSize = GlobalThreads * 16; // 16 MB VRAM

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
    private static nint s_clBuffer = nint.Zero;
    private static nint s_program = nint.Zero;
    private static nint s_kernel = nint.Zero;

    public static void Run(CancellationToken hostCt, int initialDuration = 0, bool isRusticl = false)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        string apiTitle = isRusticl ? "Rusticl (Mesa Rust OpenCL)" : "OpenCL 1.2+ Compute";
        var theme = isRusticl ? ThemePalette.Rusticl : ThemePalette.OpenCL;

        // 1. Поиск лучшей видеокарты (NVIDIA / AMD / Intel dGPU)
        (nint platform, nint device, string devName, string platformName) = SelectBestOpenClDevice(isRusticl);
        
        if (platform == nint.Zero || device == nint.Zero)
            throw new InvalidOperationException($"No suitable OpenCL platform/device found for target: {apiTitle}");

        string devVendor = GetClDeviceInfo(device, OpenClNative.CL_DEVICE_VENDOR);
        string drvVersion = GetClDeviceInfo(device, OpenClNative.CL_DRIVER_VERSION);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OpenCLEngine] API Target:        {apiTitle}");
        Console.WriteLine($"[OpenCLEngine] Selected Platform: {platformName}");
        Console.WriteLine($"[OpenCLEngine] Selected Device:   {devName} ({devVendor})");
        Console.WriteLine($"[OpenCLEngine] Driver Version:    {drvVersion}");
        Console.ResetColor();

        // 2. Создание Context и Queue
        int err = 0;
        s_context = OpenClNative.clCreateContext(null, 1, &device, null, null, &err);
        if (err != 0) throw new InvalidOperationException($"clCreateContext failed: {err}");

        s_queue = OpenClNative.clCreateCommandQueue(s_context, device, 0, &err);
        if (err != 0) throw new InvalidOperationException($"clCreateCommandQueue failed: {err}");

        // 3. Выделение буфера VRAM (16 MB)
        s_clBuffer = OpenClNative.clCreateBuffer(s_context, OpenClNative.CL_MEM_READ_WRITE, BufferSize, null, &err);
        if (err != 0) throw new InvalidOperationException($"clCreateBuffer failed: {err}");

        // 4. Компиляция высоконагруженного OpenCL C ядра (256 FMA)
        string clSource = GetStressKernelSource();
        s_program = BuildClProgram(s_context, device, clSource);

        fixed (byte* pKernelName = "stress_kernel"u8)
        {
            s_kernel = OpenClNative.clCreateKernel(s_program, pKernelName, &err);
            if (err != 0) throw new InvalidOperationException($"clCreateKernel failed: {err}");

            fixed (nint* pBuf = &s_clBuffer)
            {
                OpenClNative.clSetKernelArg(s_kernel, 0, (nuint)sizeof(nint), pBuf);
            }
        }

        // 5. Запуск выделенного фонового потока 100% вычислений
        using var computeCts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        var computeThread = new Thread(() =>
        {
            float timeVal = 0f;
            nuint gws = GlobalThreads;
            nuint lws = LocalThreads;

            while (!computeCts.Token.IsCancellationRequested)
            {
                if (!s_isBenchmarking)
                {
                    Thread.Sleep(15);
                    continue;
                }

                timeVal += 0.05f;
                OpenClNative.clSetKernelArg(s_kernel, 1, (nuint)sizeof(float), &timeVal);

                // Забиваем аппаратную очередь непрерывными пакетами без пауз
                for (int p = 0; p < 16; p++)
                {
                    OpenClNative.clEnqueueNDRangeKernel(s_queue, s_kernel, 1, null, &gws, &lws, 0, null, null);
                    s_totalDispatches++;
                }
                OpenClNative.clFlush(s_queue);
                OpenClNative.clFinish(s_queue); // Синхронизация пачки для непрерывной 100% загрузки
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };

        computeThread.Start();

        // 6. Создание окна UI
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = $"GPU-T Render Test & {apiTitle} Stress Agent";
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
                    apiTitle, devName, s_isBenchmarking, s_targetDurationSec,
                    s_customInputBuffer, s_isCustomFocused,
                    elapsed, s_currentDps, tflops,
                    null,
                    theme,
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

            if (s_queue != nint.Zero)
            {
                OpenClNative.clFinish(s_queue);
                OpenClNative.clReleaseKernel(s_kernel);
                OpenClNative.clReleaseProgram(s_program);
                OpenClNative.clReleaseMemObject(s_clBuffer);
                OpenClNative.clReleaseCommandQueue(s_queue);
                OpenClNative.clReleaseContext(s_context);

                s_queue = nint.Zero;
                s_kernel = nint.Zero;
                s_program = nint.Zero;
                s_clBuffer = nint.Zero;
                s_context = nint.Zero;
            }

            Console.WriteLine($"[OpenCLEngine] {apiTitle} context released. Exit 0.");
        };

        window.Run();
    }

    private static (nint Platform, nint Device, string DevName, string PlatformName) SelectBestOpenClDevice(bool isRusticl)
    {
        uint numPlatforms = 0;
        OpenClNative.clGetPlatformIDs(0, null, &numPlatforms);
        if (numPlatforms == 0) return (nint.Zero, nint.Zero, "", "");

        nint* platforms = stackalloc nint[(int)numPlatforms];
        OpenClNative.clGetPlatformIDs(numPlatforms, platforms, null);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[OpenCL Discovery] Found {numPlatforms} platform(s):");

        nint bestPlatform = nint.Zero;
        nint bestDevice = nint.Zero;
        string bestDevName = "";
        string bestPlatformName = "";
        int highestScore = -1;

        for (uint i = 0; i < numPlatforms; i++)
        {
            string pName = GetClPlatformInfo(platforms[i], OpenClNative.CL_PLATFORM_NAME);
            string pVendor = GetClPlatformInfo(platforms[i], OpenClNative.CL_PLATFORM_VENDOR);
            Console.WriteLine($"  [{i}] Platform: \"{pName}\" (Vendor: \"{pVendor}\")");

            uint devCount = 0;
            OpenClNative.clGetDeviceIDs(platforms[i], OpenClNative.CL_DEVICE_TYPE_ALL, 0, null, &devCount);
            if (devCount == 0) continue;

            nint* devices = stackalloc nint[(int)devCount];
            OpenClNative.clGetDeviceIDs(platforms[i], OpenClNative.CL_DEVICE_TYPE_ALL, devCount, devices, null);

            for (uint d = 0; d < devCount; d++)
            {
                string dName = GetClDeviceInfo(devices[d], OpenClNative.CL_DEVICE_NAME);
                string dVendor = GetClDeviceInfo(devices[d], OpenClNative.CL_DEVICE_VENDOR);
                Console.WriteLine($"       -> Device #{d}: \"{dName}\" (Vendor: \"{dVendor}\")");

                int score = CalculateDeviceScore(pName, pVendor, dName, dVendor, isRusticl);

                if (score > highestScore)
                {
                    highestScore = score;
                    bestPlatform = platforms[i];
                    bestDevice = devices[d];
                    bestDevName = dName;
                    bestPlatformName = pName;
                }
            }
        }
        Console.ResetColor();

        return (bestPlatform, bestDevice, bestDevName, bestPlatformName);
    }

    private static int CalculateDeviceScore(string pName, string pVendor, string dName, string dVendor, bool isRusticl)
    {
        string fullInfo = $"{pName} {pVendor} {dName} {dVendor}".ToLowerInvariant();

        if (isRusticl)
        {
            // Если запрошен режим Rusticl, платформы Mesa Rusticl получают максимальный приоритет
            if (pName.Contains("rusticl", StringComparison.OrdinalIgnoreCase))
            {
                int score = 2000;
                if (fullInfo.Contains("radeon") || fullInfo.Contains("amd")) score += 300;
                if (fullInfo.Contains("nvidia") || fullInfo.Contains("geforce") || fullInfo.Contains("rtx")) score += 300;
                if (fullInfo.Contains("intel") || fullInfo.Contains("arc")) score += 100;
                return score;
            }
            return 10; // Не Rusticl платформы получают минимальный вес
        }

        // Обычный режим (приоритет официальным дискретным GPU NVIDIA / AMD / Intel)
        int baseScore = 0;

        // 1. NVIDIA (CUDA платформы и GPU)
        if (fullInfo.Contains("nvidia") || fullInfo.Contains("cuda") || fullInfo.Contains("geforce") || fullInfo.Contains("rtx") || fullInfo.Contains("quadro") || fullInfo.Contains("tesla"))
        {
            baseScore += 1000;
        }
        // 2. AMD (ROCm / PAL / Radeon)
        else if (fullInfo.Contains("amd") || fullInfo.Contains("advanced micro devices") || fullInfo.Contains("radeon"))
        {
            baseScore += 950;
        }
        // 3. Intel Arc / Дискретная графика
        else if (fullInfo.Contains("arc") || fullInfo.Contains("iris") || fullInfo.Contains("intel"))
        {
            baseScore += 700;
        }
        // 4. Fallback для других GPU
        else
        {
            baseScore += 300;
        }

        // Штраф для чисто процессорных / софтверных эмуляций
        if (fullInfo.Contains("cpu") || fullInfo.Contains("pocl") || fullInfo.Contains("llvmpipe") || fullInfo.Contains("portable computing language"))
        {
            baseScore -= 600;
        }

        return Math.Max(1, baseScore);
    }

    private static string GetClPlatformInfo(nint platform, uint param)
    {
        nuint size = 0;
        OpenClNative.clGetPlatformInfo(platform, param, 0, null, &size);
        if (size == 0) return "";
        byte* buffer = stackalloc byte[(int)size];
        OpenClNative.clGetPlatformInfo(platform, param, size, buffer, null);
        return Marshal.PtrToStringUTF8((nint)buffer) ?? Marshal.PtrToStringAnsi((nint)buffer) ?? "";
    }

    private static string GetClDeviceInfo(nint device, uint param)
    {
        nuint size = 0;
        OpenClNative.clGetDeviceInfo(device, param, 0, null, &size);
        if (size == 0) return "";
        byte* buffer = stackalloc byte[(int)size];
        OpenClNative.clGetDeviceInfo(device, param, size, buffer, null);
        return Marshal.PtrToStringUTF8((nint)buffer) ?? Marshal.PtrToStringAnsi((nint)buffer) ?? "";
    }

    private static nint BuildClProgram(nint context, nint device, string source)
    {
        int err = 0;
        byte* pSrc = (byte*)Marshal.StringToHGlobalAnsi(source);
        nuint len = (nuint)source.Length;

        nint program = OpenClNative.clCreateProgramWithSource(context, 1, &pSrc, &len, &err);
        Marshal.FreeHGlobal((nint)pSrc);
        if (err != 0) throw new InvalidOperationException($"clCreateProgramWithSource failed: {err}");

        // Оптимизирующие флаги для мгновенной сборки Clang/LLVM в Rusticl, NVIDIA CUDA и AMD ROCm
        fixed (byte* pOpts = "-cl-fast-relaxed-math -cl-mad-enable"u8)
        {
            int buildRes = OpenClNative.clBuildProgram(program, 1, &device, pOpts, null, null);
            if (buildRes != 0)
            {
                nuint logSize = 0;
                OpenClNative.clGetProgramBuildInfo(program, device, OpenClNative.CL_PROGRAM_BUILD_LOG, 0, null, &logSize);
                byte* pLog = stackalloc byte[(int)logSize];
                OpenClNative.clGetProgramBuildInfo(program, device, OpenClNative.CL_PROGRAM_BUILD_LOG, logSize, pLog, null);
                string log = Marshal.PtrToStringUTF8((nint)pLog) ?? Marshal.PtrToStringAnsi((nint)pLog) ?? "";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[OpenCLEngine] OpenCL C Build Log:\n{log}");
                Console.ResetColor();
                throw new InvalidOperationException($"OpenCL Kernel Build Error ({buildRes})");
            }
        }

        return program;
    }

    // 256 шагов тяжелой SIMD-математики (FMA) на каждый поток
    private static string GetStressKernelSource() => @"
__kernel void stress_kernel(__global float4* data, float time) {
    int gid = get_global_id(0);
    float4 v = data[gid];
    float4 t = (float4)(sin(time + (float)gid * 0.0001f), cos(time), sin(time * 0.35f), cos(time * 1.25f));
    float4 c1 = (float4)(0.12f, 0.25f, 0.38f, 0.51f);
    float4 c2 = (float4)(0.51f, 0.38f, 0.25f, 0.12f);
    float4 scale = (float4)(0.999f, 0.999f, 0.999f, 0.999f);
    float4 eps = (float4)(0.001f, 0.001f, 0.001f, 0.001f);
    
    for (int i = 0; i < 256; ++i) {
        v = fma(v, t, sin(v * 1.35f + c1));
        t = fma(t, scale, cos(v * 1.85f - c2));
        v = v * v * 0.25f + eps;
    }
    
    data[gid] = v;
}";

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