using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPU_T.StressTest.Payloads.D3D;

public static unsafe partial class Program
{
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(nint hWnd, string text, string caption, uint type);

    public static int Main(string[] args)
    {
        // 1. Ловим вообще любые падения процесса (включая неуправляемые)
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            string crashMsg = $"[FATAL UNHANDLED] {e.ExceptionObject}";
            LogAndShowError(crashMsg);
        };

        string targetApi = "dx11";
        int duration = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            if ((arg is "--api" or "-a") && i + 1 < args.Length)
            {
                targetApi = args[++i].ToLowerInvariant();
            }
            else if (arg is "dx9" or "d3d9") targetApi = "dx9";
            else if (arg is "dx12" or "d3d12") targetApi = "dx12";
            else if (arg is "dx11" or "d3d11") targetApi = "dx11";
            else if ((arg is "--duration" or "-d") && i + 1 < args.Length && int.TryParse(args[i + 1], out int d))
            {
                duration = d;
                i++;
            }
        }

        try
        {
            switch (targetApi)
            {
                case "dx9":
                    DX9Runner.Run(duration);
                    break;
                case "dx11":
                    DX11Runner.Run(duration);
                    break;
                case "dx12":
                    DX12Runner.Run(duration);
                    break;
                default:
                    DX11Runner.Run(duration);
                    break;
            }
            return 0;
        }
        catch (Exception ex)
        {
            string detailedError = $"API: {targetApi}\n\nType: {ex.GetType().FullName}\nMessage: {ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                detailedError += $"\n\nInnerException: {ex.InnerException.Message}";
            }

            LogAndShowError(detailedError);
            return 1;
        }
    }

    private static void LogAndShowError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();

        try
        {
            File.WriteAllText("d3d_payload_crash.log", message);
            string desktopLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "d3d_payload_crash.log");
            File.WriteAllText(desktopLog, message);
        }
        catch { }

        // Показываем окно поверх всех окон, чтобы его нельзя было не заметить
        MessageBox(nint.Zero, message, "Direct3D Payload Exception", 0x00000010 /* MB_ICONERROR */ | 0x00040000 /* MB_TOPMOST */);
    }
}