using GPU_T.StressTest.Core;
using GPU_T.StressTest.Native.DirectX.Common;
using GPU_T.StressTest.Runtimes;

namespace GPU_T.StressTest.Native.DirectX.DX9;

public static class Dx9StressBenchmark
{
    public static void Run(CancellationToken hostCt, int durationSec, D3DTranslationLayer layer, GpuDeviceDescriptor? targetGpu, bool useNativeD3D = false)
    {
        var runtimes = RuntimeScanner.DiscoverAll();
        var targetBackend = layer == D3DTranslationLayer.WineD3D ? TargetBackend.Wd3d9 : TargetBackend.Dxvk9;
        var selectedRuntime = BackendRouter.ResolveRuntime(targetBackend, runtimes);

        if (selectedRuntime == null)
            throw new InvalidOperationException("No suitable Wine or Proton runtime found to execute Direct3D 9.");

        DirectXRunner.LaunchPayload(selectedRuntime, "dx9", layer, targetGpu, durationSec, hostCt, useNativeD3D);
    }
}