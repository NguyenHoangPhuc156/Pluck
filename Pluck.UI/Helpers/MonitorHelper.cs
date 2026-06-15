using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Pluck.UI.Helpers;

namespace Pluck.UI.Helpers;

internal static class MonitorHelper
{
    /// <summary>Primary monitor stack anchor (screen DIP, bubble top-left).</summary>
    public static Point GetPrimaryBubbleStackScreenDip(
        Visual dpiVisual,
        double bubbleWidth,
        double rightPadding,
        double topPadding)
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var wa = screen.WorkingArea;
        var topRightDip = ScreenCoordinateHelper.PhysicalScreenToDip(new Point(wa.Right, wa.Top), dpiVisual);
        var topLeftDip = ScreenCoordinateHelper.PhysicalScreenToDip(new Point(wa.Left, wa.Top), dpiVisual);
        return new Point(topRightDip.X - bubbleWidth - rightPadding, topLeftDip.Y + topPadding);
    }

    /// <summary>Primary monitor work area in WPF screen DIP.</summary>
    public static Rect GetPrimaryWorkAreaDip(Visual dpiVisual)
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var wa = screen.WorkingArea;

        var topLeft = ScreenCoordinateHelper.PhysicalScreenToDip(
            new Point(wa.Left, wa.Top),
            dpiVisual);
        var bottomRight = ScreenCoordinateHelper.PhysicalScreenToDip(
            new Point(wa.Right, wa.Bottom),
            dpiVisual);

        return new Rect(topLeft, bottomRight);
    }
}
