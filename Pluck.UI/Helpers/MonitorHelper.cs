using System.Runtime.InteropServices;
using System.Windows;
using Pluck.Core.Native;

namespace Pluck.UI.Helpers;

/// <summary>
/// Converts monitor geometry into overlay canvas coordinates for bubble layout.
/// </summary>
internal static class MonitorHelper
{
    /// <summary>
    /// Computes the primary-monitor stack anchor in canvas coordinates inside the overlay window.
    /// </summary>
    /// <param name="overlayWindow">The full-screen bubble overlay window.</param>
    /// <param name="bubbleWidth">Width of the bubble being positioned, in device-independent pixels.</param>
    /// <param name="rightPadding">Padding from the working-area right edge.</param>
    /// <param name="topPadding">Padding from the working-area top edge.</param>
    /// <returns>The canvas point for the top-left of the primary stack anchor.</returns>
    /// <remarks>
    /// Uses screen-minus-window conversion rather than <see cref="Visual.PointFromScreen"/> to avoid
    /// per-monitor DPI mismatch on span windows.
    /// </remarks>
    public static Point GetPrimaryBubbleStackCanvasPoint(
        Window overlayWindow,
        double bubbleWidth,
        double rightPadding,
        double topPadding)
    {
        var wa = GetPrimaryMonitorWorkArea();

        var dpi = GetDpiForPhysicalPoint(wa.Right - 1, wa.Top + 1);
        var screenRight = wa.Right * 96.0 / dpi;
        var screenTop = wa.Top * 96.0 / dpi;
        var screen = new Point(screenRight - bubbleWidth - rightPadding, screenTop + topPadding);

        return new Point(screen.X - overlayWindow.Left, screen.Y - overlayWindow.Top);
    }

    /// <summary>
    /// Returns the working area rectangle of the primary display monitor in physical pixels.
    /// </summary>
    /// <returns>The primary monitor working area.</returns>
    /// <exception cref="InvalidOperationException">Thrown when monitor information cannot be retrieved.</exception>
    private static NativeMethods.RECT GetPrimaryMonitorWorkArea()
    {
        var pt = new NativeMethods.POINT();
        var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        if (monitor == IntPtr.Zero)
            throw new InvalidOperationException("No primary screen.");

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException("Could not read primary monitor info.");

        return info.rcWork;
    }

    /// <summary>
    /// Returns the effective horizontal DPI for the monitor nearest a physical screen point.
    /// </summary>
    /// <param name="x">Physical X coordinate in pixels.</param>
    /// <param name="y">Physical Y coordinate in pixels.</param>
    /// <returns>Effective DPI for the monitor, or 96 when lookup fails.</returns>
    private static double GetDpiForPhysicalPoint(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero
            && NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
            && dpiX > 0)
            return dpiX;

        return 96;
    }
}
