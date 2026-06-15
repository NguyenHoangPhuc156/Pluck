namespace Pluck.Data.Models;

public sealed class PluckSettings
{
    // Bubble settings
    public int OpacityPercent { get; set; } = 70;
    public int MaxBubbles { get; set; } = 20;
    public bool DisplayDurationEnabled { get; set; }
    public int DisplayDurationSeconds { get; set; } = 10;
    public bool FloatingAnimationEnabled { get; set; } = true;
    public bool StackCollapseEnabled { get; set; } = true;
    public int StackCollapseThreshold { get; set; } = 5;
    public BubbleContentDisplayMode ContentDisplay { get; set; } = BubbleContentDisplayMode.BestContent;
    public bool ShowSourceAppIcon { get; set; } = true;
    public bool ShowSourceAppName { get; set; } = true;
    public bool ShowCopyTimestamp { get; set; }
    public bool PopEffectOnPaste { get; set; } = true;

    // Mouse bindings (must include a mouse button)
    public BubbleMouseBinding MouseLeft { get; set; } = BubbleMouseBinding.DefaultLeft();
    public BubbleMouseBinding MouseRight { get; set; } = BubbleMouseBinding.DefaultRight();
    public BubbleMouseBinding MouseMiddle { get; set; } = BubbleMouseBinding.DefaultMiddle();

    // General settings
    public string GlobalHotkey { get; set; } = "Ctrl+Shift+V";
    public bool LaunchAtStartup { get; set; }
    public int HistoryLimit { get; set; } = 200;
    public bool ClearHistoryOnExit { get; set; }
}

public enum BubbleContentDisplayMode
{
    BestContent,
    KnownContentOnly,
    Disabled
}
