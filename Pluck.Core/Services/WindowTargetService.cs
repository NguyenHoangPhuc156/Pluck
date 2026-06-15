using Pluck.Core.Native;

namespace Pluck.Core.Services;

public static class WindowTargetService
{
    private static readonly uint PluckProcessId = (uint)Environment.ProcessId;

    public static bool IsPluckWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == PluckProcessId;
    }

    /// <summary>
    /// Topmost visible non-Pluck root window under a screen point (Z-order walk).
    /// Works after Pluck windows are hidden — does not use WindowFromPoint alone.
    /// </summary>
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

    public static IntPtr FindLastExternalForegroundWindow()
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg != IntPtr.Zero && !IsPluckWindow(fg))
            return NativeMethods.GetAncestor(fg, NativeMethods.GA_ROOT);

        return IntPtr.Zero;
    }
}
