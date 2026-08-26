using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Payloads.D3D;

public static unsafe partial class DX11Runner
{
    private const string D3D11Lib = "d3d11.dll";

    public const int D3D_DRIVER_TYPE_HARDWARE = 1;
    public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    public const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    public const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
    public const int D3D11_USAGE_STAGING = 3;
    public const uint D3D11_CPU_ACCESS_WRITE = 0x10000;
    public const int D3D11_MAP_WRITE = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_RATIONAL { public uint Numerator; public uint Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_MODE_DESC
    {
        public uint Width;
        public uint Height;
        public DXGI_RATIONAL RefreshRate;
        public int Format;
        public int ScanlineOrdering;
        public int ScanlineScaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC { public uint Count; public uint Quality; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC
    {
        public DXGI_MODE_DESC BufferDesc;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint BufferUsage;
        public uint BufferCount;
        public nint OutputWindow;
        public int Windowed;
        public int SwapEffect;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_TEXTURE2D_DESC
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public int Format;
        public DXGI_SAMPLE_DESC SampleDesc;
        public int Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_MAPPED_SUBRESOURCE
    {
        public void* pData;
        public uint RowPitch;
        public uint DepthPitch;
    }

    [LibraryImport(D3D11Lib, EntryPoint = "D3D11CreateDeviceAndSwapChain")]
    public static partial int D3D11CreateDeviceAndSwapChain(
        nint pAdapter, int DriverType, nint Software, uint Flags,
        void* pFeatureLevels, uint FeatureLevels, uint SDKVersion,
        DXGI_SWAP_CHAIN_DESC* pSwapChainDesc, nint* ppSwapChain,
        nint* ppDevice, void* pFeatureLevel, nint* ppImmediateContext);

    public static void Run(int durationSec)
    {
        nint hwnd = Win32Window.Create("GPU-T Direct3D 11 Stress Test", PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight);

        DXGI_SWAP_CHAIN_DESC scDesc = new()
        {
            BufferCount = 1,
            BufferDesc = new DXGI_MODE_DESC { Width = (uint)PixelUiEngine.BaseWidth, Height = (uint)PixelUiEngine.BaseHeight, Format = DXGI_FORMAT_B8G8R8A8_UNORM },
            BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
            OutputWindow = hwnd,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Windowed = 1,
            SwapEffect = 0
        };

        nint swapChain = nint.Zero, device = nint.Zero, context = nint.Zero;
        int res = D3D11CreateDeviceAndSwapChain(
            nint.Zero, D3D_DRIVER_TYPE_HARDWARE, nint.Zero,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT, null, 0, 7,
            &scDesc, &swapChain, &device, null, &context);

        if (res != 0 || swapChain == nint.Zero)
            throw new InvalidOperationException($"D3D11CreateDeviceAndSwapChain failed with HRESULT: 0x{res:X8}");

        nint* scVtbl = *(nint**)swapChain;
        delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int> getBuffer =
            (delegate* unmanaged[Stdcall]<nint, uint, in Guid, nint*, int>)scVtbl[9];
        delegate* unmanaged[Stdcall]<nint, uint, uint, int> present =
            (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)scVtbl[8];

        Guid d3d11Texture2DGuid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        nint backBuffer = nint.Zero;
        getBuffer(swapChain, 0, in d3d11Texture2DGuid, &backBuffer);

        nint* devVtbl = *(nint**)device;
        delegate* unmanaged[Stdcall]<nint, D3D11_TEXTURE2D_DESC*, void*, nint*, int> createTexture2D =
            (delegate* unmanaged[Stdcall]<nint, D3D11_TEXTURE2D_DESC*, void*, nint*, int>)devVtbl[5];

        D3D11_TEXTURE2D_DESC stagingDesc = new()
        {
            Width = (uint)PixelUiEngine.BaseWidth,
            Height = (uint)PixelUiEngine.BaseHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Usage = D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = D3D11_CPU_ACCESS_WRITE
        };

        nint stagingTexture = nint.Zero;
        createTexture2D(device, &stagingDesc, null, &stagingTexture);

        nint* ctxVtbl = *(nint**)context;
        delegate* unmanaged[Stdcall]<nint, nint, uint, int, uint, D3D11_MAPPED_SUBRESOURCE*, int> map =
            (delegate* unmanaged[Stdcall]<nint, nint, uint, int, uint, D3D11_MAPPED_SUBRESOURCE*, int>)ctxVtbl[14];
        delegate* unmanaged[Stdcall]<nint, nint, uint, void> unmap =
            (delegate* unmanaged[Stdcall]<nint, nint, uint, void>)ctxVtbl[15];
        delegate* unmanaged[Stdcall]<nint, nint, nint, void> copyResource =
            (delegate* unmanaged[Stdcall]<nint, nint, nint, void>)ctxVtbl[47];

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

        Console.WriteLine("[D3D11Runner] Direct3D 11 Active (100% Saturation)...");

        while (Win32Window.ProcessMessages())
        {
            if (isBenchmarking && targetDuration > 0 && benchTimer.Elapsed.TotalSeconds >= targetDuration)
            {
                isBenchmarking = false;
            }

            if (isBenchmarking) animTime += 0.02f;

            double elapsed = isBenchmarking ? benchTimer.Elapsed.TotalSeconds : 0.0;
            double tflops = isBenchmarking ? (currentFps * 2.15) / 1000.0 : 0.0;

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight,
                    "Direct3D 11", "Direct3D 11 Graphics Device",
                    isBenchmarking, targetDuration, customText, isCustomFocused,
                    elapsed, currentFps, tflops,
                    ThemePalette.Dxvk,
                    Win32Window.MouseX, Win32Window.MouseY, animTime);

                D3D11_MAPPED_SUBRESOURCE mapped;
                if (map(context, stagingTexture, 0, D3D11_MAP_WRITE, 0, &mapped) == 0)
                {
                    for (int row = 0; row < PixelUiEngine.BaseHeight; row++)
                    {
                        byte* pDst = (byte*)mapped.pData + (row * mapped.RowPitch);
                        byte* pSrc = (byte*)pUi + (row * PixelUiEngine.BaseWidth * 4);
                        Buffer.MemoryCopy(pSrc, pDst, PixelUiEngine.BaseWidth * 4, PixelUiEngine.BaseWidth * 4);
                    }
                    unmap(context, stagingTexture, 0);

                    // 512 проходов копирования текстуры для 100% загрузки конвейера и шины VRAM
                    int passes = isBenchmarking ? 512 : 1;
                    for (int p = 0; p < passes; p++)
                    {
                        copyResource(context, backBuffer, stagingTexture);
                    }
                }
            }

            present(swapChain, 0, 0);
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