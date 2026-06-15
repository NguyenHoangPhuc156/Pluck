using System.Drawing;
using System.IO;
using System.Text;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

public static class SourceAppDetector
{
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
