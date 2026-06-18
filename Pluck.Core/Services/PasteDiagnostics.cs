using System.IO;
using System.Text;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

/// <summary>
/// Writes optional paste and drop diagnostics to a local log file for troubleshooting.
/// </summary>
public static class PasteDiagnostics
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pluck",
        "paste-debug.log");

    /// <summary>
    /// Appends a drop-target diagnostic line to the paste debug log.
    /// </summary>
    /// <param name="x">Horizontal screen coordinate of the drop.</param>
    /// <param name="y">Vertical screen coordinate of the drop.</param>
    /// <param name="rootHwnd">The root window handle under the drop point.</param>
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

    /// <summary>
    /// Appends a paste-operation diagnostic line to the paste debug log.
    /// </summary>
    /// <param name="rootHwnd">The target root window handle.</param>
    /// <param name="x">Horizontal screen coordinate used for the paste.</param>
    /// <param name="y">Vertical screen coordinate used for the paste.</param>
    /// <param name="method">A short label describing the paste strategy used.</param>
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

    /// <summary>
    /// Retrieves the Win32 class name of a window for logging purposes.
    /// </summary>
    /// <param name="hwnd">The window handle to query.</param>
    /// <returns>The window class name, or <c>(none)</c> when the handle is zero.</returns>
    private static string GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return "(none)";
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
