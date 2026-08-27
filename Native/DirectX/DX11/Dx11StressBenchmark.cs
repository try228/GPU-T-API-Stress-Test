using GPU_T.StressTest.Core;
using GPU_T.StressTest.Native.DirectX.Common;
using GPU_T.StressTest.Runtimes;

namespace GPU_T.StressTest.Native.DirectX.DX11;

public static class Dx11StressBenchmark
{
    public static void Run(CancellationToken hostCt, int durationSec, D3DTranslationLayer layer, GpuDeviceDescriptor? targetGpu, bool useNativeD3D = false)
    {
        var runtimes = RuntimeScanner.DiscoverAll();
        var targetBackend = layer == D3DTranslationLayer.WineD3D ? TargetBackend.Wd3d11 : TargetBackend.Dxvk11;
        var selectedRuntime = BackendRouter.ResolveRuntime(targetBackend, runtimes);

        if (selectedRuntime == null)
            throw new InvalidOperationException("No suitable Wine or Proton runtime found to execute Direct3D 11.");

        DirectXRunner.LaunchPayload(selectedRuntime, "dx11", layer, targetGpu, durationSec, hostCt, useNativeD3D);
    }
}