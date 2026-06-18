using System.Windows;
using Pluck.Core.Services;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.Services;
using Pluck.UI.Views;

namespace Pluck.UI;

/// <summary>
/// Central composition root for Pluck UI services, clipboard monitoring, and user-facing windows.
/// </summary>
public sealed class PluckAppHost : IDisposable
{
    /// <summary>
    /// Gets the singleton host instance created during application startup.
    /// </summary>
    public static PluckAppHost Instance { get; private set; } = null!;

    private readonly SettingsStore _settingsStore;
    private readonly ClipboardRepository _repository;
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly ClipboardCaptureService _captureService;
    private readonly TrayIconService _tray;
    private readonly MainDialog _mainDialog;
    private readonly BubbleManager _bubbleManager;
    private readonly GlobalHotkeyService _hotkeyService;
    private PluckSettings _settings;

    /// <summary>
    /// Initializes core services, wires event handlers, and starts clipboard monitoring.
    /// </summary>
    private PluckAppHost()
    {
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _repository = new ClipboardRepository(historyLimit: _settings.HistoryLimit);
        _captureService = new ClipboardCaptureService(_repository);
        _clipboardMonitor = new ClipboardMonitor();
        _bubbleManager = new BubbleManager(_repository);
        _bubbleManager.ApplySettings(_settings);

        _mainDialog = new MainDialog(_repository, _settingsStore, _settings);
        _mainDialog.Hide();
        PluckWindowGuard.Instance.Register(_mainDialog);

        _tray = new TrayIconService(
            onOpen: ToggleMainDialog,
            onSettings: () =>
            {
                _mainDialog.Show();
                _mainDialog.ShowSettingsPanel();
            },
            onClearAll: ClearAllHistory,
            onExit: Shutdown);

        _hotkeyService = new GlobalHotkeyService();
        try { _hotkeyService.Register(_settings.GlobalHotkey, ToggleMainDialog); }
        catch { _hotkeyService.Register("Ctrl+Shift+V", ToggleMainDialog); }

        ApplyStartupSetting();

        _clipboardMonitor.ClipboardChanged += OnClipboardChanged;
        _captureService.ItemCaptured += (_, item) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _bubbleManager.AddBubble(item);
                _mainDialog.RefreshHistory();
            });
        };

        _clipboardMonitor.Start();

        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () =>
            {
                PasteService.Instance.PrewarmClipboard();
                _bubbleManager.PrewarmPasteDrag();
            });

        Instance = this;
    }

    /// <summary>
    /// Creates and initializes the singleton application host.
    /// </summary>
    public static void Initialize() => _ = new PluckAppHost();

    /// <summary>
    /// Gets the current application settings snapshot.
    /// </summary>
    public PluckSettings Settings => _settings;

    /// <summary>
    /// Applies new settings to bubbles, the main dialog, hotkeys, and startup registration.
    /// </summary>
    /// <param name="settings">Updated settings; a default instance is used when <paramref name="settings"/> is null.</param>
    public void UpdateSettings(PluckSettings settings)
    {
        _settings = settings ?? new PluckSettings();
        _bubbleManager.ApplySettings(_settings);
        _mainDialog.ApplySettings(_settings);
        RegisterHotkey();
        ApplyStartupSetting();
    }

    /// <summary>
    /// Shows or hides the main history dialog.
    /// </summary>
    public void ToggleMainDialog()
    {
        if (_mainDialog.IsVisible)
            _mainDialog.Hide();
        else
        {
            _mainDialog.RefreshHistory();
            _mainDialog.Show();
            _mainDialog.Activate();
        }
    }

    /// <summary>
    /// Pastes a clipboard item at the current cursor position while Pluck windows are hidden.
    /// </summary>
    /// <param name="item">The clipboard item to paste.</param>
    public void PasteItem(ClipboardItem item)
    {
        PluckWindowGuard.Instance.RunHidden(() =>
        {
            if (Pluck.Core.Native.NativeMethods.GetCursorPos(out var pt))
                PasteService.Instance.PasteToPoint(pt.X, pt.Y, item);
        });
    }

    /// <summary>
    /// Copies a history item back to the system clipboard.
    /// </summary>
    /// <param name="item">The clipboard item to copy.</param>
    public void CopyItemToClipboard(ClipboardItem item) =>
        PasteService.Instance.CopyToClipboard(item);

    /// <summary>
    /// Removes any on-screen bubble associated with the given history item.
    /// </summary>
    /// <param name="itemId">Database identifier of the history item.</param>
    public void RemoveBubbleForItem(long itemId) => _bubbleManager.RemoveByItemId(itemId);

    /// <summary>
    /// Creates or refreshes a bubble for the given clipboard item.
    /// </summary>
    /// <param name="item">The clipboard item to display as a bubble.</param>
    public void ShowBubbleForItem(ClipboardItem item) => _bubbleManager.AddBubble(item);

    /// <summary>
    /// Synchronizes bubble pin state when history pin status changes.
    /// </summary>
    /// <param name="itemId">Database identifier of the history item.</param>
    /// <param name="pinned">Whether the item is pinned.</param>
    public void NotifyHistoryPinChanged(long itemId, bool pinned) =>
        _bubbleManager.SetPinnedByItemId(itemId, pinned);

    /// <summary>
    /// Handles clipboard change notifications by capturing foreground context and processing the update.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event data.</param>
    private void OnClipboardChanged(object? sender, EventArgs e)
    {
        var (hwnd, path, name) = SourceAppDetector.CaptureForeground();
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            _captureService.ProcessClipboardUpdate(hwnd, name, path));
    }

    /// <summary>
    /// Re-registers the global hotkey from current settings, falling back to the default chord on failure.
    /// </summary>
    private void RegisterHotkey()
    {
        try
        {
            _hotkeyService.Update(_settings.GlobalHotkey);
        }
        catch
        {
            try { _hotkeyService.Register("Ctrl+Shift+V", ToggleMainDialog); }
            catch { /* hotkey in use */ }
        }
    }

    /// <summary>
    /// Updates Windows startup registration according to the launch-at-startup setting.
    /// </summary>
    private void ApplyStartupSetting()
    {
        try
        {
            StartupService.SetEnabled(_settings.LaunchAtStartup);
        }
        catch
        {
            // ignore registry errors
        }
    }

    /// <summary>
    /// Prompts the user and clears all clipboard history when confirmed.
    /// </summary>
    private void ClearAllHistory()
    {
        if (System.Windows.MessageBox.Show("Clear all clipboard history?", "Pluck",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _repository.ClearAll();
        _mainDialog.RefreshHistory();
    }

    /// <summary>
    /// Optionally clears history on exit and shuts down the WPF application.
    /// </summary>
    private void Shutdown()
    {
        if (_settings.ClearHistoryOnExit)
            _repository.ClearAll();
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Releases clipboard monitoring, hotkeys, bubbles, tray icon, and repository resources.
    /// </summary>
    public void Dispose()
    {
        _clipboardMonitor.Dispose();
        _hotkeyService.Dispose();
        _bubbleManager.Dispose();
        _tray.Dispose();
        _repository.Dispose();
    }
}
