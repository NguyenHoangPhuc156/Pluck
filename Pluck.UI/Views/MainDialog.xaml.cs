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

/// <summary>
/// Main history browser window with search, filters, context actions, and paired settings panel.
/// </summary>
public partial class MainDialog : Window
{
    private readonly ClipboardRepository _repository;
    private readonly SettingsStore _settingsStore;
    private PluckSettings _settings;
    private readonly DispatcherTimer _searchDebounceTimer;
    private bool _historyFiltersInitialized;
    private SettingsDialog? _settingsDialog;
    private bool _syncingMove;

    /// <summary>
    /// Initializes the main dialog, history filters, and debounced search behavior.
    /// </summary>
    /// <param name="repository">Clipboard repository backing the history list.</param>
    /// <param name="settingsStore">Persistent settings store for the settings panel.</param>
    /// <param name="settings">Initial application settings.</param>
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

    /// <summary>
    /// Shows the main dialog and opens the settings panel beside it.
    /// </summary>
    public void ShowSettingsPanel()
    {
        Show();
        Activate();
        ToggleSettingsPanel(forceOpen: true);
    }

    /// <summary>
    /// Refreshes source-app filter options and reapplies the current history query.
    /// </summary>
    public void RefreshHistory()
    {
        RefreshAppFilterOptions();
        ApplyHistoryFilters();
    }

    /// <summary>
    /// Updates the cached settings snapshot and reloads the settings dialog when present.
    /// </summary>
    /// <param name="settings">Updated application settings.</param>
    public void ApplySettings(PluckSettings settings)
    {
        _settings = settings;
        _settingsDialog?.LoadSettings(settings);
    }

    /// <summary>
    /// Moves the main dialog to remain docked to the left of the settings panel.
    /// </summary>
    /// <param name="settingsLeft">Settings panel left edge in DIP.</param>
    /// <param name="settingsTop">Settings panel top edge in DIP.</param>
    /// <param name="settingsWidth">Settings panel width in DIP.</param>
    /// <param name="settingsHeight">Settings panel height in DIP.</param>
    internal void SyncFromSettingsPosition(double settingsLeft, double settingsTop, double settingsWidth, double settingsHeight)
    {
        _syncingMove = true;
        Left = settingsLeft - ActualWidth;
        Top = settingsTop;
        Height = settingsHeight;
        _syncingMove = false;
    }

    /// <summary>
    /// Populates type and time filter combo boxes and initializes the app filter list.
    /// </summary>
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

    /// <summary>
    /// Rebuilds the source-application filter list while preserving the current selection when possible.
    /// </summary>
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

    /// <summary>
    /// Queries the repository with current filter controls and binds results to the history list.
    /// </summary>
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

    /// <summary>
    /// Responds to filter control changes, debouncing text search input.
    /// </summary>
    /// <param name="sender">Filter control that raised the change.</param>
    /// <param name="e">Routed event data.</param>
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

    /// <summary>
    /// Clears all history filter controls and refreshes the list.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void ClearHistoryFilters_Click(object sender, RoutedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        HistorySearchBox.Text = "";
        HistoryAppFilter.SelectedIndex = 0;
        HistoryTypeFilter.SelectedIndex = 0;
        HistoryTimeFilter.SelectedIndex = 0;
        ApplyHistoryFilters();
    }

    /// <summary>
    /// Returns the currently selected history items from the list view.
    /// </summary>
    /// <returns>Read-only list of selected view models.</returns>
    private IReadOnlyList<HistoryItemViewModel> GetSelectedItems() =>
        HistoryList.SelectedItems.Cast<HistoryItemViewModel>().ToList();

    /// <summary>
    /// Selects a list item under the cursor on right-click when it is not already selected.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
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

    /// <summary>
    /// Allows the default context menu to open after right-button-up handling.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
    private void HistoryList_MouseRightButtonUp(object sender, MouseButtonEventArgs e) =>
        e.Handled = false;

    /// <summary>
    /// Updates context menu item labels and enabled state based on the current selection.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
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

    /// <summary>
    /// Pastes all selected history items at the current cursor location.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void CtxPaste_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems())
            PluckAppHost.Instance.PasteItem(vm.Model);
    }

    /// <summary>
    /// Toggles pin state for all selected history items and refreshes the list.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
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

    /// <summary>
    /// Copies all selected history items back to the system clipboard.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void CtxCopyAgain_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems())
            PluckAppHost.Instance.CopyItemToClipboard(vm.Model);
    }

    /// <summary>
    /// Shows a floating bubble for each selected history item.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void CtxPluck_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems())
            PluckAppHost.Instance.ShowBubbleForItem(vm.Model);
    }

    /// <summary>
    /// Deletes all selected history items and removes associated bubbles.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void CtxDelete_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in GetSelectedItems().ToList())
        {
            _repository.Delete(vm.Id);
            PluckAppHost.Instance.RemoveBubbleForItem(vm.Id);
        }

        RefreshHistory();
    }

    /// <summary>
    /// Toggles visibility of the paired settings panel.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void Settings_Click(object sender, RoutedEventArgs e) => ToggleSettingsPanel();

    /// <summary>
    /// Shows or hides the settings dialog beside the main window.
    /// </summary>
    /// <param name="forceOpen">When true, always opens the settings panel even if it is already visible.</param>
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

    /// <summary>
    /// Lazily creates and registers the paired settings dialog.
    /// </summary>
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

    /// <summary>
    /// Applies saved settings through the application host and refreshes the history query.
    /// </summary>
    /// <param name="settings">Settings saved from the settings dialog.</param>
    private void OnSettingsSaved(PluckSettings settings)
    {
        _settings = settings;
        PluckAppHost.Instance.UpdateSettings(settings);
        ApplyHistoryFilters();
    }

    /// <summary>
    /// Hides the main dialog and paired settings panel.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void Close_Click(object sender, RoutedEventArgs e) => HideMainAndSettings();

    /// <summary>
    /// Enables dragging the main window by its custom title bar.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    /// <summary>
    /// Handles Enter to paste and Delete to remove selected history items.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Key event data.</param>
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

    /// <summary>
    /// Keeps the settings panel aligned when the main dialog moves.
    /// </summary>
    /// <param name="e">Location change event data.</param>
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_syncingMove || _settingsDialog is null || !_settingsDialog.IsVisible)
            return;

        _settingsDialog.SyncFromMainPosition(Left, Top, ActualWidth, ActualHeight);
    }

    /// <summary>
    /// Hides the main and settings windows instead of closing them.
    /// </summary>
    /// <param name="e">Cancel event arguments set to prevent actual window closure.</param>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        HideMainAndSettings();
    }

    /// <summary>
    /// Hides the main dialog and any visible settings panel.
    /// </summary>
    public new void Hide()
    {
        HideMainAndSettings();
    }

    /// <summary>
    /// Hides the settings dialog first, then hides the main window.
    /// </summary>
    private void HideMainAndSettings()
    {
        _settingsDialog?.Hide();
        base.Hide();
    }

    /// <summary>
    /// Walks the visual tree upward to find an ancestor of the specified type.
    /// </summary>
    /// <typeparam name="T">Ancestor type to locate.</typeparam>
    /// <param name="current">Starting visual node.</param>
    /// <returns>The matching ancestor, or <see langword="null"/> when not found.</returns>
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

    /// <summary>
    /// Local filter option type used only within the main dialog filter combo boxes.
    /// </summary>
    /// <typeparam name="T">Underlying filter value type.</typeparam>
    /// <param name="Label">Display text shown in the combo box.</param>
    /// <param name="Value">Filter value applied when selected.</param>
    private sealed record HistoryFilterOption<T>(string Label, T Value)
    {
        /// <summary>
        /// Returns the display label for combo box rendering.
        /// </summary>
        /// <returns>The option label.</returns>
        public override string ToString() => Label;
    }
}
