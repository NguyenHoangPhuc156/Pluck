using System.Windows;
using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.UI.Services;

/// <summary>
/// Temporarily hides registered Pluck windows so paste and focus operations target external applications.
/// </summary>
public sealed class PluckWindowGuard
{
    /// <summary>
    /// Gets the process-wide window guard singleton.
    /// </summary>
    public static PluckWindowGuard Instance { get; } = new();

    private readonly List<Window> _windows = new();

    /// <summary>
    /// Registers a Pluck window that should be hidden during guarded operations.
    /// </summary>
    /// <param name="window">The WPF window to track.</param>
    public void Register(Window window)
    {
        if (!_windows.Contains(window))
            _windows.Add(window);
    }

    /// <summary>
    /// Removes a window from the guard registry.
    /// </summary>
    /// <param name="window">The WPF window to stop tracking.</param>
    public void Unregister(Window window) => _windows.Remove(window);

    /// <summary>
    /// Hides visible registered windows, runs an action, then restores them without activating.
    /// </summary>
    /// <param name="action">The work to perform while Pluck windows are hidden.</param>
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
                System.Windows.Threading.DispatcherPriority.Send);
            Thread.Sleep(40);
            action();
        }
        finally
        {
            foreach (var hwnd in hidden)
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNA);
        }
    }
}
