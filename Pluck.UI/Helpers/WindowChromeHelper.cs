using System.Windows;
using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.UI.Helpers;

/// <summary>
/// Applies native window style tweaks for utility overlay windows.
/// </summary>
internal static class WindowChromeHelper
{
    /// <summary>
    /// Configures a window so it does not appear in the Alt+Tab switcher.
    /// </summary>
    /// <param name="window">The WPF window to modify.</param>
    public static void HideFromAltTab(Window window)
    {
        if (window.IsLoaded)
            Apply(window);
        else
            window.SourceInitialized += (_, _) => Apply(window);
    }

    /// <summary>
    /// Sets extended window styles that mark the window as a tool window rather than an app window.
    /// </summary>
    /// <param name="window">The WPF window whose HWND should be updated.</param>
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
