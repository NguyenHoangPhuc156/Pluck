using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Pluck.Core.Native;
using Pluck.UI.Helpers;

namespace Pluck.UI.Services;

/// <summary>
/// Manages the system tray icon and context menu via the Shell notification area API.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int TrayIconId = 1;

    private readonly Action _onOpen;
    private readonly Action _onSettings;
    private readonly Action _onClearAll;
    private readonly Action _onExit;
    private readonly HwndSource _messageSource;
    private readonly IntPtr _iconHandle;
    private bool _disposed;

    /// <summary>
    /// Creates the tray icon and wires menu actions to host callbacks.
    /// </summary>
    /// <param name="onOpen">Invoked when the user opens Pluck from the tray.</param>
    /// <param name="onSettings">Invoked when the user chooses Settings from the tray menu.</param>
    /// <param name="onClearAll">Invoked when the user chooses Clear All from the tray menu.</param>
    /// <param name="onExit">Invoked when the user chooses Exit from the tray menu.</param>
    public TrayIconService(Action onOpen, Action onSettings, Action onClearAll, Action onExit)
    {
        _onOpen = onOpen;
        _onSettings = onSettings;
        _onClearAll = onClearAll;
        _onExit = onExit;

        _messageSource = CreateMessageWindow();
        _messageSource.AddHook(WndProc);
        _iconHandle = IconHelper.LoadTrayIconHandle();

        var data = CreateNotifyIconData(_messageSource.Handle, _iconHandle);
        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data))
            throw new InvalidOperationException("Could not create the tray icon.");

        data.uVersion = NativeMethods.NOTIFYICON_VERSION_4;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_SETVERSION, ref data);
    }

    /// <summary>
    /// Shows a short informational balloon tip above the tray icon.
    /// </summary>
    /// <param name="title">Balloon title text.</param>
    /// <param name="message">Balloon body text.</param>
    /// <remarks>Balloon tips are not shown in the native tray implementation.</remarks>
    public void ShowBalloon(string title, string message)
    {
    }

    /// <summary>
    /// Removes the tray icon and disposes native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        var data = CreateNotifyIconData(_messageSource.Handle, _iconHandle);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);

        if (_iconHandle != IntPtr.Zero)
            NativeMethods.DestroyIcon(_iconHandle);

        _messageSource.RemoveHook(WndProc);
        _messageSource.Dispose();
    }

    /// <summary>
    /// Creates a message-only window that receives tray icon callbacks.
    /// </summary>
    /// <returns>The HWND source for the hidden message window.</returns>
    private static HwndSource CreateMessageWindow()
    {
        var parameters = new HwndSourceParameters("PluckTrayMessageWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE
        };

        return new HwndSource(parameters);
    }

    /// <summary>
    /// Builds the notification icon data structure for Shell API calls.
    /// </summary>
    /// <param name="hwnd">Handle of the callback window.</param>
    /// <param name="iconHandle">Handle of the tray icon image.</param>
    /// <returns>A populated <see cref="NativeMethods.NOTIFYICONDATA"/> instance.</returns>
    private static NativeMethods.NOTIFYICONDATA CreateNotifyIconData(IntPtr hwnd, IntPtr iconHandle) =>
        new()
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = TrayIconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = iconHandle,
            szTip = "Pluck — Floating Clipboard"
        };

    /// <summary>
    /// Handles tray icon mouse messages and routes them to application actions.
    /// </summary>
    /// <param name="hwnd">Handle of the window receiving the message.</param>
    /// <param name="msg">The message identifier.</param>
    /// <param name="wParam">Additional message information.</param>
    /// <param name="lParam">Additional message information.</param>
    /// <param name="handled">Set to <see langword="true"/> when the message is handled.</param>
    /// <returns>The message result.</returns>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_TRAYICON || wParam.ToInt32() != TrayIconId)
            return IntPtr.Zero;

        switch ((int)lParam)
        {
            case NativeMethods.WM_LBUTTONUP:
                _onOpen();
                handled = true;
                break;
            case NativeMethods.WM_RBUTTONUP:
            case NativeMethods.WM_CONTEXTMENU:
                ShowContextMenu();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Displays the tray context menu at the current cursor position.
    /// </summary>
    private void ShowContextMenu()
    {
        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        var menu = new ContextMenu
        {
            Placement = PlacementMode.AbsolutePoint,
            PlacementRectangle = new Rect(pt.X, pt.Y, 0, 0)
        };

        menu.Items.Add(CreateMenuItem("Open", _onOpen));
        menu.Items.Add(CreateMenuItem("Settings", _onSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Clear All", _onClearAll));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Exit", _onExit));

        menu.IsOpen = true;
    }

    /// <summary>
    /// Creates a menu item that invokes the supplied action when selected.
    /// </summary>
    /// <param name="header">Text shown in the menu.</param>
    /// <param name="action">Action invoked when the item is clicked.</param>
    /// <returns>The configured menu item.</returns>
    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
