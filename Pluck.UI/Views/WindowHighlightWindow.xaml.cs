using System.Windows;
using Pluck.Core.Native;

namespace Pluck.UI.Views;

public partial class WindowHighlightWindow : Window
{
    public WindowHighlightWindow()
    {
        InitializeComponent();
    }

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

    public void HideHighlight() => Hide();
}
