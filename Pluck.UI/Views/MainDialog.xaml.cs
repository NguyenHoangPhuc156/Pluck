using System.Windows;
using System.Windows.Input;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.ViewModels;

namespace Pluck.UI.Views;

public partial class MainDialog : Window
{
    private readonly ClipboardRepository _repository;
    private readonly SettingsStore _settingsStore;
    private PluckSettings _settings;

    public MainDialog(ClipboardRepository repository, SettingsStore settingsStore, PluckSettings settings)
    {
        InitializeComponent();
        _repository = repository;
        _settingsStore = settingsStore;
        _settings = settings;

        Icon = Helpers.IconHelper.LoadAppIconImage();
        BindSettingsToUi();
        RefreshHistory();
    }

    public void ShowSettingsTab()
    {
        MainTabs.SelectedItem = SettingsTab;
        Show();
        Activate();
    }

    public void RefreshHistory()
    {
        var items = _repository.GetRecent(200)
            .Select(i => new HistoryItemViewModel(i))
            .ToList();
        HistoryList.ItemsSource = items;
    }

    private void BindSettingsToUi()
    {
        OpacitySlider.Value = _settings.OpacityPercent;
        OpacityLabel.Text = $"{_settings.OpacityPercent}%";
        OpacitySlider.ValueChanged += (_, _) => OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";

        MaxBubblesBox.Text = _settings.MaxBubbles.ToString();
        DisplayDurationCheck.IsChecked = _settings.DisplayDurationEnabled;
        DisplayDurationBox.Text = _settings.DisplayDurationSeconds.ToString();
        FloatingAnimationCheck.IsChecked = _settings.FloatingAnimationEnabled;
        ContentDisplayCombo.ItemsSource = Enum.GetValues<BubbleContentDisplayMode>();
        ContentDisplayCombo.SelectedItem = _settings.ContentDisplay;
        ShowSourceIconCheck.IsChecked = _settings.ShowSourceAppIcon;
        ShowSourceNameCheck.IsChecked = _settings.ShowSourceAppName;
        ShowTimestampCheck.IsChecked = _settings.ShowCopyTimestamp;
        PopEffectCheck.IsChecked = _settings.PopEffectOnPaste;
        HotkeyBox.Text = _settings.GlobalHotkey;
        LaunchAtStartupCheck.IsChecked = _settings.LaunchAtStartup;
        HistoryLimitBox.Text = _settings.HistoryLimit.ToString();
        ClearOnExitCheck.IsChecked = _settings.ClearHistoryOnExit;
    }

    private PluckSettings ReadSettingsFromUi()
    {
        return new PluckSettings
        {
            OpacityPercent = (int)OpacitySlider.Value,
            MaxBubbles = Clamp(ParseInt(MaxBubblesBox.Text, _settings.MaxBubbles), 10, 50),
            DisplayDurationEnabled = DisplayDurationCheck.IsChecked == true,
            DisplayDurationSeconds = Clamp(ParseInt(DisplayDurationBox.Text, 10), 1, 60),
            FloatingAnimationEnabled = FloatingAnimationCheck.IsChecked == true,
            ContentDisplay = (BubbleContentDisplayMode)(ContentDisplayCombo.SelectedItem ?? BubbleContentDisplayMode.BestContent),
            ShowSourceAppIcon = ShowSourceIconCheck.IsChecked == true,
            ShowSourceAppName = ShowSourceNameCheck.IsChecked == true,
            ShowCopyTimestamp = ShowTimestampCheck.IsChecked == true,
            PopEffectOnPaste = PopEffectCheck.IsChecked == true,
            GlobalHotkey = HotkeyBox.Text.Trim(),
            LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true,
            HistoryLimit = Clamp(ParseInt(HistoryLimitBox.Text, _settings.HistoryLimit), 10, 1000),
            ClearHistoryOnExit = ClearOnExitCheck.IsChecked == true
        };
    }

    private HistoryItemViewModel? Selected => HistoryList.SelectedItem as HistoryItemViewModel;

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected?.Model;
        if (item is null) return;
        PluckAppHost.Instance.PasteItem(item);
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm is null) return;
        var pinned = !vm.IsPinned;
        _repository.SetPinned(vm.Id, pinned);
        PluckAppHost.Instance.NotifyHistoryPinChanged(vm.Id, pinned);
        RefreshHistory();
    }

    private void CopyAgain_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected?.Model;
        if (item is null) return;
        PluckAppHost.Instance.CopyItemToClipboard(item);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm is null) return;
        _repository.Delete(vm.Id);
        PluckAppHost.Instance.RemoveBubbleForItem(vm.Id);
        RefreshHistory();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings = ReadSettingsFromUi();
        _settingsStore.Save(_settings);
        PluckAppHost.Instance.UpdateSettings(_settings);
        System.Windows.MessageBox.Show(this, "Settings saved.", "Pluck", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void HistoryList_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Paste_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Delete:
                Delete_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, out var v) ? v : fallback;

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}
