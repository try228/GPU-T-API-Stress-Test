using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Payloads.D3D;

public static unsafe partial class Win32Window
{
    public const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int WS_VISIBLE = 0x10000000;
    public const int PM_REMOVE = 0x0001;
    public const int WM_QUIT = 0x0012;
    public const int WM_DESTROY = 0x0002;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_CHAR = 0x0102;
    public const int WM_SETCURSOR = 0x0020;

    public const int VK_SPACE = 0x20;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_BACK = 0x08;

    public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public char* lpszMenuName;
        public char* lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    // --- Native Win32 API imports ---

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
    public static partial nint GetModuleHandleW(void* lpModuleName);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClassExW(WNDCLASSEXW* lpwcx);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, void* lpParam);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PeekMessageW(MSG* lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    public static partial int TranslateMessage(MSG* lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial nint DispatchMessageW(MSG* lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage")]
    public static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    public static partial nint LoadCursorW(nint hInstance, nint lpCursorName);

    [LibraryImport("user32.dll", EntryPoint = "SetCursor")]
    public static partial nint SetCursor(nint hCursor);

    public static int MouseX = 0;
    public static int MouseY = 0;
    public static bool IsRunning = true;
    public static Action<int, int>? OnMouseClick;
    public static Action<char>? OnCharInput;
    public static Action<int>? OnKeyDown;

    private static WndProc? s_wndProc;
    private static nint s_defaultCursor;

    public static nint Create(string title, int width, int height)
    {
        nint hInstance = GetModuleHandleW(null);
        string className = "GPU_T_D3D_Class";
        s_defaultCursor = LoadCursorW(nint.Zero, (nint)32512); // IDC_ARROW

        s_wndProc = (hWnd, msg, wParam, lParam) =>
        {
            switch (msg)
            {
                case WM_SETCURSOR:
                    SetCursor(s_defaultCursor);
                    return 1;

                case WM_MOUSEMOVE:
                    MouseX = (short)(lParam & 0xFFFF);
                    MouseY = (short)((lParam >> 16) & 0xFFFF);
                    return 0;

                case WM_LBUTTONDOWN:
                    int mx = (short)(lParam & 0xFFFF);
                    int my = (short)((lParam >> 16) & 0xFFFF);
                    OnMouseClick?.Invoke(mx, my);
                    return 0;

                case WM_CHAR:
                    OnCharInput?.Invoke((char)wParam);
                    return 0;

                case WM_KEYDOWN:
                    OnKeyDown?.Invoke((int)wParam);
                    return 0;

                case WM_DESTROY:
                    IsRunning = false;
                    PostQuitMessage(0);
                    return 0;
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        };

        fixed (char* pClassName = className)
        {
            WNDCLASSEXW wc = new()
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = hInstance,
                hCursor = s_defaultCursor,
                lpszClassName = pClassName
            };
            RegisterClassExW(&wc);
        }

        nint hwnd = CreateWindowExW(
            0, className, title,
            WS_OVERLAPPEDWINDOW | WS_VISIBLE,
            100, 100, width, height,
            nint.Zero, nint.Zero, hInstance, null);

        if (hwnd == nint.Zero)
            throw new InvalidOperationException($"CreateWindowExW failed with Win32 Error: {Marshal.GetLastWin32Error()}");

        return hwnd;
    }

    public static bool ProcessMessages()
    {
        MSG msg;
        while (PeekMessageW(&msg, nint.Zero, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT) return false;
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        return IsRunning;
    }
}