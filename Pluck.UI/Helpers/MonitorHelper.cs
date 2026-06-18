using System.Windows;
using System.Windows.Forms;
using Pluck.Core.Native;

namespace Pluck.UI.Helpers;

internal static class MonitorHelper
{
    /// <summary>
    /// Primary monitor stack anchor in canvas coordinates inside the overlay window.
    /// Uses screen-minus-window (not PointFromScreen) to avoid per-monitor DPI mismatch on span windows.
    /// </summary>
    public static Point GetPrimaryBubbleStackCanvasPoint(
        Window overlayWindow,
        double bubbleWidth,
        double rightPadding,
        double topPadding)
    {
        var wa = Screen.PrimaryScreen?.WorkingArea
                 ?? throw new InvalidOperationException("No primary screen.");

        var dpi = GetDpiForPhysicalPoint(wa.Right - 1, wa.Top + 1);
        var screenRight = wa.Right * 96.0 / dpi;
        var screenTop = wa.Top * 96.0 / dpi;
        var screen = new Point(screenRight - bubbleWidth - rightPadding, screenTop + topPadding);

        return new Point(screen.X - overlayWindow.Left, screen.Y - overlayWindow.Top);
    }

    private static double GetDpiForPhysicalPoint(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero
            && NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _)
            == 0
            && dpiX > 0)
            return dpiX;

        return 96;
    }
}
