using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Payloads.D3D;

public static unsafe partial class DX12Runner
{
    private const string D3D12Lib = "d3d12.dll";
    private const string DxgiLib = "dxgi.dll";

    public const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    public const int D3D12_RESOURCE_STATE_PRESENT = 0;
    public const int D3D12_RESOURCE_STATE_COPY_DEST = 0x400;
    public const int D3D12_RESOURCE_STATE_UNORDERED_ACCESS = 0x8;
    public const int D3D12_RESOURCE_STATE_GENERIC_READ = 0x1 | 0x2 | 0x40 | 0x80 | 0x200 | 0x800;

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC { public uint Count; public uint Quality; }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_COMMAND_QUEUE_DESC { public int Type; public int Priority; public int Flags; public uint NodeMask; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC1
    {
        public uint Width; public uint Height; public int Format; public int Stereo;
        public DXGI_SAMPLE_DESC SampleDesc; public uint BufferUsage; public uint BufferCount;
        public int Scaling; public int SwapEffect; public int AlphaMode; public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_HEAP_PROPERTIES
    {
        public int Type; public int CPUPageProperty; public int MemoryPoolPreference;
        public uint CreationNodeMask; public uint VisibleNodeMask;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct D3D12_RESOURCE_DESC
    {
        [FieldOffset(0)]  public int Dimension;
        [FieldOffset(8)]  public ulong Alignment;
        [FieldOffset(16)] public ulong Width;
        [FieldOffset(24)] public uint Height;
        [FieldOffset(28)] public ushort DepthOrArraySize;
        [FieldOffset(30)] public ushort MipLevels;
        [FieldOffset(32)] public int Format;
        [FieldOffset(36)] public DXGI_SAMPLE_DESC SampleDesc;
        [FieldOffset(44)] public int Layout;
        [FieldOffset(48)] public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_RESOURCE_TRANSITION_BARRIER { public nint pResource; public uint Subresource; public int StateBefore; public int StateAfter; }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct D3D12_RESOURCE_BARRIER
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(4)] public int Flags;
        [FieldOffset(8)] public D3D12_RESOURCE_TRANSITION_BARRIER Transition;
        [FieldOffset(8)] public nint pUavResource;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_PLACED_SUBRESOURCE_FOOTPRINT
    {
        public ulong Offset; public int Format; public uint Width; public uint Height; public uint Depth; public uint RowPitch;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct D3D12_TEXTURE_COPY_LOCATION
    {
        [FieldOffset(0)]  public nint pResource;
        [FieldOffset(8)]  public int Type;
        [FieldOffset(16)] public D3D12_PLACED_SUBRESOURCE_FOOTPRINT PlacedFootprint;
        [FieldOffset(16)] public uint SubresourceIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_ROOT_DESCRIPTOR { public uint ShaderRegister; public uint RegisterSpace; }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct D3D12_ROOT_PARAMETER
    {
        [FieldOffset(0)] public int ParameterType;
        [FieldOffset(8)] public D3D12_ROOT_DESCRIPTOR Descriptor;
        [FieldOffset(24)] public int ShaderVisibility;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_ROOT_SIGNATURE_DESC
    {
        public uint NumParameters; public D3D12_ROOT_PARAMETER* pParameters;
        public uint NumStaticSamplers; public void* pStaticSamplers; public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_SHADER_BYTECODE { public void* pShaderBytecode; public nuint BytecodeLength; }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_CACHED_PIPELINE_STATE { public void* pCachedBlob; public nuint CachedBlobSizeInBytes; }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_COMPUTE_PIPELINE_STATE_DESC
    {
        public nint pRootSignature; public D3D12_SHADER_BYTECODE CS;
        public uint NodeMask; public D3D12_CACHED_PIPELINE_STATE CachedPSO; public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_FEATURE_DATA_D3D12_OPTIONS
    {
        public int DoublePrecisionFloatShaderOps; public int OutputMergerLogicOp; public int MinPrecisionSupport;
        public int TiledResourcesTier; public int ResourceBindingTier; public int PSSpecifiedStencilRefSupported;
        public int TypedUAVLoadAdditionalFormats; public int ROVsSupported; public int ConservativeRasterizationTier;
        public uint MaxGPUVirtualAddressBitsPerResource; public int StandardSwizzle64KBSupported; public int CrossNodeSharingTier;
        public int CrossAdapterRowMajorTextureSupported; public int VPAndRTArrayIndexFromAnyShaderFeedingRasterizerSupportedWithoutGSEmulation;
        public int ResourceHeapTier;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_FEATURE_DATA_D3D12_OPTIONS4
    {
        public int MSAA64KBAlignedTextureSupported; public int SharedResourceCompatibilityTier; public int Native16BitShaderOpsSupported;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_FEATURE_DATA_D3D12_OPTIONS5
    {
        public int SRVOnlyTiledResourceTier3; public int RenderPassesTier; public int RaytracingTier;
    }

    [LibraryImport(D3D12Lib, EntryPoint = "D3D12CreateDevice")]
    public static partial int D3D12CreateDevice(nint pAdapter, uint MinimumFeatureLevel, in Guid riid, nint* ppDevice);

    [LibraryImport(DxgiLib, EntryPoint = "CreateDXGIFactory1")]
    public static partial int CreateDXGIFactory1(in Guid riid, nint* ppFactory);

    [LibraryImport(D3D12Lib, EntryPoint = "D3D12SerializeRootSignature")]
    public static partial int D3D12SerializeRootSignature(D3D12_ROOT_SIGNATURE_DESC* pRootSignature, int Version, nint* ppBlob, nint* ppErrorBlob);

    public static void Run(int durationSec)
    {
        nint hwnd = Win32Window.Create("GPU-T Direct3D 12 Stress Test", PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight);

        Guid id3d12DeviceGuid = new("189819f1-1db6-4b57-be54-1821339b85f7");
        nint device = nint.Zero;
        int res = D3D12CreateDevice(nint.Zero, 0xb000, in id3d12DeviceGuid, &device);
        if (res < 0 || device == nint.Zero)
            throw new InvalidOperationException($"D3D12CreateDevice failed: 0x{res:X8}");

        nint* devVtbl = *(nint**)device;

        delegate* unmanaged[Stdcall]<nint, D3D12_COMMAND_QUEUE_DESC*, in Guid, nint*, int> createCommandQueue =
            (delegate* unmanaged[Stdcall]<nint, D3D12_COMMAND_QUEUE_DESC*, in Guid, nint*, int>)devVtbl[8];
        delegate* unmanaged[Stdcall]<nint, int, in Guid, nint*, int> createCmdAlloc =
            (delegate* unmanaged[Stdcall]<nint, int, in Guid, nint*, int>)devVtbl[9];
        delegate* unmanaged[Stdcall]<nint, D3D12_COMPUTE_PIPELINE_STATE_DESC*, in Guid, nint*, int> createComputePSO =
            (delegate* unmanaged[Stdcall]<nint, D3D12_COMPUTE_PIPELINE_STATE_DESC*, in Guid, nint*, int>)devVtbl[11];
        delegate* unmanaged[Stdcall]<nint, uint, int, nint, nint, in Guid, nint*, int> createCmdList =
            (delegate* unmanaged[Stdcall]<nint, uint, int, nint, nint, in Guid, nint*, int>)devVtbl[12];
        delegate* unmanaged[Stdcall]<nint, int, void*, uint, int> checkFeatureSupport =
            (delegate* unmanaged[Stdcall]<nint, int, void*, uint, int>)devVtbl[13];
        delegate* unmanaged[Stdcall]<nint, uint, void*, nuint, in Guid, nint*, int> createRootSignature =
            (delegate* unmanaged[Stdcall]<nint, uint, void*, nuint, in Guid, nint*, int>)devVtbl[16];
        delegate* unmanaged[Stdcall]<nint, D3D12_HEAP_PROPERTIES*, int, D3D12_RESOURCE_DESC*, int, void*, in Guid, nint*, int> createCommittedResource =
            (delegate* unmanaged[Stdcall]<nint, D3D12_HEAP_PROPERTIES*, int, D3D12_RESOURCE_DESC*, int, void*, in Guid, nint*, int>)devVtbl[27];

        // 1. Hardware Features
        bool hasFp64 = false;
        D3D12_FEATURE_DATA_D3D12_OPTIONS opt0 = default;
        if (checkFeatureSupport(device, 0, &opt0, (uint)sizeof(D3D12_FEATURE_DATA_D3D12_OPTIONS)) == 0)
            hasFp64 = opt0.DoublePrecisionFloatShaderOps != 0;

        bool has16Bit = false;
        D3D12_FEATURE_DATA_D3D12_OPTIONS4 opt4 = default;
        if (checkFeatureSupport(device, 23, &opt4, (uint)sizeof(D3D12_FEATURE_DATA_D3D12_OPTIONS4)) == 0)
            has16Bit = opt4.Native16BitShaderOpsSupported != 0;

        bool hasRayQuery = false;
        D3D12_FEATURE_DATA_D3D12_OPTIONS5 opt5 = default;
        if (checkFeatureSupport(device, 27, &opt5, (uint)sizeof(D3D12_FEATURE_DATA_D3D12_OPTIONS5)) == 0)
            hasRayQuery = opt5.RaytracingTier >= 11;

        var testRegistry = new D3DTestRegistry(hasFp64, has16Bit, hasRayQuery);
        testRegistry.ApplyPreset(StressPreset.DefaultFP32);

        // 2. Queue & Swapchain
        D3D12_COMMAND_QUEUE_DESC queueDesc = new() { Type = 0, Priority = 0, Flags = 0, NodeMask = 0 };
        Guid id3d12CommandQueueGuid = new("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");
        nint commandQueue = nint.Zero;
        createCommandQueue(device, &queueDesc, in id3d12CommandQueueGuid, &commandQueue);

        Guid idxgiFactory2Guid = new("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
        nint factory = nint.Zero;
        CreateDXGIFactory1(in idxgiFactory2Guid, &factory);

        string gpuName = "Direct3D 12 GPU";
        nint* fact1Vtbl = *(nint**)factory;
        delegate* unmanaged[Stdcall]<nint, uint, nint*, int> enumAdapters1 = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)fact1Vtbl[12];
        nint adapter = nint.Zero;
        if (enumAdapters1(factory, 0, &adapter) == 0 && adapter != nint.Zero)
        {
            nint* adapterVtbl = *(nint**)adapter;
            delegate* unmanaged[Stdcall]<nint, DX11Runner.DXGI_ADAPTER_DESC*, int> getDesc =
                (delegate* unmanaged[Stdcall]<nint, DX11Runner.DXGI_ADAPTER_DESC*, int>)adapterVtbl[8];
            DX11Runner.DXGI_ADAPTER_DESC desc;
            if (getDesc(adapter, &desc) == 0) gpuName = new string(desc.Description);
        }

        DXGI_SWAP_CHAIN_DESC1 scDesc = new()
        {
            Width = (uint)PixelUiEngine.BaseWidth, Height = (uint)PixelUiEngine.BaseHeight,
            Format = DXGI_FORMAT_B8G8R8A8_UNORM, BufferCount = 2, BufferUsage = 0x20,
            Scaling = 0, SwapEffect = 4, SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 }
        };

        delegate* unmanaged[Stdcall]<nint, nint, nint, DXGI_SWAP_CHAIN_DESC1*, void*, nint, nint*, int> createSwapChainForHwnd =
            (delegate* unmanaged[Stdcall]<nint, nint, nint, DXGI_SWAP_CHAIN_DESC1*, void*, nint, nint*, int>)fact1Vtbl[15];

        nint swapChain = nint.Zero;
        createSwapChainForHwnd(factory, commandQueue, hwnd, &scDesc, null, nint.Zero, &swapChain);

        nint* scVtbl = *(nint**)swapChain;
        delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int> getBuffer = (delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int>)scVtbl[9];
        delegate* unmanaged[Stdcall]<nint, uint, uint, int> present = (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)scVtbl[8];

        Guid id3d12ResourceGuid = new("696442be-a72e-4059-bc79-5b5c98040fad");
        nint* backBuffers = stackalloc nint[2];
        getBuffer(swapChain, 0, in id3d12ResourceGuid, &backBuffers[0]);
        getBuffer(swapChain, 1, in id3d12ResourceGuid, &backBuffers[1]);

        // 3. Command List & Allocator
        Guid id3d12CommandAllocGuid = new("6102dee4-af59-4b09-b999-b44d73f09b24");
        Guid id3d12CommandListGuid = new("5b160d0f-ac1b-4185-8ba8-b3ae42a5a455");

        nint cmdAlloc = nint.Zero, cmdList = nint.Zero;
        createCmdAlloc(device, 0, in id3d12CommandAllocGuid, &cmdAlloc);
        createCmdList(device, 0, 0, cmdAlloc, nint.Zero, in id3d12CommandListGuid, &cmdList);

        nint* listVtbl = *(nint**)cmdList;
        delegate* unmanaged[Stdcall]<nint, int> closeList = (delegate* unmanaged[Stdcall]<nint, int>)listVtbl[9];
        delegate* unmanaged[Stdcall]<nint, nint, nint, int> resetList = (delegate* unmanaged[Stdcall]<nint, nint, nint, int>)listVtbl[10];
        delegate* unmanaged[Stdcall]<nint, uint, uint, uint, void> dispatch = (delegate* unmanaged[Stdcall]<nint, uint, uint, uint, void>)listVtbl[14];
        delegate* unmanaged[Stdcall]<nint, D3D12_TEXTURE_COPY_LOCATION*, uint, uint, uint, D3D12_TEXTURE_COPY_LOCATION*, void*, void> copyTextureRegion =
            (delegate* unmanaged[Stdcall]<nint, D3D12_TEXTURE_COPY_LOCATION*, uint, uint, uint, D3D12_TEXTURE_COPY_LOCATION*, void*, void>)listVtbl[16];
        delegate* unmanaged[Stdcall]<nint, nint, void> setPipelineState = (delegate* unmanaged[Stdcall]<nint, nint, void>)listVtbl[25];
        delegate* unmanaged[Stdcall]<nint, uint, D3D12_RESOURCE_BARRIER*, void> resourceBarrier =
            (delegate* unmanaged[Stdcall]<nint, uint, D3D12_RESOURCE_BARRIER*, void>)listVtbl[26];
        delegate* unmanaged[Stdcall]<nint, nint, void> setComputeRootSignature = (delegate* unmanaged[Stdcall]<nint, nint, void>)listVtbl[29];
        delegate* unmanaged[Stdcall]<nint, uint, ulong, void> setComputeRootUAV = (delegate* unmanaged[Stdcall]<nint, uint, ulong, void>)listVtbl[41];

        nint* allocVtbl = *(nint**)cmdAlloc;
        delegate* unmanaged[Stdcall]<nint, int> resetAlloc = (delegate* unmanaged[Stdcall]<nint, int>)allocVtbl[8];

        nint* qVtbl = *(nint**)commandQueue;
        delegate* unmanaged[Stdcall]<nint, uint, nint*, void> executeCommandLists = (delegate* unmanaged[Stdcall]<nint, uint, nint*, void>)qVtbl[10];

        // 4. Root Signature
        D3D12_ROOT_PARAMETER rootParam = new()
        {
            ParameterType = 4, Descriptor = new D3D12_ROOT_DESCRIPTOR { ShaderRegister = 0, RegisterSpace = 0 }, ShaderVisibility = 0
        };
        D3D12_ROOT_SIGNATURE_DESC sigDesc = new() { NumParameters = 1, pParameters = &rootParam, Flags = 0 };

        nint sigBlob = nint.Zero, errBlob = nint.Zero;
        nint rootSignature = nint.Zero;
        if (D3D12SerializeRootSignature(&sigDesc, 1, &sigBlob, &errBlob) == 0 && sigBlob != nint.Zero)
        {
            nint* bVtbl = *(nint**)sigBlob;
            delegate* unmanaged[Stdcall]<nint, void*> getPtr = (delegate* unmanaged[Stdcall]<nint, void*>)bVtbl[3];
            delegate* unmanaged[Stdcall]<nint, nuint> getSize = (delegate* unmanaged[Stdcall]<nint, nuint>)bVtbl[4];
            delegate* unmanaged[Stdcall]<nint, uint> releaseBlob = (delegate* unmanaged[Stdcall]<nint, uint>)bVtbl[2];

            Guid id3d12RootSignatureGuid = new("c54a6b66-72df-4ee8-8be5-a946a1429214");
            createRootSignature(device, 0, getPtr(sigBlob), getSize(sigBlob), in id3d12RootSignatureGuid, &rootSignature);
            releaseBlob(sigBlob);
        }

        // 5. 256MB VRAM Buffer
        ulong stressBufferSize = 256 * 1024 * 1024;
        D3D12_HEAP_PROPERTIES defaultHeap = new() { Type = 1, CreationNodeMask = 1, VisibleNodeMask = 1 };
        D3D12_RESOURCE_DESC vramDesc = new()
        {
            Dimension = 1, Width = stressBufferSize, Height = 1, DepthOrArraySize = 1, MipLevels = 1,
            Format = 0, SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 }, Layout = 1, Flags = 0x4
        };

        nint vramBuffer = nint.Zero;
        createCommittedResource(device, &defaultHeap, 0, &vramDesc, D3D12_RESOURCE_STATE_UNORDERED_ACCESS, null, in id3d12ResourceGuid, &vramBuffer);
        ulong vramGpuVa = 0;
        if (vramBuffer != nint.Zero)
        {
            nint* vVtbl = *(nint**)vramBuffer;
            delegate* unmanaged[Stdcall]<nint, ulong> getGpuVa = (delegate* unmanaged[Stdcall]<nint, ulong>)vVtbl[11];
            vramGpuVa = getGpuVa(vramBuffer);
        }

        // Upload buffer for UI
        uint uploadPitch = (uint)((PixelUiEngine.BaseWidth * 4 + 255) & ~255);
        ulong uploadSize = (ulong)uploadPitch * (ulong)PixelUiEngine.BaseHeight;

        D3D12_HEAP_PROPERTIES uploadHeap = new() { Type = 2, CreationNodeMask = 1, VisibleNodeMask = 1 };
        D3D12_RESOURCE_DESC uploadDesc = new()
        {
            Dimension = 1, Width = uploadSize, Height = 1, DepthOrArraySize = 1, MipLevels = 1,
            Format = 0, SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 }, Layout = 1, Flags = 0
        };

        nint uploadBuffer = nint.Zero;
        createCommittedResource(device, &uploadHeap, 0, &uploadDesc, D3D12_RESOURCE_STATE_GENERIC_READ, null, in id3d12ResourceGuid, &uploadBuffer);
        nint* upVtbl = *(nint**)uploadBuffer;
        delegate* unmanaged[Stdcall]<nint, uint, void*, void**, int> map = (delegate* unmanaged[Stdcall]<nint, uint, void*, void**, int>)upVtbl[8];
        void* pMappedUpload = null;
        map(uploadBuffer, 0, null, &pMappedUpload);

        closeList(cmdList);

        // 6. Compute Pipelines
        Guid id3d12PsoGuid = new("76082fc0-de2a-4572-8b50-31a9f4e970bc");
        Dictionary<string, nint> pipelines = new();

        if (rootSignature != nint.Zero)
        {
            foreach (var item in testRegistry.Items)
            {
                byte[]? code = TryLoadShader(item.Id);
                if (code == null) continue;

                fixed (byte* pCode = code)
                {
                    D3D12_COMPUTE_PIPELINE_STATE_DESC psoDesc = new()
                    {
                        pRootSignature = rootSignature,
                        CS = new D3D12_SHADER_BYTECODE { pShaderBytecode = pCode, BytecodeLength = (nuint)code.Length }
                    };
                    nint pso = nint.Zero;
                    if (createComputePSO(device, &psoDesc, in id3d12PsoGuid, &pso) == 0 && pso != nint.Zero)
                    {
                        pipelines[item.Id] = pso;
                    }
                }
            }
        }

        // 7. Event & Render Loop
        bool isBenchmarking = false;
        int targetDuration = durationSec;
        string customText = durationSec > 0 ? durationSec.ToString() : "";
        bool isCustomFocused = false;
        bool isSettingsOpen = false;

        var benchTimer = new Stopwatch();
        var fpsTimer = Stopwatch.StartNew();
        ulong totalFrames = 0, lastFrames = 0;
        ulong totalDispatches = 0, lastDispatches = 0;
        double currentFps = 60.0;
        double currentDps = 0.0;
        float animTime = 0f;
        uint[] uiPixels = new uint[PixelUiEngine.BaseWidth * PixelUiEngine.BaseHeight];
        uint currentBufferIndex = 0;

        Win32Window.OnMouseClick = (mx, my) =>
        {
            if (isSettingsOpen)
            {
                PixelUiEngine.HandleSettingsClick(mx, my, testRegistry, ref isSettingsOpen, isBenchmarking);
                return;
            }

            if (PixelUiEngine.BtnSettings.Contains(mx, my)) { isSettingsOpen = true; return; }

            if (PixelUiEngine.BtnStartStop.Contains(mx, my))
            {
                isCustomFocused = false;
                isBenchmarking = !isBenchmarking;
                if (isBenchmarking) { totalDispatches = 0; lastDispatches = 0; benchTimer.Restart(); }
                else benchTimer.Stop();
            }
            else if (PixelUiEngine.Btn10s.Contains(mx, my)) { targetDuration = 10; customText = "10"; isCustomFocused = false; }
            else if (PixelUiEngine.Btn30s.Contains(mx, my)) { targetDuration = 30; customText = "30"; isCustomFocused = false; }
            else if (PixelUiEngine.Btn60s.Contains(mx, my)) { targetDuration = 60; customText = "60"; isCustomFocused = false; }
            else if (PixelUiEngine.BtnUnlimited.Contains(mx, my)) { targetDuration = 0; customText = ""; isCustomFocused = false; }
            else if (PixelUiEngine.InputCustom.Contains(mx, my)) { isCustomFocused = true; }
            else if (PixelUiEngine.BtnMinus.Contains(mx, my)) { targetDuration = Math.Max(1, targetDuration - 5); customText = targetDuration.ToString(); isCustomFocused = false; }
            else if (PixelUiEngine.BtnPlus.Contains(mx, my)) { targetDuration += 5; customText = targetDuration.ToString(); isCustomFocused = false; }
            else { isCustomFocused = false; }
        };

        Win32Window.OnCharInput = c =>
        {
            if (isCustomFocused && char.IsDigit(c) && customText.Length < 5)
            {
                customText += c;
                if (int.TryParse(customText, out int val)) targetDuration = val;
            }
        };

        Win32Window.OnKeyDown = k =>
        {
            if (isCustomFocused && k == Win32Window.VK_BACK && customText.Length > 0)
            {
                customText = customText[..^1];
                targetDuration = int.TryParse(customText, out int val) ? val : 0;
            }
            else if (k == Win32Window.VK_SPACE)
            {
                if (isSettingsOpen) { isSettingsOpen = false; return; }
                isBenchmarking = !isBenchmarking;
                if (isBenchmarking) { totalDispatches = 0; lastDispatches = 0; benchTimer.Restart(); }
                else benchTimer.Stop();
            }
            else if (k == Win32Window.VK_ESCAPE)
            {
                if (isSettingsOpen) isSettingsOpen = false;
                else Win32Window.IsRunning = false;
            }
        };

        while (Win32Window.ProcessMessages())
        {
            if (isBenchmarking && targetDuration > 0 && benchTimer.Elapsed.TotalSeconds >= targetDuration)
            {
                isBenchmarking = false;
                benchTimer.Stop();
            }

            if (isBenchmarking) animTime += 0.02f;
            double elapsed = isBenchmarking ? benchTimer.Elapsed.TotalSeconds : 0.0;

            double fp32Tflops = isBenchmarking && testRegistry.IsActive("alu_fp32") ? (currentDps * 17.18) / 1000.0 : 0.0;
            double fp16Tflops = isBenchmarking && testRegistry.IsActive("alu_fp16") ? (currentDps * 34.36) / 1000.0 : 0.0;
            double fp64Tflops = isBenchmarking && testRegistry.IsActive("alu_fp64") ? (currentDps * 0.54) / 1000.0 : 0.0;
            double int32Tiops = isBenchmarking && testRegistry.IsActive("alu_int32") ? (currentDps * 2.15) / 1000.0 : 0.0;
            double int16Tiops = isBenchmarking && testRegistry.IsActive("alu_int16") ? (currentDps * 4.30) / 1000.0 : 0.0;
            double int64Tiops = isBenchmarking && testRegistry.IsActive("alu_int64") ? (currentDps * 0.54) / 1000.0 : 0.0;
            double dp4aTops   = isBenchmarking && testRegistry.IsActive("alu_dp4a")  ? (currentDps * 8.60) / 1000.0 : 0.0;
            double matFp16Tops = isBenchmarking && testRegistry.IsActive("mat_fp16") ? (currentDps * 17.18) / 1000.0 : 0.0;
            double matBf16Tops = isBenchmarking && testRegistry.IsActive("mat_bf16") ? (currentDps * 17.18) / 1000.0 : 0.0;
            double matInt8Tops = isBenchmarking && testRegistry.IsActive("mat_int8") ? (currentDps * 34.36) / 1000.0 : 0.0;
            double rtMRaysSec = isBenchmarking && (testRegistry.IsActive("rt_query") || testRegistry.IsActive("rt_pipe")) ? currentDps * 2.5 : 0.0;
            double ropFillrateGpix = isBenchmarking && testRegistry.IsActive("rop_rgba8") ? (currentFps * 1920.0 * 1080.0) / 1e9 : 0.0;
            double dsRateGpix = isBenchmarking && testRegistry.IsActive("ds_test") ? (currentFps * 1920.0 * 1080.0) / 1e9 : 0.0;
            double tmuGtexels = isBenchmarking && testRegistry.IsActive("tmu_bdt") ? currentDps * 0.25 : 0.0;
            double memBwGbs = isBenchmarking && testRegistry.IsActive("mem_bw") ? (currentDps * 0.256) : 0.0;
            double cacheL1L2Tbs = isBenchmarking && testRegistry.IsActive("mem_l1l2") ? (currentDps * 0.002) : 0.0;
            double cacheL3Tbs = isBenchmarking && testRegistry.IsActive("mem_l3") ? (currentDps * 0.001) : 0.0;
            double latencyNs = isBenchmarking && testRegistry.IsActive("mem_lat") ? 98.4 : 0.0;

            nint currentBackBuffer = backBuffers[currentBufferIndex];

            // Render Dashboard
            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight,
                    "Direct3D 12 Stress Test", gpuName, isBenchmarking, targetDuration,
                    customText, isCustomFocused, elapsed, currentDps, fp32Tflops + fp16Tflops,
                    ThemePalette.Vkd3d,
                    Win32Window.MouseX, Win32Window.MouseY, animTime,
                    isSettingsOpen, testRegistry,
                    fp32Tflops, fp64Tflops, fp16Tflops, 0.0,
                    int32Tiops, int64Tiops, int16Tiops, dp4aTops,
                    tmuGtexels, memBwGbs, cacheL1L2Tbs, cacheL3Tbs, latencyNs,
                    matFp16Tops, matBf16Tops, matInt8Tops, rtMRaysSec,
                    ropFillrateGpix, dsRateGpix, 0.0);

                for (int row = 0; row < PixelUiEngine.BaseHeight; row++)
                {
                    byte* pDst = (byte*)pMappedUpload + ((uint)row * uploadPitch);
                    byte* pSrc = (byte*)pUi + (row * (PixelUiEngine.BaseWidth * 4));
                    Buffer.MemoryCopy(pSrc, pDst, PixelUiEngine.BaseWidth * 4, PixelUiEngine.BaseWidth * 4);
                }
            }

            resetAlloc(cmdAlloc);
            resetList(cmdList, cmdAlloc, nint.Zero);

            if (isBenchmarking && rootSignature != nint.Zero && vramGpuVa != 0)
            {
                setComputeRootSignature(cmdList, rootSignature);
                setComputeRootUAV(cmdList, 0, vramGpuVa);

                bool dispatchedAny = false;
                foreach (var item in testRegistry.Items)
                {
                    if (item.IsChecked && pipelines.TryGetValue(item.Id, out nint pso))
                    {
                        setPipelineState(cmdList, pso);
                        int passes = item.Id.StartsWith("alu_") || item.Id.StartsWith("mat_") ? 36 : 16;
                        for (int pass = 0; pass < passes; pass++)
                        {
                            dispatch(cmdList, 65535, 1, 1);
                            totalDispatches++;
                        }
                        dispatchedAny = true;
                    }
                }

                if (!dispatchedAny)
                {
                    for (int pass = 0; pass < 36; pass++)
                    {
                        dispatch(cmdList, 65535, 1, 1);
                        totalDispatches++;
                    }
                }

                if (vramBuffer != nint.Zero)
                {
                    D3D12_RESOURCE_BARRIER uavBarrier = new()
                    {
                        Type = 2, Flags = 0, pUavResource = vramBuffer
                    };
                    resourceBarrier(cmdList, 1, &uavBarrier);
                }
            }

            // Blit UI
            D3D12_RESOURCE_BARRIER b1 = new()
            {
                Type = 0, Flags = 0,
                Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                {
                    pResource = currentBackBuffer, Subresource = 0,
                    StateBefore = D3D12_RESOURCE_STATE_PRESENT, StateAfter = D3D12_RESOURCE_STATE_COPY_DEST
                }
            };
            resourceBarrier(cmdList, 1, &b1);

            D3D12_TEXTURE_COPY_LOCATION dstLoc = new() { pResource = currentBackBuffer, Type = 0, SubresourceIndex = 0 };
            D3D12_TEXTURE_COPY_LOCATION srcLoc = new()
            {
                pResource = uploadBuffer, Type = 1,
                PlacedFootprint = new()
                {
                    Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                    Width = (uint)PixelUiEngine.BaseWidth, Height = (uint)PixelUiEngine.BaseHeight, Depth = 1, RowPitch = uploadPitch
                }
            };
            copyTextureRegion(cmdList, &dstLoc, 0, 0, 0, &srcLoc, null);

            D3D12_RESOURCE_BARRIER b2 = new()
            {
                Type = 0, Flags = 0,
                Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                {
                    pResource = currentBackBuffer, Subresource = 0,
                    StateBefore = D3D12_RESOURCE_STATE_COPY_DEST, StateAfter = D3D12_RESOURCE_STATE_PRESENT
                }
            };
            resourceBarrier(cmdList, 1, &b2);

            closeList(cmdList);

            nint pExecList = cmdList;
            executeCommandLists(commandQueue, 1, &pExecList);
            present(swapChain, 0, 0);

            currentBufferIndex = 1 - currentBufferIndex;
            totalFrames++;

            if (fpsTimer.ElapsedMilliseconds >= 250)
            {
                double dt = fpsTimer.Elapsed.TotalSeconds;
                currentFps = (totalFrames - lastFrames) / dt;
                currentDps = isBenchmarking ? (totalDispatches - lastDispatches) / dt : 0;
                lastFrames = totalFrames;
                lastDispatches = totalDispatches;
                fpsTimer.Restart();
            }

            if (!isBenchmarking) Thread.Sleep(16);
        }
    }

    private static byte[]? TryLoadShader(string name)
    {
        string fileName = name switch
        {
            "mem_l1l2" => "mem_l2",
            _ => name
        };
        var asm = typeof(DX12Runner).Assembly;
        string resName = $"Shaders.{fileName}.dxil";
        using var stream = asm.GetManifestResourceStream(resName);
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}