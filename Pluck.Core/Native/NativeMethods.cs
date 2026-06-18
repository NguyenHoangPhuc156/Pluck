using System.Runtime.InteropServices;

namespace Pluck.Core.Native;

/// <summary>
/// P/Invoke declarations and Win32 constants used by Pluck core services.
/// </summary>
public static class NativeMethods
{
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_PASTE = 0x0302;
    public const int EM_REPLACESEL = 0x00C2;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const uint SW_RESTORE = 9;
    public const uint SW_HIDE = 0;
    public const uint SW_SHOWNA = 8;
    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint CWP_SKIPINVISIBLE = 0x0001;
    public const uint CWP_SKIPTRANSPARENT = 0x0004;
    public const int ASFW_ANY = -1;

    /// <summary>
    /// Registers the specified window to receive clipboard format change notifications.
    /// </summary>
    /// <param name="hwnd">Handle of the window to register.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    /// <summary>
    /// Removes a window from the clipboard format listener list.
    /// </summary>
    /// <param name="hwnd">Handle of the window to unregister.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    /// <summary>
    /// Retrieves a handle to the foreground window.
    /// </summary>
    /// <returns>The foreground window handle, or <see cref="IntPtr.Zero"/> if none exists.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Brings the specified window to the foreground and activates it.
    /// </summary>
    /// <param name="hWnd">Handle of the window to activate.</param>
    /// <returns><see langword="true"/> if the window was brought to the foreground; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Sets the keyboard focus to the specified window.
    /// </summary>
    /// <param name="hWnd">Handle of the window that receives focus.</param>
    /// <returns>The handle of the window that previously had focus.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    /// <summary>
    /// Brings the specified window to the top of the Z order.
    /// </summary>
    /// <param name="hWnd">Handle of the window to bring to the top.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>
    /// Allows the specified process to set the foreground window.
    /// </summary>
    /// <param name="dwProcessId">Process identifier, or <see cref="ASFW_ANY"/> to allow any process.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int dwProcessId);

    /// <summary>
    /// Sets the show state of a window.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nCmdShow">Show-state command such as <see cref="SW_RESTORE"/>.</param>
    /// <returns><see langword="true"/> if the window was previously visible; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    /// <summary>
    /// Determines whether the specified window is minimized (iconic).
    /// </summary>
    /// <param name="hWnd">Handle of the window to test.</param>
    /// <returns><see langword="true"/> if the window is minimized; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    /// <summary>
    /// Retrieves a handle to the window that contains the specified point.
    /// </summary>
    /// <param name="point">The point to test, in screen coordinates.</param>
    /// <returns>The window handle under the point.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    /// <summary>
    /// Retrieves a handle to the child window at the specified point, with optional skipping flags.
    /// </summary>
    /// <param name="hWndParent">Handle of the parent window.</param>
    /// <param name="pt">The point to test, in client coordinates of the parent.</param>
    /// <param name="uFlags">Flags indicating which child windows to skip.</param>
    /// <returns>The child window handle, or the parent handle if no child is found.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, POINT pt, uint uFlags);

    /// <summary>
    /// Retrieves the cursor's position in screen coordinates.
    /// </summary>
    /// <param name="lpPoint">Receives the cursor position when the call succeeds.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    /// <summary>
    /// Moves the cursor to the specified screen coordinates.
    /// </summary>
    /// <param name="x">The new x-coordinate in screen coordinates.</param>
    /// <param name="y">The new y-coordinate in screen coordinates.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    /// <summary>
    /// Converts screen coordinates of a point to client coordinates relative to a window.
    /// </summary>
    /// <param name="hWnd">Handle of the window for the conversion.</param>
    /// <param name="lpPoint">On input, screen coordinates; on output, client coordinates.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    /// <summary>
    /// Retrieves the bounding rectangle of a window in screen coordinates.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="lpRect">Receives the window rectangle when the call succeeds.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Determines whether the specified window is visible.
    /// </summary>
    /// <param name="hWnd">Handle of the window to test.</param>
    /// <returns><see langword="true"/> if the window is visible; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Retrieves the ancestor of the specified window.
    /// </summary>
    /// <param name="hwnd">Handle of the window whose ancestor is requested.</param>
    /// <param name="gaFlags">The ancestor type to retrieve, such as <see cref="GA_ROOT"/>.</param>
    /// <returns>The ancestor window handle, or <see cref="IntPtr.Zero"/> if none exists.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    /// <summary>
    /// Retrieves the class name of the specified window.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="lpClassName">Buffer that receives the class name.</param>
    /// <param name="nMaxCount">Size of the buffer, in characters.</param>
    /// <returns>The number of characters copied to the buffer, not including the terminating null character.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    /// <summary>
    /// Sends a string message to a window.
    /// </summary>
    /// <param name="hWnd">Handle of the destination window.</param>
    /// <param name="msg">The message to send.</param>
    /// <param name="wParam">Additional message-specific information.</param>
    /// <param name="lParam">Additional message-specific information as a string.</param>
    /// <returns>The result of the message processing.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string? lParam);

    /// <summary>
    /// Sends a message to a window.
    /// </summary>
    /// <param name="hWnd">Handle of the destination window.</param>
    /// <param name="msg">The message to send.</param>
    /// <param name="wParam">Additional message-specific information.</param>
    /// <param name="lParam">Additional message-specific information.</param>
    /// <returns>The result of the message processing.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Captures mouse input to the specified window.
    /// </summary>
    /// <param name="hWnd">Handle of the window that receives mouse capture.</param>
    /// <returns>The handle of the window that previously captured the mouse.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr SetCapture(IntPtr hWnd);

    /// <summary>
    /// Releases mouse capture from a window.
    /// </summary>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    /// <summary>
    /// Retrieves the asynchronous key state for the specified virtual key.
    /// </summary>
    /// <param name="vKey">Virtual-key code to test.</param>
    /// <returns>The high-order bit is set if the key is down.</returns>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public const int WM_NCHITTEST = 0x0084;
    public const int HTCLIENT = 1;
    public const int HTTRANSPARENT = -1;
    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;
    public const int VK_MBUTTON = 0x04;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOCOPYBITS = 0x0100;

    /// <summary>
    /// Retrieves window information for 32-bit processes.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nIndex">The zero-based offset to the value to retrieve.</param>
    /// <returns>The requested 32-bit value.</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    /// <summary>
    /// Retrieves window information for 64-bit processes.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nIndex">The zero-based offset to the value to retrieve.</param>
    /// <returns>The requested value.</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    /// <summary>
    /// Sets window information for 32-bit processes.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nIndex">The zero-based offset to the value to set.</param>
    /// <param name="dwNewLong">The replacement value.</param>
    /// <returns>The previous value of the specified offset.</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// Sets window information for 64-bit processes.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nIndex">The zero-based offset to the value to set.</param>
    /// <param name="dwNewLong">The replacement value.</param>
    /// <returns>The previous value of the specified offset.</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// Retrieves window information, selecting the correct API for the current process bitness.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nIndex">The zero-based offset to the value to retrieve.</param>
    /// <returns>The requested value as a 32-bit integer.</returns>
    public static int GetWindowLong(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8
            ? (int)GetWindowLongPtr64(hWnd, nIndex)
            : GetWindowLong32(hWnd, nIndex);

    /// <summary>
    /// Sets window information, selecting the correct API for the current process bitness.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="nIndex">The zero-based offset to the value to set.</param>
    /// <param name="dwNewLong">The replacement value.</param>
    public static void SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hWnd, nIndex, new IntPtr(dwNewLong));
        else
            SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    /// <summary>
    /// Retrieves a handle to the display monitor that contains a point.
    /// </summary>
    /// <param name="pt">The point to test, in device coordinates.</param>
    /// <param name="dwFlags">Determines how the monitor is chosen when the point is not on any display.</param>
    /// <returns>A monitor handle, or <see cref="IntPtr.Zero"/> on failure.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>
    /// Gets the dots per inch (DPI) of a display monitor.
    /// </summary>
    /// <param name="hmonitor">Handle of the monitor.</param>
    /// <param name="dpiType">The type of DPI to retrieve.</param>
    /// <param name="dpiX">Receives the horizontal DPI.</param>
    /// <param name="dpiY">Receives the vertical DPI.</param>
    /// <returns>An HRESULT indicating success or failure.</returns>
    [DllImport("Shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>
    /// Retrieves the DPI for the specified window.
    /// </summary>
    /// <param name="hwnd">Handle of the window.</param>
    /// <returns>The DPI value for the window.</returns>
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>
    /// Changes the size, position, and Z order of a child, pop-up, or top-level window.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="hWndInsertAfter">Handle of the window to precede the positioned window in the Z order.</param>
    /// <param name="x">New position of the left side of the window.</param>
    /// <param name="y">New position of the top of the window.</param>
    /// <param name="cx">New width of the window.</param>
    /// <param name="cy">New height of the window.</param>
    /// <param name="uFlags">Window sizing and positioning flags.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    /// <summary>
    /// Registers a hot key for the specified window.
    /// </summary>
    /// <param name="hWnd">Handle of the window that receives the hot key message.</param>
    /// <param name="id">Identifier of the hot key.</param>
    /// <param name="fsModifiers">Modifier keys for the hot key.</param>
    /// <param name="vk">Virtual-key code of the hot key.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// Unregisters a hot key registered by <see cref="RegisterHotKey"/>.
    /// </summary>
    /// <param name="hWnd">Handle of the window associated with the hot key.</param>
    /// <param name="id">Identifier of the hot key to remove.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Synthesizes keystrokes, mouse motions, and button clicks.
    /// </summary>
    /// <param name="nInputs">Number of structures in the input array.</param>
    /// <param name="pInputs">Array of input events.</param>
    /// <param name="cbSize">Size of an <see cref="INPUT"/> structure, in bytes.</param>
    /// <returns>The number of events successfully inserted into the input stream.</returns>
    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Retrieves the thread identifier and process identifier of the thread that created the specified window.
    /// </summary>
    /// <param name="hWnd">Handle of the window.</param>
    /// <param name="lpdwProcessId">Receives the process identifier.</param>
    /// <returns>The thread identifier.</returns>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Attaches or detaches the input processing mechanism of one thread to another.
    /// </summary>
    /// <param name="idAttach">Identifier of the thread to attach.</param>
    /// <param name="idAttachTo">Identifier of the thread to receive input from <paramref name="idAttach"/>.</param>
    /// <param name="fAttach"><see langword="true"/> to attach; <see langword="false"/> to detach.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    /// <summary>
    /// Opens an existing local process object.
    /// </summary>
    /// <param name="dwDesiredAccess">Access rights requested for the process object.</param>
    /// <param name="bInheritHandle">Whether child processes inherit the returned handle.</param>
    /// <param name="dwProcessId">Identifier of the process to open.</param>
    /// <returns>A process handle, or <see cref="IntPtr.Zero"/> on failure.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    /// <summary>
    /// Retrieves the full executable path of a process.
    /// </summary>
    /// <param name="hProcess">Handle to the process.</param>
    /// <param name="dwFlags">Reserved; must be zero.</param>
    /// <param name="lpExeName">Buffer that receives the executable path.</param>
    /// <param name="lpdwSize">On input, buffer size in characters; on output, number of characters written.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, System.Text.StringBuilder lpExeName, ref int lpdwSize);

    /// <summary>
    /// Closes an open object handle.
    /// </summary>
    /// <param name="hObject">Handle to the object to close.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Callback delegate used by <see cref="EnumChildWindows"/>.
    /// </summary>
    /// <param name="hwnd">Handle to a child window.</param>
    /// <param name="lParam">Application-defined value passed to the callback.</param>
    /// <returns><see langword="true"/> to continue enumeration; <see langword="false"/> to stop.</returns>
    public delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

    /// <summary>
    /// Enumerates the child windows of a parent window by passing each handle to an application-defined callback.
    /// </summary>
    /// <param name="hWndParent">Handle of the parent window whose children are enumerated.</param>
    /// <param name="lpEnumFunc">Application-defined callback function.</param>
    /// <param name="lParam">Application-defined value passed to the callback.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    /// <summary>
    /// Callback delegate used by <see cref="EnumWindows"/>.
    /// </summary>
    /// <param name="hwnd">Handle to a top-level window.</param>
    /// <param name="lParam">Application-defined value passed to the callback.</param>
    /// <returns><see langword="true"/> to continue enumeration; <see langword="false"/> to stop.</returns>
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    /// <summary>
    /// Enumerates all top-level windows on the screen by passing each handle to an application-defined callback.
    /// </summary>
    /// <param name="lpEnumFunc">Application-defined callback function.</param>
    /// <param name="lParam">Application-defined value passed to the callback.</param>
    /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint GA_ROOT = 2;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    /// <summary>
    /// Defines the x- and y-coordinates of a point.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Defines the coordinates of the upper-left and lower-right corners of a rectangle.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Contains information for synthesizing input events via <see cref="SendInput"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    /// <summary>
    /// Union of mouse and keyboard input payloads used by <see cref="INPUT"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mi;
        [FieldOffset(0)] public KEYBDINPUT Ki;
    }

    /// <summary>
    /// Contains information about a synthesized mouse event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>
    /// Contains information about a synthesized keyboard event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
