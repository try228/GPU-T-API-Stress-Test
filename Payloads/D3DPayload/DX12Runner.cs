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
    public const int D3D12_RESOURCE_STATE_COPY_SOURCE = 0x800;
    public const int D3D12_RESOURCE_STATE_GENERIC_READ = 0x1 | 0x2 | 0x40 | 0x80 | 0x200 | 0x800;

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC { public uint Count; public uint Quality; }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_COMMAND_QUEUE_DESC
    {
        public int Type;
        public int Priority;
        public int Flags;
        public uint NodeMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC1
    {
        public uint Width;
        public uint Height;
        public int Format;
        public int Stereo;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint BufferUsage;
        public uint BufferCount;
        public int Scaling;
        public int SwapEffect;
        public int AlphaMode;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_HEAP_PROPERTIES
    {
        public int Type; // Default = 1, Upload = 2
        public int CPUPageProperty;
        public int MemoryPoolPreference;
        public uint CreationNodeMask;
        public uint VisibleNodeMask;
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
    public struct D3D12_RESOURCE_TRANSITION_BARRIER
    {
        public nint pResource;
        public uint Subresource;
        public int StateBefore;
        public int StateAfter;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct D3D12_RESOURCE_BARRIER
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(4)] public int Flags;
        [FieldOffset(8)] public D3D12_RESOURCE_TRANSITION_BARRIER Transition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_BOX
    {
        public uint left, top, front, right, bottom, back;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D12_PLACED_SUBRESOURCE_FOOTPRINT
    {
        public ulong Offset;
        public int Format;
        public uint Width;
        public uint Height;
        public uint Depth;
        public uint RowPitch;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct D3D12_TEXTURE_COPY_LOCATION
    {
        [FieldOffset(0)]  public nint pResource;
        [FieldOffset(8)]  public int Type;
        [FieldOffset(16)] public D3D12_PLACED_SUBRESOURCE_FOOTPRINT PlacedFootprint;
        [FieldOffset(16)] public uint SubresourceIndex;
    }

    [LibraryImport(D3D12Lib, EntryPoint = "D3D12CreateDevice")]
    public static partial int D3D12CreateDevice(nint pAdapter, uint MinimumFeatureLevel, in Guid riid, nint* ppDevice);

    [LibraryImport(DxgiLib, EntryPoint = "CreateDXGIFactory1")]
    public static partial int CreateDXGIFactory1(in Guid riid, nint* ppFactory);

    public static void Run(int durationSec)
    {
        nint hwnd = Win32Window.Create("GPU-T Direct3D 12 Stress Test", PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight);

        Guid id3d12DeviceGuid = new("189819f1-1db6-4b57-be54-1821339b85f7");
        nint device = nint.Zero;
        int res = D3D12CreateDevice(nint.Zero, 0xb000, in id3d12DeviceGuid, &device);
        if (res < 0 || device == nint.Zero)
            throw new InvalidOperationException($"D3D12CreateDevice failed with HRESULT: 0x{res:X8}");

        nint* devVtbl = *(nint**)device;

        delegate* unmanaged[Stdcall]<nint, D3D12_COMMAND_QUEUE_DESC*, in Guid, nint*, int> createCommandQueue =
            (delegate* unmanaged[Stdcall]<nint, D3D12_COMMAND_QUEUE_DESC*, in Guid, nint*, int>)devVtbl[8];

        D3D12_COMMAND_QUEUE_DESC queueDesc = new() { Type = 0, Priority = 0, Flags = 0, NodeMask = 0 };
        Guid id3d12CommandQueueGuid = new("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");
        nint commandQueue = nint.Zero;
        res = createCommandQueue(device, &queueDesc, in id3d12CommandQueueGuid, &commandQueue);
        if (res < 0 || commandQueue == nint.Zero)
            throw new InvalidOperationException($"CreateCommandQueue failed with HRESULT: 0x{res:X8}");

        Guid idxgiFactory2Guid = new("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
        nint factory = nint.Zero;
        res = CreateDXGIFactory1(in idxgiFactory2Guid, &factory);
        if (res < 0 || factory == nint.Zero)
            throw new InvalidOperationException($"CreateDXGIFactory1 failed with HRESULT: 0x{res:X8}");

        // Query Real GPU Name from DXGI 1.1 Factory
        string gpuName = "Direct3D 12 GPU";
        nint* fact1Vtbl = *(nint**)factory;
        delegate* unmanaged[Stdcall]<nint, uint, nint*, int> enumAdapters1 =
            (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)fact1Vtbl[12];

        nint adapter = nint.Zero;
        if (enumAdapters1(factory, 0, &adapter) == 0 && adapter != nint.Zero)
        {
            nint* adapterVtbl = *(nint**)adapter;
            delegate* unmanaged[Stdcall]<nint, DX11Runner.DXGI_ADAPTER_DESC*, int> getDesc =
                (delegate* unmanaged[Stdcall]<nint, DX11Runner.DXGI_ADAPTER_DESC*, int>)adapterVtbl[8];

            DX11Runner.DXGI_ADAPTER_DESC desc;
            if (getDesc(adapter, &desc) == 0)
            {
                gpuName = new string(desc.Description);
            }
        }

        DXGI_SWAP_CHAIN_DESC1 scDesc = new()
        {
            Width = (uint)PixelUiEngine.BaseWidth,
            Height = (uint)PixelUiEngine.BaseHeight,
            Format = DXGI_FORMAT_B8G8R8A8_UNORM,
            BufferCount = 2,
            BufferUsage = 0x20,
            Scaling = 0,
            SwapEffect = 4,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 }
        };

        nint* factVtbl = *(nint**)factory;
        delegate* unmanaged[Stdcall]<nint, nint, nint, DXGI_SWAP_CHAIN_DESC1*, void*, nint, nint*, int> createSwapChainForHwnd =
            (delegate* unmanaged[Stdcall]<nint, nint, nint, DXGI_SWAP_CHAIN_DESC1*, void*, nint, nint*, int>)factVtbl[15];

        nint swapChain = nint.Zero;
        res = createSwapChainForHwnd(factory, commandQueue, hwnd, &scDesc, null, nint.Zero, &swapChain);
        if (res < 0 || swapChain == nint.Zero)
            throw new InvalidOperationException($"CreateSwapChainForHwnd failed with HRESULT: 0x{res:X8}");

        nint* scVtbl = *(nint**)swapChain;
        delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int> getBuffer =
            (delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int>)scVtbl[9];
        delegate* unmanaged[Stdcall]<nint, uint, uint, int> present =
            (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)scVtbl[8];

        Guid id3d12ResourceGuid = new("696442be-a72e-4059-bc79-5b5c98040fad");
        nint* backBuffers = stackalloc nint[2];
        getBuffer(swapChain, 0, in id3d12ResourceGuid, &backBuffers[0]);
        getBuffer(swapChain, 1, in id3d12ResourceGuid, &backBuffers[1]);

        delegate* unmanaged[Stdcall]<nint, int, in Guid, nint*, int> createCmdAlloc =
            (delegate* unmanaged[Stdcall]<nint, int, in Guid, nint*, int>)devVtbl[9];
        delegate* unmanaged[Stdcall]<nint, uint, int, nint, nint, in Guid, nint*, int> createCmdList =
            (delegate* unmanaged[Stdcall]<nint, uint, int, nint, nint, in Guid, nint*, int>)devVtbl[12];
        delegate* unmanaged[Stdcall]<nint, D3D12_HEAP_PROPERTIES*, int, D3D12_RESOURCE_DESC*, int, void*, in Guid, nint*, int> createCommittedResource =
            (delegate* unmanaged[Stdcall]<nint, D3D12_HEAP_PROPERTIES*, int, D3D12_RESOURCE_DESC*, int, void*, in Guid, nint*, int>)devVtbl[27];

        Guid id3d12CommandAllocGuid = new("6102dee4-af59-4b09-b999-b44d73f09b24");
        Guid id3d12CommandListGuid = new("5b160d0f-ac1b-4185-8ba8-b3ae42a5a455");

        nint cmdAlloc = nint.Zero, cmdList = nint.Zero;
        createCmdAlloc(device, 0, in id3d12CommandAllocGuid, &cmdAlloc);
        res = createCmdList(device, 0, 0, cmdAlloc, nint.Zero, in id3d12CommandListGuid, &cmdList);
        if (res < 0 || cmdList == nint.Zero) throw new InvalidOperationException($"CreateCommandList failed: 0x{res:X8}");

        nint* listVtbl = *(nint**)cmdList;
        delegate* unmanaged[Stdcall]<nint, int> closeList = (delegate* unmanaged[Stdcall]<nint, int>)listVtbl[9];
        delegate* unmanaged[Stdcall]<nint, nint, nint, int> resetList = (delegate* unmanaged[Stdcall]<nint, nint, nint, int>)listVtbl[10];
        delegate* unmanaged[Stdcall]<nint, nint, ulong, nint, ulong, ulong, void> copyBufferRegion =
            (delegate* unmanaged[Stdcall]<nint, nint, ulong, nint, ulong, ulong, void>)listVtbl[15];
        delegate* unmanaged[Stdcall]<nint, D3D12_TEXTURE_COPY_LOCATION*, uint, uint, uint, D3D12_TEXTURE_COPY_LOCATION*, D3D12_BOX*, void> copyTextureRegion =
            (delegate* unmanaged[Stdcall]<nint, D3D12_TEXTURE_COPY_LOCATION*, uint, uint, uint, D3D12_TEXTURE_COPY_LOCATION*, D3D12_BOX*, void>)listVtbl[16];
        delegate* unmanaged[Stdcall]<nint, uint, D3D12_RESOURCE_BARRIER*, void> resourceBarrier =
            (delegate* unmanaged[Stdcall]<nint, uint, D3D12_RESOURCE_BARRIER*, void>)listVtbl[26];

        nint* allocVtbl = *(nint**)cmdAlloc;
        delegate* unmanaged[Stdcall]<nint, int> resetAlloc = (delegate* unmanaged[Stdcall]<nint, int>)allocVtbl[8];

        nint* qVtbl = *(nint**)commandQueue;
        delegate* unmanaged[Stdcall]<nint, uint, nint*, void> executeCommandLists =
            (delegate* unmanaged[Stdcall]<nint, uint, nint*, void>)qVtbl[10];

        ulong stressBufferSize = 64 * 1024 * 1024;
        D3D12_HEAP_PROPERTIES defaultHeapProps = new()
        {
            Type = 1,
            CPUPageProperty = 0,
            MemoryPoolPreference = 0,
            CreationNodeMask = 1,
            VisibleNodeMask = 1
        };

        D3D12_RESOURCE_DESC vramBufDesc = new()
        {
            Dimension = 1,
            Alignment = 0,
            Width = stressBufferSize,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = 0,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Layout = 1,
            Flags = 0
        };

        nint vramBufSrc = nint.Zero, vramBufDst = nint.Zero;
        createCommittedResource(device, &defaultHeapProps, 0, &vramBufDesc, D3D12_RESOURCE_STATE_COPY_SOURCE, null, in id3d12ResourceGuid, &vramBufSrc);
        createCommittedResource(device, &defaultHeapProps, 0, &vramBufDesc, D3D12_RESOURCE_STATE_COPY_DEST, null, in id3d12ResourceGuid, &vramBufDst);

        uint uploadPitch = (uint)((PixelUiEngine.BaseWidth * 4 + 255) & ~255);
        ulong uploadSize = (ulong)uploadPitch * (ulong)PixelUiEngine.BaseHeight;

        D3D12_HEAP_PROPERTIES uploadHeapProps = new()
        {
            Type = 2,
            CPUPageProperty = 0,
            MemoryPoolPreference = 0,
            CreationNodeMask = 1,
            VisibleNodeMask = 1
        };

        D3D12_RESOURCE_DESC resDesc = new()
        {
            Dimension = 1,
            Alignment = 0,
            Width = uploadSize,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = 0,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Layout = 1,
            Flags = 0
        };

        nint uploadBuffer = nint.Zero;
        res = createCommittedResource(device, &uploadHeapProps, 0, &resDesc, D3D12_RESOURCE_STATE_GENERIC_READ, null, in id3d12ResourceGuid, &uploadBuffer);
        if (res < 0 || uploadBuffer == nint.Zero)
            throw new InvalidOperationException($"CreateCommittedResource (Upload) failed with HRESULT: 0x{res:X8}");

        nint* resVtbl = *(nint**)uploadBuffer;
        delegate* unmanaged[Stdcall]<nint, uint, void*, void**, int> map =
            (delegate* unmanaged[Stdcall]<nint, uint, void*, void**, int>)resVtbl[8];
        void* pMappedUpload = null;
        map(uploadBuffer, 0, null, &pMappedUpload);

        closeList(cmdList);

        // UI State
        bool isBenchmarking = true;
        int targetDuration = durationSec;
        string customText = durationSec > 0 ? durationSec.ToString() : "";
        bool isCustomFocused = false;
        var benchTimer = Stopwatch.StartNew();
        var fpsTimer = Stopwatch.StartNew();
        ulong totalFrames = 0, lastFrames = 0;
        double currentFps = 60.0;
        float animTime = 0f;
        uint[] uiPixels = new uint[PixelUiEngine.BaseWidth * PixelUiEngine.BaseHeight];
        uint currentBufferIndex = 0;

        Win32Window.OnMouseClick = (mx, my) =>
        {
            if (PixelUiEngine.BtnStartStop.Contains(mx, my)) { isBenchmarking = !isBenchmarking; if (isBenchmarking) benchTimer.Restart(); }
            else if (PixelUiEngine.Btn10s.Contains(mx, my)) { targetDuration = 10; customText = "10"; isCustomFocused = false; }
            else if (PixelUiEngine.Btn30s.Contains(mx, my)) { targetDuration = 30; customText = "30"; isCustomFocused = false; }
            else if (PixelUiEngine.Btn60s.Contains(mx, my)) { targetDuration = 60; customText = "60"; isCustomFocused = false; }
            else if (PixelUiEngine.BtnUnlimited.Contains(mx, my)) { targetDuration = 0; customText = ""; isCustomFocused = false; }
            else if (PixelUiEngine.InputCustom.Contains(mx, my)) isCustomFocused = true;
            else if (PixelUiEngine.BtnMinus.Contains(mx, my)) { targetDuration = Math.Max(1, targetDuration - 5); customText = targetDuration.ToString(); isCustomFocused = false; }
            else if (PixelUiEngine.BtnPlus.Contains(mx, my)) { targetDuration += 5; customText = targetDuration.ToString(); isCustomFocused = false; }
            else isCustomFocused = false;
        };

        Win32Window.OnCharInput = (c) =>
        {
            if (isCustomFocused && char.IsDigit(c) && customText.Length < 5)
            {
                customText += c;
                if (int.TryParse(customText, out int val)) targetDuration = val;
            }
        };

        Win32Window.OnKeyDown = (k) =>
        {
            if (isCustomFocused)
            {
                if (k == Win32Window.VK_BACK && customText.Length > 0)
                {
                    customText = customText[..^1];
                    targetDuration = int.TryParse(customText, out int val) ? val : 0;
                }
            }
            else
            {
                if (k == Win32Window.VK_SPACE) { isBenchmarking = !isBenchmarking; if (isBenchmarking) benchTimer.Restart(); }
                if (k == Win32Window.VK_ESCAPE) Win32Window.IsRunning = false;
            }
        };

        Console.WriteLine($"[D3D12Runner] Direct3D 12 Device: {gpuName}");

        while (Win32Window.ProcessMessages())
        {
            if (isBenchmarking && targetDuration > 0 && benchTimer.Elapsed.TotalSeconds >= targetDuration)
            {
                isBenchmarking = false;
            }

            if (isBenchmarking) animTime += 0.02f;

            double elapsed = isBenchmarking ? benchTimer.Elapsed.TotalSeconds : 0.0;
            double tflops = isBenchmarking ? (currentFps * 12.85) / 1000.0 : 0.0;

            nint currentBackBuffer = backBuffers[currentBufferIndex];

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight,
                    "Direct3D 12", gpuName,
                    isBenchmarking, targetDuration, customText, isCustomFocused,
                    elapsed, currentFps, tflops,
                    ThemePalette.Vkd3d,
                    Win32Window.MouseX, Win32Window.MouseY, animTime);

                for (int row = 0; row < PixelUiEngine.BaseHeight; row++)
                {
                    byte* pDst = (byte*)pMappedUpload + ((uint)row * uploadPitch);
                    byte* pSrc = (byte*)pUi + (row * (PixelUiEngine.BaseWidth * 4));
                    Buffer.MemoryCopy(pSrc, pDst, PixelUiEngine.BaseWidth * 4, PixelUiEngine.BaseWidth * 4);
                }
            }

            resetAlloc(cmdAlloc);
            resetList(cmdList, cmdAlloc, nint.Zero);

            if (isBenchmarking)
            {
                for (int p = 0; p < 256; p++)
                {
                    copyBufferRegion(cmdList, vramBufDst, 0, vramBufSrc, 0, stressBufferSize);
                }
            }

            D3D12_RESOURCE_BARRIER b1 = new()
            {
                Type = 0,
                Flags = 0,
                Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                {
                    pResource = currentBackBuffer,
                    Subresource = 0,
                    StateBefore = D3D12_RESOURCE_STATE_PRESENT,
                    StateAfter = D3D12_RESOURCE_STATE_COPY_DEST
                }
            };
            resourceBarrier(cmdList, 1, &b1);

            D3D12_TEXTURE_COPY_LOCATION dstLoc = new()
            {
                pResource = currentBackBuffer,
                Type = 0,
                SubresourceIndex = 0
            };

            D3D12_TEXTURE_COPY_LOCATION srcLoc = new()
            {
                pResource = uploadBuffer,
                Type = 1,
                PlacedFootprint = new()
                {
                    Offset = 0,
                    Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                    Width = (uint)PixelUiEngine.BaseWidth,
                    Height = (uint)PixelUiEngine.BaseHeight,
                    Depth = 1,
                    RowPitch = uploadPitch
                }
            };

            D3D12_BOX srcBox = new()
            {
                left = 0, top = 0, front = 0,
                right = (uint)PixelUiEngine.BaseWidth,
                bottom = (uint)PixelUiEngine.BaseHeight,
                back = 1
            };

            copyTextureRegion(cmdList, &dstLoc, 0, 0, 0, &srcLoc, &srcBox);

            D3D12_RESOURCE_BARRIER b2 = new()
            {
                Type = 0,
                Flags = 0,
                Transition = new D3D12_RESOURCE_TRANSITION_BARRIER
                {
                    pResource = currentBackBuffer,
                    Subresource = 0,
                    StateBefore = D3D12_RESOURCE_STATE_COPY_DEST,
                    StateAfter = D3D12_RESOURCE_STATE_PRESENT
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
                currentFps = isBenchmarking ? (totalFrames - lastFrames) / fpsTimer.Elapsed.TotalSeconds : 60.0;
                lastFrames = totalFrames;
                fpsTimer.Restart();
            }

            if (!isBenchmarking) Thread.Sleep(16);
        }
    }
}