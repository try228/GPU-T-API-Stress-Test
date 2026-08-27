using GPU_T.StressTest.Core;
using GPU_T.StressTest.Native.DirectX.Common;
using GPU_T.StressTest.Runtimes;

namespace GPU_T.StressTest.Native.DirectX.DX12;

public static class Dx12StressBenchmark
{
    public static void Run(CancellationToken hostCt, int durationSec, D3DTranslationLayer layer, GpuDeviceDescriptor? targetGpu, bool useNativeD3D = false)
    {
        var runtimes = RuntimeScanner.DiscoverAll();
        var targetBackend = layer == D3DTranslationLayer.Vkd3d ? TargetBackend.Vkd3d : TargetBackend.Vkd3dP;
        var selectedRuntime = BackendRouter.ResolveRuntime(targetBackend, runtimes);

        if (selectedRuntime == null)
            throw new InvalidOperationException("No suitable Wine or Proton runtime found to execute Direct3D 12.");

        DirectXRunner.LaunchPayload(selectedRuntime, "dx12", layer, targetGpu, durationSec, hostCt, useNativeD3D);
    }
}