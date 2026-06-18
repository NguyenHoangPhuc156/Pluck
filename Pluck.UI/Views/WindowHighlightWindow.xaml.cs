using System.Windows;
using Pluck.Core.Native;
using Pluck.UI.Helpers;

namespace Pluck.UI.Views;

/// <summary>
/// Transparent overlay that outlines a target window during paste or focus highlighting.
/// </summary>
public partial class WindowHighlightWindow : Window
{
    /// <summary>
    /// Initializes the highlight window and hides it from the Alt+Tab switcher.
    /// </summary>
    public WindowHighlightWindow()
    {
        InitializeComponent();
        WindowChromeHelper.HideFromAltTab(this);
    }

    /// <summary>
    /// Positions and shows the highlight rectangle to match a native window's bounds.
    /// </summary>
    /// <param name="hwnd">Handle of the window to highlight.</param>
    public void ShowForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            Hide();
            return;
        }

        Left = rect.Left;
        Top = rect.Top;
        Width = Math.Max(1, rect.Right - rect.Left);
        Height = Math.Max(1, rect.Bottom - rect.Top);
        if (!IsVisible)
            Show();
    }

    /// <summary>
    /// Hides the highlight overlay without closing the window.
    /// </summary>
    public void HideHighlight() => Hide();
}
