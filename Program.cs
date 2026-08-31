using System.Diagnostics;
using GPU_T.StressTest.Core;
using GPU_T.StressTest.Native.OpenGL;
using GPU_T.StressTest.Native.OpenCL;
using GPU_T.StressTest.Native.Vulkan;
using GPU_T.StressTest.Native.CUDA;
using GPU_T.StressTest.Native.ROCm;
using GPU_T.StressTest.Native.OneAPI;
using GPU_T.StressTest.Native.DirectX.Common;
using GPU_T.StressTest.Native.DirectX.DX9;
using GPU_T.StressTest.Native.DirectX.DX11;
using GPU_T.StressTest.Native.DirectX.DX12;
using GPU_T.StressTest.Runtimes;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

namespace GPU_T.StressTest;

/// <summary>
/// Main entry point for the GPU-T Stress Test sidecar agent.
/// Parses CLI arguments, configures environment overrides, and routes execution to selected backend.
/// </summary>
public static class Program
{
    /// <summary>
    /// Application main execution entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Process exit code (0 on success, 1 on fatal error).</returns>
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
        bool useNativeD3D = false;
        bool enableDebug = false;
        MockGpuTarget mockGpu = MockGpuTarget.None;

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
            else if ((arg is "--mock-gpu" or "-m") && i + 1 < args.Length)
            {
                mockGpu = ParseMockGpuTarget(args[++i]);
            }
            else if (arg.StartsWith("--mock-gpu=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-m=", StringComparison.OrdinalIgnoreCase))
            {
                mockGpu = ParseMockGpuTarget(arg.Substring(arg.IndexOf('=') + 1));
            }
            else if (arg is "--native-d3d" or "--native-payload" or "-n")
            {
                useNativeD3D = true;
            }
            else if (arg is "--debug" or "-dbg" or "--vk-debug")
            {
                enableDebug = true;
            }
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

        if (Environment.GetEnvironmentVariable("GPUT_EXPERIMENTAL_D3D") == "1")
        {
            useNativeD3D = true;
        }

        if (Environment.GetEnvironmentVariable("GPUT_DEBUG") == "1")
        {
            enableDebug = true;
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
            if (enableDebug)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[Agent] Vulkan Debug Messenger Active (VK_EXT_debug_utils)");
                Console.ResetColor();
            }
            if (mockGpu != MockGpuTarget.None)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[Agent] Virtual Mock GPU Active: {mockGpu}");
                Console.ResetColor();
            }
            if (targetGpu != null)
                Console.WriteLine($"[Agent] Target Device:  [{selectedGpuIndex}] {targetGpu.Name} ({targetGpu.Vendor})");

            using var lifecycle = new LifecycleManager(0);

            // Direct execution dispatch
            switch (backend)
            {
                case TargetBackend.Gl:
                case TargetBackend.Zink:
                    OpenGlStressBenchmark.Run(lifecycle.Token, duration, isGles: false, isZink: backend == TargetBackend.Zink, selectedGpuIndex);
                    break;
                case TargetBackend.Gles:
                case TargetBackend.ZinkEs:
                    OpenGlStressBenchmark.Run(lifecycle.Token, duration, isGles: true, isZink: backend == TargetBackend.ZinkEs, selectedGpuIndex);
                    break;
                case TargetBackend.Vk:
                    VulkanStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex, mockGpu, enableDebug);
                    break;
                case TargetBackend.Cl:
                case TargetBackend.MesaCl:
                    OpenClStressBenchmark.Run(lifecycle.Token, duration, isRusticl: backend == TargetBackend.MesaCl, gpuArg, selectedGpuIndex);
                    break;
                case TargetBackend.Cuda:
                    CudaStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex);
                    break;
                case TargetBackend.Rocm:
                    RocmStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex);
                    break;
                case TargetBackend.Oapi:
                    OneApiStressBenchmark.Run(lifecycle.Token, duration, selectedGpuIndex);
                    break;

                // Direct3D 9
                case TargetBackend.Dxvk9:
                    Dx9StressBenchmark.Run(lifecycle.Token, duration, D3DTranslationLayer.Dxvk, targetGpu, useNativeD3D);
                    break;
                case TargetBackend.Wd3d9:
                    Dx9StressBenchmark.Run(lifecycle.Token, duration, D3DTranslationLayer.WineD3D, targetGpu, useNativeD3D);
                    break;

                // Direct3D 11
                case TargetBackend.Dxvk11:
                    Dx11StressBenchmark.Run(lifecycle.Token, duration, D3DTranslationLayer.Dxvk, targetGpu, useNativeD3D);
                    break;
                case TargetBackend.Wd3d11:
                    Dx11StressBenchmark.Run(lifecycle.Token, duration, D3DTranslationLayer.WineD3D, targetGpu, useNativeD3D);
                    break;

                // Direct3D 12
                case TargetBackend.Vkd3d:
                    Dx12StressBenchmark.Run(lifecycle.Token, duration, D3DTranslationLayer.Vkd3d, targetGpu, useNativeD3D);
                    break;
                case TargetBackend.Vkd3dP:
                    Dx12StressBenchmark.Run(lifecycle.Token, duration, D3DTranslationLayer.Vkd3dProton, targetGpu, useNativeD3D);
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

    private static MockGpuTarget ParseMockGpuTarget(string val) => val.ToLowerInvariant() switch
    {
        "nvidia" or "blackwell" or "nv" or "rtx" => MockGpuTarget.NvidiaBlackwell,
        "intel" or "battlemage" or "arc" or "xmx" => MockGpuTarget.IntelBattlemage,
        "amd" or "rdna3" or "radeon" => MockGpuTarget.AmdRdna3,
        "pascal" or "legacy" or "gtx" => MockGpuTarget.LegacyPascal,
        _ => MockGpuTarget.None
    };

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: GPU-T.StressTest [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -b, --backend <api>     Select backend (default: gl): gl, gles, vk, zink, cl, mesa_cl, cuda, rocm, oapi, dxvk_9, dxvk_11, wd3d_9, wd3d_11, vkd3d, vkd3d_p");
        Console.WriteLine("  -g, --gpu <index|name>  Select target GPU device by numeric index (0, 1) or name substring");
        Console.WriteLine("  -d, --duration <sec>    Initial test duration in seconds (0 = unlimited)");
        Console.WriteLine("  -m, --mock-gpu <vendor> Simulate virtual GPU architecture (nvidia, intel, rdna3, pascal)");
        Console.WriteLine("  -n, --native-d3d        Use lightweight native C payload (d3d_stress_native.exe) instead of managed .NET payload");
        Console.WriteLine("      --debug, -dbg       Enable Vulkan debug messenger (VK_EXT_debug_utils) callback logging");
        Console.WriteLine("      --list-gpus         Print list of all detected GPU devices and exit");
        Console.WriteLine("  -h, --help              Show this help information");
    }
}