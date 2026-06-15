using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.Helpers;
using Pluck.UI.Services;
using Pluck.UI.ViewModels;

namespace Pluck.UI.Views;

public partial class MainDialog : Window
{
    private readonly ClipboardRepository _repository;
    private readonly SettingsStore _settingsStore;
    private PluckSettings _settings;
    private readonly DispatcherTimer _searchDebounceTimer;
    private bool _historyFiltersInitialized;
    private SettingsDialog? _settingsDialog;
    private bool _syncingMove;

    public MainDialog(ClipboardRepository repository, SettingsStore settingsStore, PluckSettings settings)
    {
        InitializeComponent();
        _repository = repository;
        _settingsStore = settingsStore;
        _settings = settings;

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            ApplyHistoryFilters();
        };

        Icon = Helpers.IconHelper.LoadAppIconImage();
        InitializeHistoryFilters();
        RefreshHistory();
    }

    public void ShowSettingsPanel()
    {
        Show();
        Activate();
        ToggleSettingsPanel(forceOpen: true);
    }

    public void RefreshHistory()
    {
        RefreshAppFilterOptions();
        ApplyHistoryFilters();
    }

    public void ApplySettings(PluckSettings settings)
    {
        _settings = settings;
        _settingsDialog?.LoadSettings(settings);
    }

    internal void SyncFromSettingsPosition(double settingsLeft, double settingsTop, double settingsWidth, double settingsHeight)
    {
        _syncingMove = true;
        Left = settingsLeft - ActualWidth;
        Top = settingsTop;
        Height = settingsHeight;
        _syncingMove = false;
    }

    private void InitializeHistoryFilters()
    {
        HistoryTypeFilter.ItemsSource = new[]
        {
            new HistoryFilterOption<ClipboardItemType?>("All types", null),
            new HistoryFilterOption<ClipboardItemType?>("Text", ClipboardItemType.Text),
            new HistoryFilterOption<ClipboardItemType?>("Image", ClipboardItemType.Image),
            new HistoryFilterOption<ClipboardItemType?>("Files", ClipboardItemType.Files),
            new HistoryFilterOption<ClipboardItemType?>("Other", ClipboardItemType.Unknown)
        };
        HistoryTypeFilter.SelectedIndex = 0;

        HistoryTimeFilter.ItemsSource = new[]
        {
            new HistoryFilterOption<HistoryTimeRange>("All time", HistoryTimeRange.All),
            new HistoryFilterOption<HistoryTimeRange>("Last 24 hours", HistoryTimeRange.Last24Hours),
            new HistoryFilterOption<HistoryTimeRange>("Today", HistoryTimeRange.Today),
            new HistoryFilterOption<HistoryTimeRange>("Last 7 days", HistoryTimeRange.Last7Days),
            new HistoryFilterOption<HistoryTimeRange>("Last 30 days", HistoryTimeRange.Last30Days)
        };
        HistoryTimeFilter.SelectedIndex = 0;

        RefreshAppFilterOptions();
        _historyFiltersInitialized = true;
    }

    private void RefreshAppFilterOptions()
    {
        var selectedApp = (HistoryAppFilter.SelectedItem as HistoryFilterOption<string?>)?.Value;
        var apps = _repository.GetDistinctSourceApps();

        var options = new List<HistoryFilterOption<string?>>
        {
            new("All apps", null)
        };
        options.AddRange(apps.Select(a => new HistoryFilterOption<string?>(a, a)));

        HistoryAppFilter.ItemsSource = options;
        HistoryAppFilter.SelectedItem = options.FirstOrDefault(o => o.Value == selectedApp) ?? options[0];
    }

    private void ApplyHistoryFilters()
    {
        if (!_historyFiltersInitialized)
            return;

        var criteria = new HistorySearchCriteria
        {
            SearchText = string.IsNullOrWhiteSpace(HistorySearchBox.Text) ? null : HistorySearchBox.Text.Trim(),
            SourceAppName = (HistoryAppFilter.SelectedItem as HistoryFilterOption<string?>)?.Value,
            Type = (HistoryTypeFilter.SelectedItem as HistoryFilterOption<ClipboardItemType?>)?.Value,
            TimeRange = (HistoryTimeFilter.SelectedItem as HistoryFilterOption<HistoryTimeRange>)?.Value
                         ?? HistoryTimeRange.All,
            Limit = Math.Max(_settings.HistoryLimit, 200)
        };

        var items = _repository.Search(criteria)
            .Select(i => new HistoryItemViewModel(i))
            .ToList();

        HistoryList.ItemsSource = items;
        HistoryResultLabel.Text = items.Count == 1 ? "1 item" : $"{items.Count} items";
        PinColumn.Width = items.Any(i => i.IsPinned) ? 22 : 0;
    }

    private void HistoryFilters_Changed(object sender, RoutedEventArgs e)
    {
        if (!_historyFiltersInitialized)
            return;

        if (sender == HistorySearchBox)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
            return;
        }

        ApplyHistoryFilters();
    }

    private void ClearHistoryFilters_Click(object sender, RoutedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        HistorySearchBox.Text = "";
        HistoryAppFilter.SelectedIndex = 0;
        HistoryTypeFilter.SelectedIndex = 0;
        HistoryTimeFilter.SelectedIndex = 0;
        ApplyHistoryFilters();
    }

    private IReadOnlyList<HistoryItemViewModel> GetSelectedItems() =>
        HistoryList.SelectedItems.Cast<HistoryItemViewModel>().ToList();

    private void HistoryList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Keep right-click selection behavior standard; context menu uses current selection.
        if (e.OriginalSource is DependencyObject source)
        {
            var item = FindAncestor<System.Windows.Controls.ListViewItem>(source);
            if (item?.Content is HistoryItemViewModel vm && !item.IsSelected)
            {
                if (Keyboard.Modifiers != ModifierKeys.Control)
                    HistoryList.SelectedItems.Clear();
                item.IsSelected = true;
            }
        }
    }

    private void HistoryList_MouseRightButtonUp(object sender, MouseButtonEventArgs e) =>
        e.Handled = false;

    private void HistoryContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItems();
        var hasSelection = selected.Count > 0;
        var single = selected.Count == 1;
        var allPinned = hasSelection && selected.All(i => i.IsPinned);
        var anyUnpinned = hasSelection && selected.Any(i => !i.IsPinned);

        CtxPaste.IsEnabled = hasSelection;
        CtxCopyAgain.IsEnabled = hasSelection;
        CtxPluck.IsEnabled = hasSelection;
        CtxDelete.IsEnabled = hasSelection;
        CtxPin.IsEnabled = hasSelection;
        CtxPin.Header = allPinned ? "Unpin" : "Pin";
        if (!anyUnpinned && allPinned)
            CtxPin.Header = "Unpin";
    }

    private void CtxPaste_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems())
            PluckAppHost.Instance.PasteItem(vm.Model);
    }

    private void CtxPin_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedItems();
        if (selected.Count == 0)
            return;

        var pin = selected.Any(i => !i.IsPinned);
        foreach (var vm in selected)
        {
            _repository.SetPinned(vm.Id, pin);
            PluckAppHost.Instance.NotifyHistoryPinChanged(vm.Id, pin);
        }

        RefreshHistory();
    }

    private void CtxCopyAgain_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems())
            PluckAppHost.Instance.CopyItemToClipboard(vm.Model);
    }

    private void CtxPluck_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems())
            PluckAppHost.Instance.ShowBubbleForItem(vm.Model);
    }

    private void CtxDelete_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems().ToList())
        {
            _repository.Delete(vm.Id);
            PluckAppHost.Instance.RemoveBubbleForItem(vm.Id);
        }

        RefreshHistory();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => ToggleSettingsPanel();

    private void ToggleSettingsPanel(bool forceOpen = false)
    {
        EnsureSettingsDialog();

        if (!forceOpen && _settingsDialog!.IsVisible)
        {
            _settingsDialog.Hide();
            return;
        }

        _settingsDialog!.LoadSettings(_settings);
        _settingsDialog.PositionBesideMain(this);
        _settingsDialog.Show();
        _settingsDialog.Activate();
    }

    private void EnsureSettingsDialog()
    {
        if (_settingsDialog is not null)
            return;

        _settingsDialog = new SettingsDialog(_settingsStore, _settings, OnSettingsSaved)
        {
            PairedMain = this,
            Owner = this
        };
        PluckWindowGuard.Instance.Register(_settingsDialog);
    }

    private void OnSettingsSaved(PluckSettings settings)
    {
        _settings = settings;
        PluckAppHost.Instance.UpdateSettings(settings);
        ApplyHistoryFilters();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideMainAndSettings();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    private void HistoryList_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CtxPaste_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Delete:
                CtxDelete_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_syncingMove || _settingsDialog is null || !_settingsDialog.IsVisible)
            return;

        _settingsDialog.SyncFromMainPosition(Left, Top, ActualWidth, ActualHeight);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        HideMainAndSettings();
    }

    public new void Hide()
    {
        HideMainAndSettings();
    }

    private void HideMainAndSettings()
    {
        _settingsDialog?.Hide();
        base.Hide();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private sealed record HistoryFilterOption<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }
}
