using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.Helpers;

namespace Pluck.UI.Views;

/// <summary>
/// Settings panel window for configuring Pluck behavior, appearance, and mouse bindings.
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsStore _settingsStore;
    private PluckSettings _settings;
    private readonly Action<PluckSettings> _onSaved;
    private bool _syncingMove;

    /// <summary>
    /// Initializes the settings dialog and binds the current settings to the UI.
    /// </summary>
    /// <param name="settingsStore">Persistent settings store used when saving.</param>
    /// <param name="settings">Initial settings snapshot.</param>
    /// <param name="onSaved">Callback invoked with saved settings after the user confirms.</param>
    public SettingsDialog(SettingsStore settingsStore, PluckSettings settings, Action<PluckSettings> onSaved)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _settings = settings;
        _onSaved = onSaved;

        WindowChromeHelper.HideFromAltTab(this);
        BindSettingsToUi();
    }

    /// <summary>
    /// Gets or sets the main dialog paired with this settings window for synchronized movement.
    /// </summary>
    public MainDialog? PairedMain { get; set; }

    /// <summary>
    /// Reloads the in-memory settings snapshot and refreshes all UI controls.
    /// </summary>
    /// <param name="settings">Settings to display.</param>
    public void LoadSettings(PluckSettings settings)
    {
        _settings = settings;
        BindSettingsToUi();
    }

    /// <summary>
    /// Positions the settings panel immediately to the right of the main dialog.
    /// </summary>
    /// <param name="main">Main dialog window used as the positioning anchor.</param>
    public void PositionBesideMain(Window main)
    {
        Left = main.Left + main.ActualWidth;
        Top = main.Top;
        Height = main.ActualHeight;
    }

    /// <summary>
    /// Copies current settings values into all UI controls and rebinds change handlers.
    /// </summary>
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

    /// <summary>
    /// Updates the opacity label when the slider value changes.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">New slider value.</param>
    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";

    /// <summary>
    /// Enables or disables the stack-collapse threshold field based on the checkbox state.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void StackCollapseCheck_Changed(object sender, RoutedEventArgs e) =>
        StackCollapseThresholdBox.IsEnabled = StackCollapseCheck.IsChecked == true;

    /// <summary>
    /// Populates mouse click and drag combo boxes with available action options.
    /// </summary>
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

    /// <summary>
    /// Sets combo box and modifier checkbox values from a mouse binding model.
    /// </summary>
    /// <param name="binding">Mouse binding to display.</param>
    /// <param name="clickCombo">Combo box for click actions.</param>
    /// <param name="dragCombo">Combo box for drag actions.</param>
    /// <param name="ctrl">Control modifier checkbox.</param>
    /// <param name="shift">Shift modifier checkbox.</param>
    /// <param name="alt">Alt modifier checkbox.</param>
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

    /// <summary>
    /// Builds a <see cref="PluckSettings"/> instance from the current UI control values.
    /// </summary>
    /// <returns>Settings read from the dialog controls.</returns>
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

    /// <summary>
    /// Reads a single mouse binding from UI controls, falling back when combo values are unset.
    /// </summary>
    /// <param name="clickCombo">Combo box for click actions.</param>
    /// <param name="dragCombo">Combo box for drag actions.</param>
    /// <param name="ctrl">Control modifier checkbox.</param>
    /// <param name="shift">Shift modifier checkbox.</param>
    /// <param name="alt">Alt modifier checkbox.</param>
    /// <param name="fallback">Binding values used when a combo selection is missing.</param>
    /// <returns>The mouse binding represented by the controls.</returns>
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

    /// <summary>
    /// Persists settings from the UI and notifies the host via the save callback.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Routed event data.</param>
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _settings = ReadSettingsFromUi();
        _settingsStore.Save(_settings);
        _onSaved(_settings);
        var owner = PairedMain as Window ?? this;
        System.Windows.MessageBox.Show(owner, "Settings saved.", "Pluck", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Enables dragging the settings window by its custom title bar.
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
    /// Keeps the paired main dialog aligned when this settings window moves.
    /// </summary>
    /// <param name="e">Location change event data.</param>
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_syncingMove || PairedMain is null || !IsVisible)
            return;

        PairedMain.SyncFromSettingsPosition(Left, Top, ActualWidth, ActualHeight);
    }

    /// <summary>
    /// Moves this settings window to stay docked to the right of the main dialog.
    /// </summary>
    /// <param name="mainLeft">Main dialog left edge in DIP.</param>
    /// <param name="mainTop">Main dialog top edge in DIP.</param>
    /// <param name="mainWidth">Main dialog width in DIP.</param>
    /// <param name="mainHeight">Main dialog height in DIP.</param>
    internal void SyncFromMainPosition(double mainLeft, double mainTop, double mainWidth, double mainHeight)
    {
        _syncingMove = true;
        Left = mainLeft + mainWidth;
        Top = mainTop;
        Height = mainHeight;
        _syncingMove = false;
    }

    /// <summary>
    /// Hides the settings dialog instead of closing it so it can be shown again quickly.
    /// </summary>
    /// <param name="e">Cancel event arguments set to prevent actual window closure.</param>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    /// <summary>
    /// Parses an integer from text, returning a fallback when parsing fails.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="fallback">Value returned when parsing fails.</param>
    /// <returns>The parsed integer or the fallback value.</returns>
    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, out var v) ? v : fallback;

    /// <summary>
    /// Clamps an integer to an inclusive minimum and maximum.
    /// </summary>
    /// <param name="value">Value to clamp.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <returns>The clamped value.</returns>
    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);

    /// <summary>
    /// Label/value pair used to populate mouse action combo boxes.
    /// </summary>
    /// <typeparam name="T">Underlying action enum type.</typeparam>
    private sealed record MouseActionOption<T>(string Label, T Value);
}
