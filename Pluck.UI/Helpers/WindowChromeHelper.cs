using System.Windows;
using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.UI.Helpers;

internal static class WindowChromeHelper
{
    /// <summary>Keep utility overlays out of the Alt+Tab switcher.</summary>
    public static void HideFromAltTab(Window window)
    {
        if (window.IsLoaded)
            Apply(window);
        else
            window.SourceInitialized += (_, _) => Apply(window);
    }

    private static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TOOLWINDOW;
        style &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }
}
