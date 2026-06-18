using System.Windows;
using Pluck.UI.Helpers;

namespace Pluck.UI.Services;

/// <summary>
/// Manages the system tray icon, context menu, and optional balloon notifications.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly Action _onOpen;
    private readonly Action _onSettings;
    private readonly Action _onClearAll;
    private readonly Action _onExit;

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

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Pluck — Floating Clipboard",
            Icon = IconHelper.CreateTrayIcon(),
            Visible = true
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                _onOpen();
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => _onOpen());
        menu.Items.Add("Settings", null, (_, _) => _onSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Clear All", null, (_, _) => _onClearAll());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _onExit());
        _notifyIcon.ContextMenuStrip = menu;
    }

    /// <summary>
    /// Shows a short informational balloon tip above the tray icon.
    /// </summary>
    /// <param name="title">Balloon title text.</param>
    /// <param name="message">Balloon body text.</param>
    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(2000, title, message, System.Windows.Forms.ToolTipIcon.Info);
    }

    /// <summary>
    /// Hides and disposes the tray icon.
    /// </summary>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
