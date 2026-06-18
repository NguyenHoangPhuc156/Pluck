namespace Pluck.Data.Models;

/// <summary>
/// User-configurable application settings for bubble appearance, behavior, and history.
/// </summary>
public sealed class PluckSettings
{
    // Bubble settings

    /// <summary>
    /// Gets or sets the bubble opacity as a percentage (0–100).
    /// </summary>
    public int OpacityPercent { get; set; } = 70;

    /// <summary>
    /// Gets or sets the maximum number of bubbles displayed at once.
    /// </summary>
    public int MaxBubbles { get; set; } = 20;

    /// <summary>
    /// Gets or sets whether bubbles automatically hide after a configured duration.
    /// </summary>
    public bool DisplayDurationEnabled { get; set; }

    /// <summary>
    /// Gets or sets how long bubbles remain visible when <see cref="DisplayDurationEnabled"/> is true, in seconds.
    /// </summary>
    public int DisplayDurationSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether bubbles use a subtle floating animation.
    /// </summary>
    public bool FloatingAnimationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether overlapping bubbles collapse into a stack.
    /// </summary>
    public bool StackCollapseEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the bubble count above which stack collapse is applied.
    /// </summary>
    public int StackCollapseThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets how clipboard content is rendered inside bubbles.
    /// </summary>
    public BubbleContentDisplayMode ContentDisplay { get; set; } = BubbleContentDisplayMode.BestContent;

    /// <summary>
    /// Gets or sets whether the source application's icon is shown on each bubble.
    /// </summary>
    public bool ShowSourceAppIcon { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the source application's name is shown on each bubble.
    /// </summary>
    public bool ShowSourceAppName { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the copy timestamp is shown on each bubble.
    /// </summary>
    public bool ShowCopyTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether a pop animation plays when an item is pasted from a bubble.
    /// </summary>
    public bool PopEffectOnPaste { get; set; } = true;

    // Mouse bindings (must include a mouse button)

    /// <summary>
    /// Gets or sets the mouse binding for the left button.
    /// </summary>
    public BubbleMouseBinding MouseLeft { get; set; } = BubbleMouseBinding.DefaultLeft();

    /// <summary>
    /// Gets or sets the mouse binding for the right button.
    /// </summary>
    public BubbleMouseBinding MouseRight { get; set; } = BubbleMouseBinding.DefaultRight();

    /// <summary>
    /// Gets or sets the mouse binding for the middle button.
    /// </summary>
    public BubbleMouseBinding MouseMiddle { get; set; } = BubbleMouseBinding.DefaultMiddle();

    // General settings

    /// <summary>
    /// Gets or sets the global hotkey chord that opens the Pluck UI (for example, <c>Ctrl+Shift+V</c>).
    /// </summary>
    public string GlobalHotkey { get; set; } = "Ctrl+Shift+V";

    /// <summary>
    /// Gets or sets whether Pluck starts automatically when the user signs in.
    /// </summary>
    public bool LaunchAtStartup { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of unpinned items retained in clipboard history.
    /// </summary>
    public int HistoryLimit { get; set; } = 200;

    /// <summary>
    /// Gets or sets whether clipboard history is cleared when the application exits.
    /// </summary>
    public bool ClearHistoryOnExit { get; set; }
}

/// <summary>
/// Controls how clipboard payload content is displayed on bubbles.
/// </summary>
public enum BubbleContentDisplayMode
{
    /// <summary>Shows the best available preview for each item type.</summary>
    BestContent,

    /// <summary>Shows content only for recognized item types; unknown types show a generic preview.</summary>
    KnownContentOnly,

    /// <summary>Does not display payload content on bubbles.</summary>
    Disabled
}
