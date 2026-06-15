using System.Windows;
using Pluck.Core.Services;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.Services;
using Pluck.UI.Views;

namespace Pluck.UI;

public sealed class PluckAppHost : IDisposable
{
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
            onSettings: () => _mainDialog.ShowSettingsTab(),
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
        Instance = this;
    }

    public static void Initialize() => _ = new PluckAppHost();

    public PluckSettings Settings => _settings;

    public void UpdateSettings(PluckSettings settings)
    {
        _settings = settings ?? new PluckSettings();
        _bubbleManager.ApplySettings(_settings);
        RegisterHotkey();
        ApplyStartupSetting();
    }

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

    public void PasteItem(ClipboardItem item)
    {
        PluckWindowGuard.Instance.RunHidden(() =>
        {
            if (Pluck.Core.Native.NativeMethods.GetCursorPos(out var pt))
                PasteService.Instance.PasteToPoint(pt.X, pt.Y, item);
        });
    }

    public void CopyItemToClipboard(ClipboardItem item) =>
        PasteService.Instance.CopyToClipboard(item);

    public void RemoveBubbleForItem(long itemId) => _bubbleManager.RemoveByItemId(itemId);

    public void NotifyHistoryPinChanged(long itemId, bool pinned) =>
        _bubbleManager.SetPinnedByItemId(itemId, pinned);

    private void OnClipboardChanged(object? sender, EventArgs e)
    {
        var (hwnd, path, name) = SourceAppDetector.CaptureForeground();
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            _captureService.ProcessClipboardUpdate(hwnd, name, path));
    }

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

    private void ClearAllHistory()
    {
        if (System.Windows.MessageBox.Show("Clear all clipboard history?", "Pluck",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _repository.ClearAll();
        _mainDialog.RefreshHistory();
    }

    private void Shutdown()
    {
        if (_settings.ClearHistoryOnExit)
            _repository.ClearAll();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _clipboardMonitor.Dispose();
        _hotkeyService.Dispose();
        _bubbleManager.Dispose();
        _tray.Dispose();
        _repository.Dispose();
    }
}
