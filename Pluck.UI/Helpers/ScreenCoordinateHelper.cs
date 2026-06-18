using System.Windows;
using System.Windows.Media;

namespace Pluck.UI.Helpers;

/// <summary>
/// Converts between physical screen coordinates and WPF device-independent pixels.
/// </summary>
internal static class ScreenCoordinateHelper
{
    /// <summary>
    /// Converts Win32 physical screen coordinates to WPF DIP for window positioning.
    /// </summary>
    /// <param name="physical">Physical screen point from Win32 APIs such as GetCursorPos.</param>
    /// <param name="relativeTo">Visual used to resolve the monitor DPI via CompositionTarget.</param>
    /// <returns>The equivalent point in device-independent pixels.</returns>
    public static Point PhysicalScreenToDip(Point physical, Visual relativeTo)
    {
        var source = PresentationSource.FromVisual(relativeTo);
        if (source?.CompositionTarget is null)
            return physical;

        return source.CompositionTarget.TransformFromDevice.Transform(physical);
    }
}
