using System.Diagnostics;
using GpuT.Agent.Core;
using GpuT.Agent.Native.OpenGL;
using GpuT.Agent.Native.OpenCL;
using GpuT.Agent.Native.Vulkan;
using GpuT.Agent.Native.CUDA; // <-- Добавлен неймспейс CUDA
using GpuT.Agent.Runtimes;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

namespace GpuT.Agent;

public static class Program
{
    public static int Main(string[] args)
    {
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        string? backendEnv = Environment.GetEnvironmentVariable("API_backend")?.ToLowerInvariant().Trim();
        var backend = BackendRouter.ParseBackend(backendEnv);

        // Инъекция флагов окружения Mesa (Zink, Rusticl)
        BackendRouter.ApplyEnvironmentOverrides(backend);

        // Self-Exec для Zink до инициализации libGL
        if (backend is TargetBackend.Zink or TargetBackend.ZinkEs)
        {
            string? currentOverride = Environment.GetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE");
            if (currentOverride != "zink")
            {
                string exePath = Environment.ProcessPath ?? "/proc/self/exe";
                ProcessStartInfo psi = new(exePath, string.Join(' ', args))
                {
                    UseShellExecute = false
                };
                psi.EnvironmentVariables["MESA_LOADER_DRIVER_OVERRIDE"] = "zink";
                psi.EnvironmentVariables["GALLIUM_DRIVER"] = "zink";

                using var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode ?? 0;
            }
        }

        Console.WriteLine("=== GPU-T Benchmark Agent (Native AOT .NET 10) ===");
        Console.WriteLine($"[Agent] API_backend = {backend} (Raw: '{backendEnv}')");

        int duration = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedDuration))
            {
                duration = parsedDuration;
            }
        }

        using var lifecycle = new LifecycleManager(0);

        switch (backend)
        {
            case TargetBackend.Vk:
                VulkanStressBenchmark.Run(lifecycle.Token, duration);
                break;

            case TargetBackend.Gl:
            case TargetBackend.Zink:
                OpenGlStressBenchmark.Run(lifecycle.Token, duration, isGles: false, isZink: backend == TargetBackend.Zink);
                break;

            case TargetBackend.Gles:
            case TargetBackend.ZinkEs:
                OpenGlStressBenchmark.Run(lifecycle.Token, duration, isGles: true, isZink: backend == TargetBackend.ZinkEs);
                break;

            // Compute: OpenCL & Rusticl
            case TargetBackend.Cl:
            case TargetBackend.MesaCl:
                OpenClStressBenchmark.Run(lifecycle.Token, duration, isRusticl: backend == TargetBackend.MesaCl);
                break;

            // Compute: NVIDIA CUDA (Native или через ZLUDA на AMD)
            case TargetBackend.Cuda:
                CudaStressBenchmark.Run(lifecycle.Token, duration);
                break;

            case TargetBackend.Dxvk:
            case TargetBackend.Vkd3d:
            case TargetBackend.Vkd3dP:
            case TargetBackend.Wd3d:
                var runtimes = RuntimeScanner.DiscoverAll();
                var selected = BackendRouter.ResolveRuntime(backend, runtimes);
                if (selected != null)
                {
                    string dummyPayload = Path.Combine(AppContext.BaseDirectory, "d3d_stress.exe");
                    WindowsPayloadRunner.Launch(selected, dummyPayload, lifecycle.Token);
                }
                break;

            case TargetBackend.Rocm:
            case TargetBackend.Oapi:
                Console.WriteLine($"[ComputeEngine] Backend {backend} selected.");
                break;
        }

        Console.WriteLine("[Agent] Shutdown complete. Exit code 0.");
        return 0;
    }
}