using System.Diagnostics;
using GPU_T.StressTest.Core;
using GPU_T.StressTest.Native.OpenGL;
using GPU_T.StressTest.Native.OpenCL;
using GPU_T.StressTest.Native.Vulkan;
using GPU_T.StressTest.Native.CUDA;
using GPU_T.StressTest.Native.ROCm;
using GPU_T.StressTest.Native.OneAPI;
using GPU_T.StressTest.Runtimes;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

namespace GPU_T.StressTest;

/// <summary>
/// Main entry point for the GPU-T Stress Test sidecar agent.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // 1. Isolated ROCm compute worker process
        if (args.Length > 0 && args[0] == "--rocm-worker")
        {
            int workerGpu = 0;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedGpu)) workerGpu = parsedGpu;

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            RocmStressBenchmark.RunWorkerProcess(cts.Token, workerGpu);
            return 0;
        }

        // 2. CLI Argument Parsing
        string? backendStr = Environment.GetEnvironmentVariable("API_backend");
        string? gpuArg = null;
        int duration = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if ((arg is "--backend" or "-b") && i + 1 < args.Length) backendStr = args[++i];
            else if (arg.StartsWith("-b=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase))
                backendStr = arg.Substring(arg.IndexOf('=') + 1);
            else if ((arg is "--duration" or "-d") && i + 1 < args.Length && int.TryParse(args[i + 1], out int d)) { duration = d; i++; }
            else if (arg.StartsWith("-d=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--duration=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(arg.Substring(arg.IndexOf('=') + 1), out int dVal)) duration = dVal;
            }
            else if ((arg is "--gpu" or "-g") && i + 1 < args.Length) gpuArg = args[++i];
            else if (arg.StartsWith("-g=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--gpu=", StringComparison.OrdinalIgnoreCase))
                gpuArg = arg.Substring(arg.IndexOf('=') + 1);
            else if (arg == "--list-gpus")
            {
                GpuDeviceManager.PrintGpuList();
                return 0;
            }
            else if (arg is "--help" or "-h")
            {
                PrintHelp();
                return 0;
            }
        }

        try
        {
            var backend = BackendRouter.ParseBackend(backendStr);
            BackendRouter.ApplyEnvironmentOverrides(backend);

            var detectedGpus = GpuDeviceManager.EnumerateGpus();
            int selectedGpuIndex = GpuDeviceManager.ResolveGpuIndex(gpuArg, detectedGpus);
            var targetGpu = detectedGpus.Count > 0 ? detectedGpus[selectedGpuIndex] : null;

            GlfwWindowing.RegisterPlatform();
            GlfwInput.RegisterPlatform();

            Console.WriteLine("=== GPU-T Benchmark & Stress Test Sidecar (Native AOT) ===");
            Console.WriteLine($"[Agent] Target Backend: {backend}");
            if (targetGpu != null)
                Console.WriteLine($"[Agent] Target Device:  [{selectedGpuIndex}] {targetGpu.Name} ({targetGpu.Vendor})");

            using var lifecycle = new LifecycleManager(0);

            // Direct execution dispatch with exact target GPU matching
            switch (backend)
            {
                case TargetBackend.Vk:
                    VulkanStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex);
                    break;
                case TargetBackend.Gl:
                case TargetBackend.Zink:
                    OpenGlStressBenchmark.Run(lifecycle.Token, duration, isGles: false, isZink: backend == TargetBackend.Zink, selectedGpuIndex);
                    break;
                case TargetBackend.Gles:
                case TargetBackend.ZinkEs:
                    OpenGlStressBenchmark.Run(lifecycle.Token, duration, isGles: true, isZink: backend == TargetBackend.ZinkEs, selectedGpuIndex);
                    break;
                case TargetBackend.Cl:
                case TargetBackend.MesaCl:
                    OpenClStressBenchmark.Run(lifecycle.Token, duration, isRusticl: backend == TargetBackend.MesaCl, gpuArg, selectedGpuIndex);
                    break;
                case TargetBackend.Cuda:
                    CudaStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex, targetGpu);
                    break;
                case TargetBackend.Rocm:
                    RocmStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex, targetGpu);
                    break;
                case TargetBackend.Oapi:
                    OneApiStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex, targetGpu);
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
            }

            Console.WriteLine("[Agent] Execution completed successfully. Exit code 0.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Fatal Error] {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: GPU-T.StressTest [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -b, --backend <api>     Select backend: vk, gl, gles, zink, zink_es, cl, mesa_cl, cuda, rocm, oapi, dxvk, vkd3d, vkd3d_p, wd3d");
        Console.WriteLine("  -g, --gpu <index|name>  Select target GPU device by numeric index (0, 1) or name substring (e.g. 'amd', 'intel', 'nvidia')");
        Console.WriteLine("  -d, --duration <sec>    Initial test duration in seconds (0 = unlimited)");
        Console.WriteLine("      --list-gpus         Print list of all detected GPU devices and exit");
        Console.WriteLine("  -h, --help              Show this help information");
    }
}