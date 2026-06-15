using System.IO;
using System.Text;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

public static class PasteDiagnostics
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pluck",
        "paste-debug.log");

    public static void LogDrop(int x, int y, IntPtr rootHwnd)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var cls = GetClassName(rootHwnd);
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] drop=({x},{y}) target=0x{rootHwnd.ToInt64():X} class={cls} pluck={WindowTargetService.IsPluckWindow(rootHwnd)}{Environment.NewLine}";
            File.AppendAllText(LogPath, line, Encoding.UTF8);
        }
        catch
        {
            // ignore
        }
    }

    public static void LogPaste(IntPtr rootHwnd, int x, int y, string method)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var cls = GetClassName(rootHwnd);
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] paste=({x},{y}) target=0x{rootHwnd.ToInt64():X} class={cls} method={method}{Environment.NewLine}";
            File.AppendAllText(LogPath, line, Encoding.UTF8);
        }
        catch
        {
            // ignore
        }
    }

    private static string GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return "(none)";
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
