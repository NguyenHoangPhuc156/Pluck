using System.Drawing;
using System.IO;
using System.Text;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

/// <summary>
/// Captures metadata about the application that owns the foreground window or supplies an icon.
/// </summary>
public static class SourceAppDetector
{
    /// <summary>
    /// Reads the currently foreground window and resolves its owning process path and display name.
    /// </summary>
    /// <returns>A tuple containing the window handle, full process path, and a short display name.</returns>
    public static (IntPtr WindowHandle, string ProcessPath, string DisplayName) CaptureForeground()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return (IntPtr.Zero, "", "Unknown");

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        var path = GetProcessPath(pid);
        var name = string.IsNullOrEmpty(path)
            ? "Unknown"
            : Path.GetFileNameWithoutExtension(path);
        return (hwnd, path, name);
    }

    /// <summary>
    /// Extracts the associated application icon as a PNG byte array.
    /// </summary>
    /// <param name="processPath">Full path to the executable whose icon is requested.</param>
    /// <param name="size">The width and height of the rendered icon in pixels.</param>
    /// <returns>PNG-encoded icon bytes, or <see langword="null"/> when extraction fails.</returns>
    public static byte[]? TryExtractIconPng(string processPath, int size = 32)
    {
        if (string.IsNullOrEmpty(processPath) || !File.Exists(processPath))
            return null;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(processPath);
            if (icon is null)
                return null;

            using var bitmap = new Bitmap(icon.ToBitmap(), new Size(size, size));
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the full image path of a process from its process identifier.
    /// </summary>
    /// <param name="processId">The Windows process ID.</param>
    /// <returns>The full executable path, or an empty string if it cannot be resolved.</returns>
    private static string GetProcessPath(uint processId)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == IntPtr.Zero)
            return "";

        try
        {
            var sb = new StringBuilder(1024);
            var len = sb.Capacity;
            return NativeMethods.QueryFullProcessImageName(handle, 0, sb, ref len)
                ? sb.ToString(0, len)
                : "";
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
