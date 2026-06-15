using System.Windows;
using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.UI.Services;

public sealed class PluckWindowGuard
{
    public static PluckWindowGuard Instance { get; } = new();

    private readonly List<Window> _windows = new();

    public void Register(Window window)
    {
        if (!_windows.Contains(window))
            _windows.Add(window);
    }

    public void Unregister(Window window) => _windows.Remove(window);

    public void RunHidden(Action action)
    {
        var hidden = new List<IntPtr>();
        foreach (var w in _windows.ToArray())
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero)
                continue;
            if (!w.IsVisible)
                continue;
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
            hidden.Add(hwnd);
        }

        try
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Thread.Sleep(150);
            action();
        }
        finally
        {
            foreach (var hwnd in hidden)
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNA);
        }
    }
}
