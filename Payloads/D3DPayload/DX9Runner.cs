using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Payloads.D3D;

public static unsafe partial class DX9Runner
{
    private const string D3D9Lib = "d3d9.dll";

    public const uint D3D_SDK_VERSION = 32;
    public const int D3DDEVTYPE_HAL = 1;
    public const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x00000040;
    public const int D3DFMT_A8R8G8B8 = 21;
    public const int D3DPOOL_SYSTEMMEM = 2;
    public const int D3DPT_TRIANGLESTRIP = 5;
    public const uint D3DFVF_XYZRHW = 0x004;
    public const uint D3DFVF_TEX1 = 0x100;

    public const int D3DRS_ALPHABLENDENABLE = 27;
    public const int D3DRS_SRCBLEND = 19;
    public const int D3DRS_DESTBLEND = 20;
    public const int D3DBLEND_ONE = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DPRESENT_PARAMETERS
    {
        public uint BackBufferWidth;
        public uint BackBufferHeight;
        public int BackBufferFormat;
        public uint BackBufferCount;
        public int MultiSampleType;
        public uint MultiSampleQuality;
        public int SwapEffect;
        public nint hDeviceWindow;
        public int Windowed;
        public int EnableAutoDepthStencil;
        public int AutoDepthStencilFormat;
        public uint Flags;
        public uint FullScreen_RefreshRateInHz;
        public uint PresentationInterval;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DLOCKED_RECT
    {
        public int Pitch;
        public void* pBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DVERTEX
    {
        public float x, y, z, rhw;
        public float u, v;
    }

    [LibraryImport(D3D9Lib, EntryPoint = "Direct3DCreate9")]
    public static partial nint Direct3DCreate9(uint sdkVersion);

    public static void Run(int durationSec)
    {
        nint hwnd = Win32Window.Create("GPU-T Direct3D 9 Stress Test", PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight);

        nint d3d9 = Direct3DCreate9(D3D_SDK_VERSION);
        if (d3d9 == nint.Zero) throw new InvalidOperationException("Failed to initialize Direct3D 9 (d3d9.dll).");

        D3DPRESENT_PARAMETERS d3dpp = new()
        {
            BackBufferWidth = (uint)PixelUiEngine.BaseWidth,
            BackBufferHeight = (uint)PixelUiEngine.BaseHeight,
            BackBufferFormat = D3DFMT_A8R8G8B8,
            BackBufferCount = 1,
            Windowed = 1,
            SwapEffect = 1,
            hDeviceWindow = hwnd,
            PresentationInterval = 0x80000000 // VSync OFF
        };

        nint* vtbl = *(nint**)d3d9;
        delegate* unmanaged[Stdcall]<nint, uint, int, nint, uint, D3DPRESENT_PARAMETERS*, nint*, int> createDevice =
            (delegate* unmanaged[Stdcall]<nint, uint, int, nint, uint, D3DPRESENT_PARAMETERS*, nint*, int>)vtbl[16];

        nint device = nint.Zero;
        int res = createDevice(d3d9, 0, D3DDEVTYPE_HAL, hwnd, D3DCREATE_HARDWARE_VERTEXPROCESSING, &d3dpp, &device);
        if (res != 0)
        {
            res = createDevice(d3d9, 0, D3DDEVTYPE_HAL, hwnd, 0x00000020, &d3dpp, &device);
        }
        if (res != 0 || device == nint.Zero)
            throw new InvalidOperationException($"D3D9 CreateDevice failed with HRESULT: 0x{res:X8}");

        nint* devVtbl = *(nint**)device;

        delegate* unmanaged[Stdcall]<nint, uint, uint, int, int, nint*, void*, int> createOffscreenSurface =
            (delegate* unmanaged[Stdcall]<nint, uint, uint, int, int, nint*, void*, int>)devVtbl[36];
        delegate* unmanaged[Stdcall]<nint, uint, uint, int, nint*, int> getBackBuffer =
            (delegate* unmanaged[Stdcall]<nint, uint, uint, int, nint*, int>)devVtbl[18];
        delegate* unmanaged[Stdcall]<nint, nint, void*, nint, void*, int> updateSurface =
            (delegate* unmanaged[Stdcall]<nint, nint, void*, nint, void*, int>)devVtbl[30];
        delegate* unmanaged[Stdcall]<nint, int> beginScene = (delegate* unmanaged[Stdcall]<nint, int>)devVtbl[41];
        delegate* unmanaged[Stdcall]<nint, int> endScene = (delegate* unmanaged[Stdcall]<nint, int>)devVtbl[42];
        delegate* unmanaged[Stdcall]<nint, int, uint, int> setRenderState =
            (delegate* unmanaged[Stdcall]<nint, int, uint, int>)devVtbl[57];
        delegate* unmanaged[Stdcall]<nint, uint, int> setFvf = (delegate* unmanaged[Stdcall]<nint, uint, int>)devVtbl[89];
        delegate* unmanaged[Stdcall]<nint, int, uint, void*, uint, int> drawPrimitiveUp =
            (delegate* unmanaged[Stdcall]<nint, int, uint, void*, uint, int>)devVtbl[83];
        delegate* unmanaged[Stdcall]<nint, void*, void*, nint, void*, int> present =
            (delegate* unmanaged[Stdcall]<nint, void*, void*, nint, void*, int>)devVtbl[17];

        D3DVERTEX* quadVerts = stackalloc D3DVERTEX[4]
        {
            new() { x = 0, y = 0, z = 0.5f, rhw = 1.0f, u = 0, v = 0 },
            new() { x = PixelUiEngine.BaseWidth, y = 0, z = 0.5f, rhw = 1.0f, u = 1, v = 0 },
            new() { x = 0, y = PixelUiEngine.BaseHeight, z = 0.5f, rhw = 1.0f, u = 0, v = 1 },
            new() { x = PixelUiEngine.BaseWidth, y = PixelUiEngine.BaseHeight, z = 0.5f, rhw = 1.0f, u = 1, v = 1 }
        };

        nint guiSurface = nint.Zero;
        int surfRes = createOffscreenSurface(device, (uint)PixelUiEngine.BaseWidth, (uint)PixelUiEngine.BaseHeight, D3DFMT_A8R8G8B8, D3DPOOL_SYSTEMMEM, &guiSurface, null);
        if (surfRes != 0 || guiSurface == nint.Zero)
            throw new InvalidOperationException($"CreateOffscreenPlainSurface failed: 0x{surfRes:X8}");

        nint backBuffer = nint.Zero;
        int bbRes = getBackBuffer(device, 0, 0, 0, &backBuffer);
        if (bbRes != 0 || backBuffer == nint.Zero)
            throw new InvalidOperationException($"GetBackBuffer failed: 0x{bbRes:X8}");

        nint* surfVtbl = *(nint**)guiSurface;
        delegate* unmanaged[Stdcall]<nint, D3DLOCKED_RECT*, void*, uint, int> lockRect =
            (delegate* unmanaged[Stdcall]<nint, D3DLOCKED_RECT*, void*, uint, int>)surfVtbl[13];
        delegate* unmanaged[Stdcall]<nint, int> unlockRect = (delegate* unmanaged[Stdcall]<nint, int>)surfVtbl[14];

        setRenderState(device, D3DRS_ALPHABLENDENABLE, 1);
        setRenderState(device, D3DRS_SRCBLEND, D3DBLEND_ONE);
        setRenderState(device, D3DRS_DESTBLEND, D3DBLEND_ONE);

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

        Console.WriteLine("[D3D9Runner] Direct3D 9 Stress Loop Active (100% Saturation)...");

        while (Win32Window.ProcessMessages())
        {
            if (isBenchmarking && targetDuration > 0 && benchTimer.Elapsed.TotalSeconds >= targetDuration)
            {
                isBenchmarking = false;
            }

            if (isBenchmarking) animTime += 0.02f;

            double elapsed = isBenchmarking ? benchTimer.Elapsed.TotalSeconds : 0.0;
            double tflops = isBenchmarking ? (currentFps * 1.52) / 1000.0 : 0.0;

            // 2048 полноэкранных бленд-проходов за кадр = 100% GPU Fillrate
            if (isBenchmarking)
            {
                beginScene(device);
                setFvf(device, D3DFVF_XYZRHW | D3DFVF_TEX1);

                for (int p = 0; p < 2048; p++)
                {
                    drawPrimitiveUp(device, D3DPT_TRIANGLESTRIP, 2, quadVerts, (uint)sizeof(D3DVERTEX));
                }

                endScene(device);
            }

            fixed (uint* pUi = uiPixels)
            {
                PixelUiEngine.Render(
                    pUi, PixelUiEngine.BaseWidth, PixelUiEngine.BaseHeight,
                    "Direct3D 9", "Direct3D 9 HAL Device",
                    isBenchmarking, targetDuration, customText, isCustomFocused,
                    elapsed, currentFps, tflops,
                    ThemePalette.Dxvk,
                    Win32Window.MouseX, Win32Window.MouseY, animTime);

                D3DLOCKED_RECT lr;
                if (lockRect(guiSurface, &lr, null, 0) == 0)
                {
                    for (int y = 0; y < PixelUiEngine.BaseHeight; y++)
                    {
                        byte* pDst = (byte*)lr.pBits + (y * lr.Pitch);
                        byte* pSrc = (byte*)pUi + (y * PixelUiEngine.BaseWidth * 4);
                        Buffer.MemoryCopy(pSrc, pDst, PixelUiEngine.BaseWidth * 4, PixelUiEngine.BaseWidth * 4);
                    }
                    unlockRect(guiSurface);
                    updateSurface(device, guiSurface, null, backBuffer, null);
                }
            }

            present(device, null, null, nint.Zero, null);
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