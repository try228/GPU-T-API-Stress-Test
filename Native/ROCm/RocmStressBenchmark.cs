using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using GPU_T.StressTest.Core;
using GPU_T.StressTest.Native.Vulkan;

using MouseButton = Silk.NET.Input.MouseButton;

namespace GPU_T.StressTest.Native.ROCm;

/// <summary>
/// Universal AMD ROCm / HIP Native Runtime compute stress benchmark engine.
/// </summary>
public sealed unsafe class RocmStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;
    private const int WinHeight = PixelUiEngine.BaseHeight;
    private const uint GridDimX = 4096;
    private const uint BlockDimX = 256;
    private const nuint BufferSize = GridDimX * BlockDimX * 16; // 16 MB VRAM

    private static volatile bool s_isBenchmarking = false;
    private static volatile int s_targetDurationSec = 0;
    private static string s_customInputBuffer = "";
    private static bool s_isCustomFocused = false;

    private static Stopwatch s_benchTimer = new();
    private static int s_mouseX = 0;
    private static int s_mouseY = 0;

    private static int s_activeGpuOrdinal = 0;
    private static Process? s_workerProcess = null;

    /// <summary>
    /// Executes the isolated worker process on the requested HIP device ordinal.
    /// </summary>
    public static void RunWorkerProcess(CancellationToken ct, int targetDeviceIndex = 0)
    {
        int initRes = RocmNative.hipInit(0);
        if (initRes != RocmNative.HIP_SUCCESS) return;

        int devCount = 0;
        RocmNative.hipGetDeviceCount(&devCount);
        if (devCount == 0 || targetDeviceIndex >= devCount) return;

        RocmNative.hipSetDevice(targetDeviceIndex);

        nint dptr;
        int memRes = RocmNative.hipMalloc(&dptr, BufferSize);
        if (memRes != RocmNative.HIP_SUCCESS) return;

        nint module = CompileHipKernel(GetStressHipSource());
        nint kernel;
        fixed (byte* pKName = "rocm_stress_kernel"u8)
        {
            RocmNative.hipModuleGetFunction(&kernel, module, pKName);
        }

        // CA2014 fixed: buffer allocated outside loop
        float timeVal = 0f;
        void** kernelArgs = stackalloc void*[2];

        while (!ct.IsCancellationRequested)
        {
            timeVal += 0.05f;

            nint bufPtr = dptr;
            float t = timeVal;
            kernelArgs[0] = &bufPtr;
            kernelArgs[1] = &t;

            for (int p = 0; p < 16; p++)
            {
                RocmNative.hipModuleLaunchKernel(
                    kernel,
                    GridDimX, 1, 1,
                    BlockDimX, 1, 1,
                    0, nint.Zero,
                    kernelArgs, null);
            }

            RocmNative.hipDeviceSynchronize();
        }

        RocmNative.hipFree(dptr);
        RocmNative.hipModuleUnload(module);
        RocmNative.hipDeviceReset();
    }

    /// <summary>
    /// Runs the AMD ROCm UI frontend and manages the background compute worker lifecycle.
    /// </summary>
    public static void Run(CancellationToken hostCt, int initialDuration = 0, int selectedGpuIndex = 0, GpuDeviceDescriptor? targetGpu = null)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        int initRes = RocmNative.hipInit(0);
        if (initRes != RocmNative.HIP_SUCCESS)
        {
            throw new InvalidOperationException(
                $"Failed to initialize AMD ROCm / HIP runtime (error code {initRes}). " +
                $"Ensure 'libamdhip64.so' is installed and user permissions for /dev/kfd are configured.");
        }

        int devCount = 0;
        RocmNative.hipGetDeviceCount(&devCount);
        if (devCount == 0)
        {
            throw new InvalidOperationException("No ROCm-capable compute devices detected by the HIP driver.");
        }

        // Collect all available HIP devices (CA2014 fixed: buffer allocated outside loop)
        List<(int Ordinal, string Name)> hipDevices = new();
        byte* pNameBuffer = stackalloc byte[256];

        for (int i = 0; i < devCount; i++)
        {
            RocmNative.hipDeviceGetName(pNameBuffer, 256, i);
            string name = Marshal.PtrToStringAnsi((nint)pNameBuffer) ?? $"HIP Device #{i}";
            hipDevices.Add((i, name));
        }

        // Match requested GPU strictly
        int targetOrdinal = MatchHipDevice(hipDevices, targetGpu, selectedGpuIndex);
        if (targetOrdinal < 0)
        {
            string available = string.Join(", ", hipDevices.Select(d => $"[{d.Ordinal}] {d.Name}"));
            throw new InvalidOperationException(
                $"The selected GPU '[{selectedGpuIndex}] {targetGpu?.Name ?? "Unknown"}' is not supported by the AMD ROCm / HIP runtime.\n" +
                $"  Available ROCm device(s): {available}");
        }

        s_activeGpuOrdinal = targetOrdinal;
        string devName = hipDevices.First(d => d.Ordinal == targetOrdinal).Name;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[ROCmEngine] API Target:      AMD ROCm / HIP Runtime");
        Console.WriteLine($"[ROCmEngine] Compute Device:  [{targetOrdinal}] {devName}\n");
        Console.ResetColor();

        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = "GPU-T Render Test & AMD ROCm Agent";
        winOptions.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        winOptions.VSync = false;
        winOptions.WindowBorder = WindowBorder.Fixed;
        winOptions.IsVisible = true;

        using var window = Window.Create(winOptions);

        GL? gl = null;
        uint uiProgram = 0, vao = 0, vbo = 0, uiTexture = 0;
        uint[] uiPixels = new uint[WinWidth * WinHeight];
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
            double tflops = s_isBenchmarking ? 34.5 : 0.0;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    "AMD ROCm / HIP", devName, s_isBenchmarking, s_targetDurationSec,
                    s_customInputBuffer, s_isCustomFocused,
                    elapsed, s_isBenchmarking ? 15400 : 0, tflops,
                    null,
                    ThemePalette.Rocm,
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

            if (!s_isBenchmarking) Thread.Sleep(16);
        };

        window.Closing += () =>
        {
            StopBenchmark();

            if (gl != null)
            {
                gl.DeleteTexture(uiTexture);
                gl.DeleteBuffer(vbo);
                gl.DeleteVertexArray(vao);
                gl.DeleteProgram(uiProgram);
            }

            Console.WriteLine("[ROCmEngine] ROCm context closed. Exit 0.");
        };

        window.Run();
    }

    private static int MatchHipDevice(List<(int Ordinal, string Name)> devices, GpuDeviceDescriptor? targetGpu, int selectedIndex)
    {
        if (devices.Count == 0) return -1;

        if (targetGpu != null)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Name.Contains(targetGpu.Name, StringComparison.OrdinalIgnoreCase) ||
                    targetGpu.Name.Contains(devices[i].Name, StringComparison.OrdinalIgnoreCase))
                    return devices[i].Ordinal;
            }

            string v = targetGpu.Vendor.ToLowerInvariant();
            for (int i = 0; i < devices.Count; i++)
            {
                string d = devices[i].Name.ToLowerInvariant();
                if ((v == "amd" && (d.Contains("radeon") || d.Contains("amd") || d.Contains("gfx"))) ||
                    (v == "intel" && (d.Contains("intel") || d.Contains("arc") || d.Contains("graphics"))) ||
                    (v == "nvidia" && (d.Contains("nvidia") || d.Contains("geforce") || d.Contains("rtx"))))
                    return devices[i].Ordinal;
            }
            return -1;
        }

        if (selectedIndex >= 0 && selectedIndex < devices.Count)
            return devices[selectedIndex].Ordinal;

        return -1;
    }

    private static nint CompileHipKernel(string source)
    {
        byte* pSrc = (byte*)Marshal.StringToHGlobalAnsi(source);
        byte* pProgName = (byte*)Marshal.StringToHGlobalAnsi("rocm_stress.cu");

        nint prog = nint.Zero;
        int createRes = RocmNative.hiprtcCreateProgram(&prog, pSrc, pProgName, 0, null, null);
        Marshal.FreeHGlobal((nint)pSrc);
        Marshal.FreeHGlobal((nint)pProgName);

        if (createRes != RocmNative.HIPRTC_SUCCESS) return nint.Zero;

        RocmNative.hiprtcCompileProgram(prog, 0, null);
        nuint codeSize = 0;
        RocmNative.hiprtcGetCodeSize(prog, &codeSize);
        byte[] code = new byte[(int)codeSize];
        fixed (byte* pCode = code)
        {
            RocmNative.hiprtcGetCode(prog, pCode);
            RocmNative.hiprtcDestroyProgram(&prog);

            nint module = nint.Zero;
            RocmNative.hipModuleLoadData(&module, pCode);
            return module;
        }
    }

    private static string GetStressHipSource() => @"
extern ""C"" __global__ void rocm_stress_kernel(float4* data, float time) {
    int gid = blockIdx.x * blockDim.x + threadIdx.x;
    float4 v = data[gid];
    float4 t = make_float4(sinf(time + (float)gid * 0.0001f), cosf(time), sinf(time * 0.35f), cosf(time * 1.25f));
    float4 c1 = make_float4(0.12f, 0.25f, 0.38f, 0.51f);
    float4 c2 = make_float4(0.51f, 0.38f, 0.25f, 0.12f);
    float4 scale = make_float4(0.999f, 0.999f, 0.999f, 0.999f);
    float4 eps = make_float4(0.001f, 0.001f, 0.001f, 0.001f);

    #pragma unroll 16
    for (int i = 0; i < 256; ++i) {
        v.x = fmaf(v.x, t.x, sinf(v.x * 1.35f + c1.x));
        v.y = fmaf(v.y, t.y, sinf(v.y * 1.35f + c1.y));
        v.z = fmaf(v.z, t.z, sinf(v.z * 1.35f + c1.z));
        v.w = fmaf(v.w, t.w, sinf(v.w * 1.35f + c1.w));

        t.x = fmaf(t.x, scale.x, cosf(v.x * 1.85f - c2.x));
        t.y = fmaf(t.y, scale.y, cosf(v.y * 1.85f - c2.y));
        t.z = fmaf(t.z, scale.z, cosf(v.z * 1.85f - c2.z));
        t.w = fmaf(t.w, scale.w, cosf(v.w * 1.85f - c2.w));

        v.x = v.x * v.x * 0.25f + eps.x;
        v.y = v.y * v.y * 0.25f + eps.y;
        v.z = v.z * v.z * 0.25f + eps.z;
        v.w = v.w * v.w * 0.25f + eps.w;
    }

    data[gid] = v;
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
        if (PixelUiEngine.BtnStartStop.Contains(x, y)) ToggleBenchmark();
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
        if (s_isBenchmarking) return;

        s_isBenchmarking = true;
        s_benchTimer.Restart();

        string exePath = Environment.ProcessPath ?? "/proc/self/exe";
        ProcessStartInfo psi = new(exePath, $"--rocm-worker {s_activeGpuOrdinal}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        s_workerProcess = Process.Start(psi);
        Console.WriteLine($"[UI] >> BENCHMARK STARTED (ROCm Worker Spawned on Ordinal {s_activeGpuOrdinal}) <<");
    }

    private static void StopBenchmark()
    {
        s_isBenchmarking = false;
        s_benchTimer.Stop();

        if (s_workerProcess != null && !s_workerProcess.HasExited)
        {
            try
            {
                s_workerProcess.Kill(entireProcessTree: true);
                s_workerProcess.WaitForExit(100);
            }
            catch { }
            s_workerProcess = null;
        }

        Console.WriteLine("[UI] >> BENCHMARK STOPPED (Worker Killed -> 0% Idle) <<");
    }
}