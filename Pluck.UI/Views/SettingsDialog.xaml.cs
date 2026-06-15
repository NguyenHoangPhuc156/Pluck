using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.Helpers;

namespace Pluck.UI.Views;

public partial class SettingsDialog : Window
{
    private readonly SettingsStore _settingsStore;
    private PluckSettings _settings;
    private readonly Action<PluckSettings> _onSaved;
    private bool _syncingMove;

    public SettingsDialog(SettingsStore settingsStore, PluckSettings settings, Action<PluckSettings> onSaved)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _settings = settings;
        _onSaved = onSaved;

        WindowChromeHelper.HideFromAltTab(this);
        BindSettingsToUi();
    }

    public MainDialog? PairedMain { get; set; }

    public void LoadSettings(PluckSettings settings)
    {
        _settings = settings;
        BindSettingsToUi();
    }

    public void PositionBesideMain(Window main)
    {
        Left = main.Left + main.ActualWidth;
        Top = main.Top;
        Height = main.ActualHeight;
    }

    private void BindSettingsToUi()
    {
        OpacitySlider.Value = _settings.OpacityPercent;
        OpacityLabel.Text = $"{_settings.OpacityPercent}%";
        OpacitySlider.ValueChanged -= OpacitySlider_ValueChanged;
        OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;

        MaxBubblesBox.Text = _settings.MaxBubbles.ToString();
        DisplayDurationCheck.IsChecked = _settings.DisplayDurationEnabled;
        DisplayDurationBox.Text = _settings.DisplayDurationSeconds.ToString();
        FloatingAnimationCheck.IsChecked = _settings.FloatingAnimationEnabled;
        StackCollapseCheck.IsChecked = _settings.StackCollapseEnabled;
        StackCollapseThresholdBox.Text = _settings.StackCollapseThreshold.ToString();
        StackCollapseThresholdBox.IsEnabled = _settings.StackCollapseEnabled;
        StackCollapseCheck.Checked -= StackCollapseCheck_Changed;
        StackCollapseCheck.Unchecked -= StackCollapseCheck_Changed;
        StackCollapseCheck.Checked += StackCollapseCheck_Changed;
        StackCollapseCheck.Unchecked += StackCollapseCheck_Changed;
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

        InitializeMouseBindingCombos();
        BindMouseBindingToUi(_settings.MouseLeft, MouseLeftClickCombo, MouseLeftDragCombo, MouseLeftCtrlCheck, MouseLeftShiftCheck, MouseLeftAltCheck);
        BindMouseBindingToUi(_settings.MouseRight, MouseRightClickCombo, MouseRightDragCombo, MouseRightCtrlCheck, MouseRightShiftCheck, MouseRightAltCheck);
        BindMouseBindingToUi(_settings.MouseMiddle, MouseMiddleClickCombo, MouseMiddleDragCombo, MouseMiddleCtrlCheck, MouseMiddleShiftCheck, MouseMiddleAltCheck);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";

    private void StackCollapseCheck_Changed(object sender, RoutedEventArgs e) =>
        StackCollapseThresholdBox.IsEnabled = StackCollapseCheck.IsChecked == true;

    private void InitializeMouseBindingCombos()
    {
        var clickItems = new[]
        {
            new MouseActionOption<BubbleClickAction>("None", BubbleClickAction.None),
            new MouseActionOption<BubbleClickAction>("Click to paste", BubbleClickAction.Paste),
            new MouseActionOption<BubbleClickAction>("Click to delete", BubbleClickAction.Delete),
            new MouseActionOption<BubbleClickAction>("Context menu", BubbleClickAction.ContextMenu)
        };

        var dragItems = new[]
        {
            new MouseActionOption<BubbleDragAction>("None", BubbleDragAction.None),
            new MouseActionOption<BubbleDragAction>("Drag to paste", BubbleDragAction.PasteDrag),
            new MouseActionOption<BubbleDragAction>("Drag to move", BubbleDragAction.MoveDrag)
        };

        foreach (var combo in new[] { MouseLeftClickCombo, MouseRightClickCombo, MouseMiddleClickCombo })
        {
            combo.ItemsSource = clickItems;
            combo.DisplayMemberPath = nameof(MouseActionOption<BubbleClickAction>.Label);
            combo.SelectedValuePath = nameof(MouseActionOption<BubbleClickAction>.Value);
        }

        foreach (var combo in new[] { MouseLeftDragCombo, MouseRightDragCombo, MouseMiddleDragCombo })
        {
            combo.ItemsSource = dragItems;
            combo.DisplayMemberPath = nameof(MouseActionOption<BubbleDragAction>.Label);
            combo.SelectedValuePath = nameof(MouseActionOption<BubbleDragAction>.Value);
        }
    }

    private static void BindMouseBindingToUi(
        BubbleMouseBinding binding,
        System.Windows.Controls.ComboBox clickCombo,
        System.Windows.Controls.ComboBox dragCombo,
        System.Windows.Controls.CheckBox ctrl,
        System.Windows.Controls.CheckBox shift,
        System.Windows.Controls.CheckBox alt)
    {
        clickCombo.SelectedValue = binding.ClickAction;
        dragCombo.SelectedValue = binding.DragAction;
        ctrl.IsChecked = binding.RequireCtrl;
        shift.IsChecked = binding.RequireShift;
        alt.IsChecked = binding.RequireAlt;
    }

    private PluckSettings ReadSettingsFromUi() => new()
    {
        OpacityPercent = (int)OpacitySlider.Value,
        MaxBubbles = Clamp(ParseInt(MaxBubblesBox.Text, _settings.MaxBubbles), 10, 50),
        DisplayDurationEnabled = DisplayDurationCheck.IsChecked == true,
        DisplayDurationSeconds = Clamp(ParseInt(DisplayDurationBox.Text, 10), 1, 60),
        FloatingAnimationEnabled = FloatingAnimationCheck.IsChecked == true,
        StackCollapseEnabled = StackCollapseCheck.IsChecked == true,
        StackCollapseThreshold = Clamp(ParseInt(StackCollapseThresholdBox.Text, _settings.StackCollapseThreshold), 2, 50),
        ContentDisplay = (BubbleContentDisplayMode)(ContentDisplayCombo.SelectedItem ?? BubbleContentDisplayMode.BestContent),
        ShowSourceAppIcon = ShowSourceIconCheck.IsChecked == true,
        ShowSourceAppName = ShowSourceNameCheck.IsChecked == true,
        ShowCopyTimestamp = ShowTimestampCheck.IsChecked == true,
        PopEffectOnPaste = PopEffectCheck.IsChecked == true,
        MouseLeft = ReadMouseBinding(MouseLeftClickCombo, MouseLeftDragCombo, MouseLeftCtrlCheck, MouseLeftShiftCheck, MouseLeftAltCheck, _settings.MouseLeft),
        MouseRight = ReadMouseBinding(MouseRightClickCombo, MouseRightDragCombo, MouseRightCtrlCheck, MouseRightShiftCheck, MouseRightAltCheck, _settings.MouseRight),
        MouseMiddle = ReadMouseBinding(MouseMiddleClickCombo, MouseMiddleDragCombo, MouseMiddleCtrlCheck, MouseMiddleShiftCheck, MouseMiddleAltCheck, _settings.MouseMiddle),
        GlobalHotkey = HotkeyBox.Text.Trim(),
        LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true,
        HistoryLimit = Clamp(ParseInt(HistoryLimitBox.Text, _settings.HistoryLimit), 10, 1000),
        ClearHistoryOnExit = ClearOnExitCheck.IsChecked == true
    };

    private static BubbleMouseBinding ReadMouseBinding(
        System.Windows.Controls.ComboBox clickCombo, System.Windows.Controls.ComboBox dragCombo, System.Windows.Controls.CheckBox ctrl, System.Windows.Controls.CheckBox shift, System.Windows.Controls.CheckBox alt, BubbleMouseBinding fallback) =>
        new()
        {
            ClickAction = (clickCombo.SelectedValue as BubbleClickAction?) ?? fallback.ClickAction,
            DragAction = (dragCombo.SelectedValue as BubbleDragAction?) ?? fallback.DragAction,
            RequireCtrl = ctrl.IsChecked == true,
            RequireShift = shift.IsChecked == true,
            RequireAlt = alt.IsChecked == true
        };

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings = ReadSettingsFromUi();
        _settingsStore.Save(_settings);
        _onSaved(_settings);
        var owner = PairedMain as Window ?? this;
        System.Windows.MessageBox.Show(owner, "Settings saved.", "Pluck", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_syncingMove || PairedMain is null || !IsVisible)
            return;

        PairedMain.SyncFromSettingsPosition(Left, Top, ActualWidth, ActualHeight);
    }

    internal void SyncFromMainPosition(double mainLeft, double mainTop, double mainWidth, double mainHeight)
    {
        _syncingMove = true;
        Left = mainLeft + mainWidth;
        Top = mainTop;
        Height = mainHeight;
        _syncingMove = false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, out var v) ? v : fallback;

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);

    private sealed record MouseActionOption<T>(string Label, T Value);
}
