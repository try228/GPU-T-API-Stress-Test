using System.Diagnostics;

namespace GPU_T.StressTest.Runtimes;

/// <summary>
/// Executes Direct3D stress-testing payloads inside Wine or Proton prefixes.
/// </summary>
public static class WindowsPayloadRunner
{
    /// <summary>
    /// Launches a Windows payload (.exe) inside the selected runtime environment.
    /// </summary>
    /// <param name="runtime">The target Wine/Proton runtime.</param>
    /// <param name="payloadExePath">Path to the Windows D3D executable.</param>
    /// <param name="ct">Cancellation token for process lifecycle.</param>
    public static void Launch(RuntimeEnvironment runtime, string payloadExePath, CancellationToken ct)
    {
        Console.WriteLine($"[PayloadRunner] Launching {Path.GetFileName(payloadExePath)} via {runtime.Name}...");

        ProcessStartInfo psi = new()
        {
            FileName = runtime.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (runtime.Type == RuntimeType.Proton)
        {
            psi.ArgumentList.Add("run");
        }
        psi.ArgumentList.Add(payloadExePath);

        // Inject D3D Overlay / HUD options
        psi.Environment["DXVK_HUD"] = "fps,frametimes,gpuload";
        psi.Environment["VKD3D_CONFIG"] = "dxr11,dxr";

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to launch runner process.");

        while (!process.WaitForExit(100))
        {
            if (ct.IsCancellationRequested)
            {
                Console.WriteLine("[PayloadRunner] Stopping runner process tree...");
                process.Kill(entireProcessTree: true);
                break;
            }
        }
    }
}