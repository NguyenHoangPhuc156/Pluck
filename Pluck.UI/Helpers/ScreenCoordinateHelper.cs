using System.Windows;
using System.Windows.Media;

namespace Pluck.UI.Helpers;

internal static class ScreenCoordinateHelper
{
    /// <summary>
    /// Win32 GetCursorPos (physical pixels) → WPF screen DIP for Window.Left/Top.
    /// Uses the visual's monitor DPI via CompositionTarget.TransformFromDevice.
    /// </summary>
    public static Point PhysicalScreenToDip(Point physical, Visual relativeTo)
    {
        var source = PresentationSource.FromVisual(relativeTo);
        if (source?.CompositionTarget is null)
            return physical;

        return source.CompositionTarget.TransformFromDevice.Transform(physical);
    }

    /// <summary>
    /// Screen DIP top-left of a visual → canvas coordinates inside a host window.
    /// </summary>
    public static Point ScreenDipToCanvas(Point screenDip, Visual hostWindow)
    {
        return hostWindow.PointFromScreen(screenDip);
    }
}
