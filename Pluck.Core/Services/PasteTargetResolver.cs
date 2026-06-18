using System.Text;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

/// <summary>
/// Resolves the HWND of a paste target (typically an edit control) from a screen point.
/// </summary>
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

    /// <summary>
    /// Finds the top-level window at a screen point, excluding Pluck windows.
    /// </summary>
    /// <param name="screenX">Horizontal screen coordinate.</param>
    /// <param name="screenY">Vertical screen coordinate.</param>
    /// <returns>The root window handle, or <see cref="IntPtr.Zero"/> if none is found.</returns>
    public static IntPtr FindRootWindowAtPoint(int screenX, int screenY) =>
        WindowTargetService.FindExternalWindowAtPoint(screenX, screenY);

    /// <summary>
    /// Finds the deepest child window (usually the text editor) at a screen point within a root window.
    /// </summary>
    /// <param name="rootHwnd">The root window to search within.</param>
    /// <param name="screenX">Horizontal screen coordinate.</param>
    /// <param name="screenY">Vertical screen coordinate.</param>
    /// <returns>The paste-target window handle, or the root window if no edit control is found.</returns>
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

    /// <summary>
    /// Enumerates edit-like child windows and selects the one containing the given screen point.
    /// </summary>
    /// <param name="parent">The parent window whose descendants are searched.</param>
    /// <param name="screenX">Horizontal screen coordinate.</param>
    /// <param name="screenY">Vertical screen coordinate.</param>
    /// <param name="best">Receives the matching edit window handle when found.</param>
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

    /// <summary>
    /// Recursively collects all descendant window handles of a parent window.
    /// </summary>
    /// <param name="parent">The parent window to enumerate.</param>
    /// <param name="list">The list to which discovered child handles are appended.</param>
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

    /// <summary>
    /// Determines whether the given window handle refers to a pasteable edit control.
    /// </summary>
    /// <param name="hwnd">The window handle to inspect.</param>
    /// <returns><see langword="true"/> if the window is an edit-like control; otherwise, <see langword="false"/>.</returns>
    public static bool IsPasteableEditHwnd(IntPtr hwnd) => IsEditLike(hwnd);

    /// <summary>
    /// Determines whether a window class name indicates an edit or rich-edit control.
    /// </summary>
    /// <param name="hwnd">The window handle whose class name is inspected.</param>
    /// <returns><see langword="true"/> if the window class is edit-like; otherwise, <see langword="false"/>.</returns>
    private static bool IsEditLike(IntPtr hwnd)
    {
        var cls = GetClassName(hwnd);
        if (EditClassNames.Any(c => cls.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return true;

        return cls.Contains("Edit", StringComparison.OrdinalIgnoreCase)
               || cls.Contains("RichEdit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Retrieves the Win32 class name of a window.
    /// </summary>
    /// <param name="hwnd">The window handle to query.</param>
    /// <returns>The window class name, or an empty string if unavailable.</returns>
    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

}
