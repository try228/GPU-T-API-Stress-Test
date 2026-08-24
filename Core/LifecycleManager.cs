using System.Runtime.InteropServices;

namespace GpuT.Agent.Core;

public sealed class LifecycleManager : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly PosixSignalRegistration _sigIntReg;
    private readonly PosixSignalRegistration _sigTermReg;

    public CancellationToken Token => _cts.Token;

    public LifecycleManager(int durationSeconds)
    {
        _sigIntReg = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx =>
        {
            ctx.Cancel = true;
            Console.WriteLine("\n[Lifecycle] Received SIGINT (Ctrl+C). Initiating fast shutdown...");
            _cts.Cancel();
        });

        _sigTermReg = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
        {
            ctx.Cancel = true;
            Console.WriteLine("\n[Lifecycle] Received SIGTERM. Initiating fast shutdown...");
            _cts.Cancel();
        });

        if (durationSeconds > 0)
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(durationSeconds));
        }
    }

    public void Dispose()
    {
        _sigIntReg.Dispose();
        _sigTermReg.Dispose();
        _cts.Dispose();
    }
}