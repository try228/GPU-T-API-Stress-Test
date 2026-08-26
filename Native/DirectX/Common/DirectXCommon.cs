using System.Diagnostics;
using GPU_T.StressTest.Core;
using GPU_T.StressTest.Runtimes;

namespace GPU_T.StressTest.Native.DirectX.Common;

/// <summary>
/// Supported Direct3D translation layer implementations.
/// </summary>
public enum D3DTranslationLayer
{
    Dxvk,
    WineD3D,
    Vkd3d,
    Vkd3dProton
}

/// <summary>
/// Common process runner and environment manager for Direct3D workloads under Wine/Proton.
/// </summary>
public static class DirectXRunner
{
    private const string PayloadExecutable = "d3d_stress.exe";

    public static void LaunchPayload(
        RuntimeEnvironment runtime,
        string apiIdentifier,
        D3DTranslationLayer layer,
        GpuDeviceDescriptor? targetGpu,
        int durationSec,
        CancellationToken ct)
    {
        string payloadPath = Path.Combine(AppContext.BaseDirectory, PayloadExecutable);
        if (!File.Exists(payloadPath))
        {
            string subPath = Path.Combine(AppContext.BaseDirectory, "payloads", PayloadExecutable);
            payloadPath = File.Exists(subPath) ? subPath : payloadPath;
        }

        if (!File.Exists(payloadPath))
        {
            throw new FileNotFoundException(
                $"Direct3D Windows payload '{PayloadExecutable}' was not found!\n" +
                $"Please build it with: 'dotnet publish Payloads/D3DPayload -c Release -r win-x64 -o {Path.GetDirectoryName(payloadPath)}'");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[DirectXRunner] Runtime:           [{runtime.Type}] {runtime.Name}");
        Console.WriteLine($"[DirectXRunner] Direct3D API:      {apiIdentifier.ToUpperInvariant()} ({layer})");
        if (targetGpu != null)
        {
            Console.WriteLine($"[DirectXRunner] Target GPU Filter: [{targetGpu.Index}] {targetGpu.Name}");
        }
        Console.WriteLine($"[DirectXRunner] Duration:          {(durationSec > 0 ? $"{durationSec}s" : "Unlimited")}\n");
        Console.ResetColor();

        ProcessStartInfo psi = new()
        {
            FileName = runtime.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        if (runtime.Type == RuntimeType.Proton)
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string compatData = Path.Combine(Path.GetTempPath(), "gput_proton_prefix");
            Directory.CreateDirectory(compatData);

            psi.Environment["STEAM_COMPAT_DATA_PATH"] = compatData;
            psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = Path.Combine(userHome, ".local/share/Steam");
            psi.ArgumentList.Add("run");
        }

        psi.ArgumentList.Add(payloadPath);
        psi.ArgumentList.Add("--api");
        psi.ArgumentList.Add(apiIdentifier);

        if (durationSec > 0)
        {
            psi.ArgumentList.Add("--duration");
            psi.ArgumentList.Add(durationSec.ToString());
        }

        ApplyLayerEnvironment(psi, layer, targetGpu);

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to spawn runtime process: {runtime.ExecutablePath}");
        }

        var timer = Stopwatch.StartNew();

        while (!process.WaitForExit(100))
        {
            if (ct.IsCancellationRequested || (durationSec > 0 && timer.Elapsed.TotalSeconds >= durationSec))
            {
                Console.WriteLine("\n[DirectXRunner] Stopping Direct3D workload process tree...");
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { }
                break;
            }
        }

        if (!ct.IsCancellationRequested && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Direct3D payload process failed with exit code: {process.ExitCode}");
        }

        Console.WriteLine("[DirectXRunner] Direct3D execution completed successfully.");
    }

    private static void ApplyLayerEnvironment(ProcessStartInfo psi, D3DTranslationLayer layer, GpuDeviceDescriptor? targetGpu)
    {
        switch (layer)
        {
            case D3DTranslationLayer.Dxvk:
                psi.Environment["WINEDLLOVERRIDES"] = "d3d11,dxgi,d3d9=n,b";
                psi.Environment["PROTON_USE_WINED3D"] = "0";
                // Компактный HUD в верхнем левом углу, не перекрывающий интерфейс
                psi.Environment["DXVK_HUD"] = "fps,gpuload,version";
                if (targetGpu != null)
                {
                    psi.Environment["DXVK_FILTER_DEVICE_NAME"] = targetGpu.Name;
                }
                break;

            case D3DTranslationLayer.WineD3D:
                psi.Environment["WINEDLLOVERRIDES"] = "d3d11,dxgi,d3d9=b";
                psi.Environment["PROTON_USE_WINED3D"] = "1";
                break;

            case D3DTranslationLayer.Vkd3dProton:
                psi.Environment["WINEDLLOVERRIDES"] = "d3d12=n,b";
                psi.Environment["VKD3D_CONFIG"] = "dxr11,dxr";
                if (targetGpu != null)
                {
                    psi.Environment["VKD3D_FILTER_DEVICE_NAME"] = targetGpu.Name;
                }
                break;

            case D3DTranslationLayer.Vkd3d:
                psi.Environment["WINEDLLOVERRIDES"] = "d3d12=b";
                break;
        }

        if (targetGpu != null)
        {
            string pciTag = $"{targetGpu.VendorId:x4}:{targetGpu.DeviceId:x4}";
            psi.Environment["MESA_VK_DEVICE_SELECT"] = pciTag;

            if (targetGpu.VendorId == 0x10DE)
            {
                psi.Environment["__NV_PRIME_RENDER_OFFLOAD"] = "1";
                psi.Environment["__VK_LAYER_NV_optimus"] = "NVIDIA_only";
                psi.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
            }
        }
    }
}