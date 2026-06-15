using System.Windows;
using Pluck.UI.Helpers;

namespace Pluck.UI.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly Action _onOpen;
    private readonly Action _onSettings;
    private readonly Action _onClearAll;
    private readonly Action _onExit;

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

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(2000, title, message, System.Windows.Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
