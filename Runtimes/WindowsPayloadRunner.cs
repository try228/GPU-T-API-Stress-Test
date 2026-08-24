using System.Diagnostics;

namespace GpuT.Agent.Runtimes;

public static class WindowsPayloadRunner
{
    public static void Launch(RuntimeEnvironment runtime, string payloadExePath, CancellationToken ct)
    {
        Console.WriteLine($"[PayloadRunner] Launching {payloadExePath} via {runtime.Name}...");

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

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to launch runner process.");

        // Ожидание завершения или отмены по токену
        while (!process.WaitForExit(100))
        {
            if (ct.IsCancellationRequested)
            {
                Console.WriteLine("[PayloadRunner] Stopping Windows Runner Process...");
                process.Kill(entireProcessTree: true);
                break;
            }
        }
    }
}