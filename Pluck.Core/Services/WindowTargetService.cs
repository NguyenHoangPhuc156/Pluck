using Pluck.Core.Native;

namespace Pluck.Core.Services;

/// <summary>
/// Resolves external window targets for paste and drop operations while excluding Pluck windows.
/// </summary>
public static class WindowTargetService
{
    private static readonly uint PluckProcessId = (uint)Environment.ProcessId;

    /// <summary>
    /// Determines whether a window belongs to the current Pluck process.
    /// </summary>
    /// <param name="hwnd">The window handle to inspect.</param>
    /// <returns><see langword="true"/> if the window is owned by Pluck; otherwise, <see langword="false"/>.</returns>
    public static bool IsPluckWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == PluckProcessId;
    }

    /// <summary>
    /// Finds the topmost visible non-Pluck root window under a screen point (Z-order walk).
    /// Works after Pluck windows are hidden — does not use WindowFromPoint alone.
    /// </summary>
    /// <param name="screenX">Horizontal screen coordinate.</param>
    /// <param name="screenY">Vertical screen coordinate.</param>
    /// <returns>The root window handle, or <see cref="IntPtr.Zero"/> if none is found.</returns>
    public static IntPtr FindExternalWindowAtPoint(int screenX, int screenY)
    {
        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
                return true;

            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root == IntPtr.Zero || IsPluckWindow(root))
                return true;

            if (!NativeMethods.GetWindowRect(root, out var rect))
                return true;

            if (screenX < rect.Left || screenX > rect.Right ||
                screenY < rect.Top || screenY > rect.Bottom)
                return true;

            found = root;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>
    /// Finds an external target window at a screen point, retrying after optional hit-test preparation.
    /// </summary>
    /// <param name="screenX">Horizontal screen coordinate.</param>
    /// <param name="screenY">Vertical screen coordinate.</param>
    /// <param name="prepareHitTest">Optional action invoked before each lookup attempt (for example, hiding overlay windows).</param>
    /// <returns>The root window handle, or <see cref="IntPtr.Zero"/> if none is found after all attempts.</returns>
    public static IntPtr FindTargetWindowAtPoint(int screenX, int screenY, Action? prepareHitTest = null)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            prepareHitTest?.Invoke();
            if (attempt > 0)
                Thread.Sleep(80);

            var root = FindExternalWindowAtPoint(screenX, screenY);
            if (root != IntPtr.Zero)
                return root;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Returns the root window of the current foreground application when it is not Pluck.
    /// </summary>
    /// <returns>The external foreground root window handle, or <see cref="IntPtr.Zero"/> if unavailable.</returns>
    public static IntPtr FindLastExternalForegroundWindow()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg != IntPtr.Zero && !IsPluckWindow(fg))
            return NativeMethods.GetAncestor(fg, NativeMethods.GA_ROOT);

        return IntPtr.Zero;
    }
}
