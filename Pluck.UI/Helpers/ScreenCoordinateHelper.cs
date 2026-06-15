using System.Windows;
using System.Windows.Media;

namespace Pluck.UI.Helpers;

internal static class ScreenCoordinateHelper
{
    /// <summary>Win32 GetCursorPos (physical pixels) → WPF DIP for Window.Left/Top.</summary>
    public static Point PhysicalScreenToDip(Point physical, Visual relativeTo)
    {
        var source = PresentationSource.FromVisual(relativeTo);
        if (source?.CompositionTarget is null)
            return physical;

        return source.CompositionTarget.TransformFromDevice.Transform(physical);
    }
}
