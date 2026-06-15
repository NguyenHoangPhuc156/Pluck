using System.Text;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

public static class PasteTargetResolver
{
    private static readonly string[] EditClassNames =
    [
        "Edit",
        "RichEdit20W",
        "RichEdit50W",
        "RichEditD2DPT",
        "RICHEDIT",
        "Scintilla",
        "ThunderRT6TextBox",
        "Windows.UI.Core.CoreWindow" // sometimes host
    ];

    public static IntPtr FindRootWindowAtPoint(int screenX, int screenY) =>
        WindowTargetService.FindExternalWindowAtPoint(screenX, screenY);

    /// <summary>Finds the deepest child (usually the text editor) at a screen point.</summary>
    public static IntPtr FindPasteTargetHwnd(IntPtr rootHwnd, int screenX, int screenY)
    {
        if (rootHwnd == IntPtr.Zero)
            return IntPtr.Zero;

        var pt = new NativeMethods.POINT { X = screenX, Y = screenY };
        NativeMethods.ScreenToClient(rootHwnd, ref pt);

        var flags = NativeMethods.CWP_SKIPINVISIBLE | NativeMethods.CWP_SKIPTRANSPARENT;
        var direct = NativeMethods.ChildWindowFromPointEx(rootHwnd, pt, flags);
        if (direct != IntPtr.Zero && direct != rootHwnd && IsEditLike(direct))
            return direct;

        if (direct != IntPtr.Zero && direct != rootHwnd)
            return direct;

        var best = IntPtr.Zero;
        EnumEditChildren(rootHwnd, screenX, screenY, ref best);
        return best != IntPtr.Zero ? best : rootHwnd;
    }

    private static void EnumEditChildren(IntPtr parent, int screenX, int screenY, ref IntPtr best)
    {
        var children = new List<IntPtr>();
        EnumChildrenRecursive(parent, children);

        foreach (var child in children)
        {
            if (!IsEditLike(child))
                continue;
            if (!NativeMethods.GetWindowRect(child, out var rect))
                continue;

            if (screenX >= rect.Left && screenX <= rect.Right &&
                screenY >= rect.Top && screenY <= rect.Bottom)
            {
                best = child;
                return;
            }
        }
    }

    private static void EnumChildrenRecursive(IntPtr parent, List<IntPtr> list)
    {
        NativeMethods.EnumChildProc callback = (hwnd, _) =>
        {
            list.Add(hwnd);
            EnumChildrenRecursive(hwnd, list);
            return true;
        };
        NativeMethods.EnumChildWindows(parent, callback, IntPtr.Zero);
    }

    public static bool IsPasteableEditHwnd(IntPtr hwnd) => IsEditLike(hwnd);

    private static bool IsEditLike(IntPtr hwnd)
    {
        var cls = GetClassName(hwnd);
        if (EditClassNames.Any(c => cls.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return true;

        return cls.Contains("Edit", StringComparison.OrdinalIgnoreCase)
               || cls.Contains("RichEdit", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

}
