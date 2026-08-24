using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using GpuT.Agent.Native.Vulkan;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

using MouseButton = Silk.NET.Input.MouseButton;

namespace GpuT.Agent.Native.OpenGL;

public sealed unsafe class OpenGlStressBenchmark
{
    private const int WinWidth = PixelUiEngine.BaseWidth;   // 900
    private const int WinHeight = PixelUiEngine.BaseHeight; // 550

    private static volatile bool s_isBenchmarking = false;
    private static volatile int s_targetDurationSec = 0;
    private static string s_customInputBuffer = "";
    private static bool s_isCustomFocused = false;

    private static Stopwatch s_benchTimer = new();
    private static ulong s_totalFrames = 0;
    private static double s_currentFps = 0;
    private static IWindow? s_windowInstance = null;

    private static int s_mouseX = 0;
    private static int s_mouseY = 0;

public static void Run(CancellationToken hostCt, int initialDuration = 0, bool isGles = false, bool isZink = false)
    {
        // Статическая регистрация GLFW для Native AOT
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        s_targetDurationSec = initialDuration;
        s_customInputBuffer = initialDuration > 0 ? initialDuration.ToString() : "";
        s_isBenchmarking = false;
        s_benchTimer.Reset();

        string apiTitle = isZink
            ? (isGles ? "Zink (OpenGL ES over Vulkan)" : "Zink (OpenGL over Vulkan)")
            : (isGles ? "OpenGL ES 3.0" : "OpenGL 3.3 Core");

        // 1. Каноничная настройка окна Silk.NET
        var winOptions = WindowOptions.Default;
        winOptions.Size = new Vector2D<int>(WinWidth, WinHeight);
        winOptions.Title = $"GPU-T Render Test & {apiTitle} Stress Agent";
        winOptions.VSync = false;
        winOptions.WindowBorder = WindowBorder.Fixed;
        winOptions.ShouldSwapAutomatically = false; // Мы сами вызываем SwapBuffers
        winOptions.IsVisible = true; // Гарантированный показ окна

        // Строгое указание API для Silk.NET (он сам подберет EGL/GLX под капотом)
        winOptions.API = isGles
            ? new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0))
            : new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));

        using var window = Window.Create(winOptions);
        s_windowInstance = window;
        window.Initialize(); // Инициализирует окно и контекст
        
        // 2. Получение OpenGL API из готового окна
        var gl = GL.GetApi(window);

        string rawRenderer = gl.GetStringS(StringName.Renderer) ?? "Generic GPU";
        string version = gl.GetStringS(StringName.Version) ?? "Unknown Version";
        string vendor = gl.GetStringS(StringName.Vendor) ?? "Unknown Vendor";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OpenGLEngine] Context Active: {apiTitle}");
        Console.WriteLine($"[OpenGLEngine] GL_RENDERER: {rawRenderer} ({vendor})");
        Console.WriteLine($"[OpenGLEngine] GL_VERSION:  {version}");
        Console.ResetColor();

        // 3. Отключаем лишнее
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        gl.ClearColor(0.08f, 0.09f, 0.12f, 1.0f);

        // 4. Компиляция шейдеров
        string vsSource = isGles ? GetGlesVertexShader() : GetDesktopVertexShader();
        string fsUiSource = isGles ? GetGlesUiFragmentShader() : GetDesktopUiFragmentShader();
        string fsStressSource = isGles ? GetGlesStressFragmentShader() : GetDesktopStressFragmentShader();

        uint uiProgram = CreateProgram(gl, vsSource, fsUiSource);
        uint stressProgram = CreateProgram(gl, vsSource, fsStressSource);

        gl.UseProgram(uiProgram);
        int locUiTex = gl.GetUniformLocation(uiProgram, "uUiTexture");
        gl.Uniform1(locUiTex, 0);

        gl.UseProgram(stressProgram);
        int locStressTime = gl.GetUniformLocation(stressProgram, "uTime");

        // 5. Полноэкранный квад (CCW)
        float[] quadVertices = [
            -1.0f, -1.0f,  0.0f, 1.0f,
             1.0f, -1.0f,  1.0f, 1.0f,
             1.0f,  1.0f,  1.0f, 0.0f,

            -1.0f, -1.0f,  0.0f, 1.0f,
             1.0f,  1.0f,  1.0f, 0.0f,
            -1.0f,  1.0f,  0.0f, 0.0f
        ];

        uint vao = gl.GenVertexArray();
        uint vbo = gl.GenBuffer();

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

        // 6. Текстура UI (Чистый GL_RGBA)
        uint uiTexture = gl.GenTexture();
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

        uint[] uiPixels = new uint[WinWidth * WinHeight];

        // 7. Ввод (через абстракцию Silk.NET)
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

        // 8. Исполнительный цикл
        var perfSw = Stopwatch.StartNew();
        ulong lastFrames = 0;
        float animTime = 0f;

        // Первый принудительный кадр для Wayland/DRI_PRIME
        gl.Viewport(0, 0, (uint)WinWidth, (uint)WinHeight);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        window.SwapBuffers();

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

            double elapsed = s_isBenchmarking ? s_benchTimer.Elapsed.TotalSeconds : 0.0;
            double tflops = s_isBenchmarking ? (s_currentFps * 1.52) / 1000.0 : 0.0;

            // Отрисовка UI
            var theme = isGles ? ThemePalette.OpenGLES : ThemePalette.OpenGL;

            // Отрисовка UI: Синий для Desktop OpenGL, Фиолетово-розовый для OpenGL ES
            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, WinWidth, WinHeight,
                    apiTitle, rawRenderer, s_isBenchmarking, s_targetDurationSec,
                    s_customInputBuffer, s_isCustomFocused,
                    elapsed, s_currentFps, tflops,
                    null,
                    theme,
                    s_mouseX, s_mouseY, animTime);

                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, uiTexture);
                gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)WinWidth, (uint)WinHeight, PixelFormat.Bgra, PixelType.UnsignedByte, pUi);
            }

            gl.Clear(ClearBufferMask.ColorBufferBit);

            // А) МНОГОПРОХОДНЫЙ СТРЕСС-ТЕСТ GPU
            if (s_isBenchmarking)
            {
                gl.UseProgram(stressProgram);
                gl.Uniform1(locStressTime, animTime);
                gl.BindVertexArray(vao);

                for (int p = 0; p < 32; p++)
                {
                    gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
                }
            }

            // Б) Отрисовка чистого UI
            gl.UseProgram(uiProgram);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, uiTexture);
            gl.BindVertexArray(vao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

            // Форсируем сброс очередей для гибридной графики
            gl.Flush();
            window.SwapBuffers();
            s_totalFrames++;

            if (perfSw.ElapsedMilliseconds >= 250)
            {
                double sec = perfSw.Elapsed.TotalSeconds;
                s_currentFps = s_isBenchmarking ? (s_totalFrames - lastFrames) / sec : 60.0;
                lastFrames = s_totalFrames;
                perfSw.Restart();
            }

            if (!s_isBenchmarking)
            {
                Thread.Sleep(16);
            }
        }

        // 9. Освобождение
        gl.DeleteTexture(uiTexture);
        gl.DeleteBuffer(vbo);
        gl.DeleteVertexArray(vao);
        gl.DeleteProgram(uiProgram);
        gl.DeleteProgram(stressProgram);

        s_windowInstance = null;
        Console.WriteLine($"[OpenGLEngine] {apiTitle} context released cleanly. Exit 0.");
    }

    private static uint CreateProgram(GL gl, string vsSrc, string fsSrc)
    {
        uint vs = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vs, vsSrc);
        gl.CompileShader(vs);
        CheckShaderError(gl, vs, "Vertex");

        uint fs = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fs, fsSrc);
        gl.CompileShader(fs);
        CheckShaderError(gl, fs, "Fragment");

        uint program = gl.CreateProgram();
        gl.AttachShader(program, vs);
        gl.AttachShader(program, fs);
        gl.LinkProgram(program);

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);

        return program;
    }

    private static void CheckShaderError(GL gl, uint shader, string stage)
    {
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string info = gl.GetShaderInfoLog(shader);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[OpenGLEngine] {stage} Shader Compile Error: {info}");
            Console.ResetColor();
        }
    }

    private static string GetDesktopVertexShader() => @"#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() {
    gl_Position = vec4(aPos, 0.0, 1.0);
    TexCoord = aTexCoord;
}";

    private static string GetDesktopUiFragmentShader() => @"#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uUiTexture;
void main() {
    FragColor = texture(uUiTexture, TexCoord);
}";

    private static string GetDesktopStressFragmentShader() => @"#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform float uTime;
void main() {
    vec2 p = TexCoord * 2.0 - 1.0;
    vec4 acc = vec4(p.x, p.y, sin(uTime), cos(uTime));
    for (int i = 0; i < 512; ++i) {
        acc = sin(acc * 1.35 + vec4(0.1, 0.3, 0.5, 0.7)) * 0.5 + cos(acc * 1.85 - vec4(0.7, 0.5, 0.3, 0.1)) * 0.5;
        acc = acc * acc + vec4(0.001);
    }
    FragColor = acc * 0.001;
}";

    private static string GetGlesVertexShader() => @"#version 300 es
precision highp float;
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() {
    gl_Position = vec4(aPos, 0.0, 1.0);
    TexCoord = aTexCoord;
}";

    private static string GetGlesUiFragmentShader() => @"#version 300 es
precision highp float;
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uUiTexture;
void main() {
    FragColor = texture(uUiTexture, TexCoord);
}";

    private static string GetGlesStressFragmentShader() => @"#version 300 es
precision highp float;
in vec2 TexCoord;
out vec4 FragColor;
uniform float uTime;
void main() {
    vec2 p = TexCoord * 2.0 - 1.0;
    vec4 acc = vec4(p.x, p.y, sin(uTime), cos(uTime));
    for (int i = 0; i < 512; ++i) {
        acc = sin(acc * 1.35 + vec4(0.1, 0.3, 0.5, 0.7)) * 0.5 + cos(acc * 1.85 - vec4(0.7, 0.5, 0.3, 0.1)) * 0.5;
        acc = acc * acc + vec4(0.001);
    }
    FragColor = acc * 0.001;
}";

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