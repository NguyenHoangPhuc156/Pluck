using Pluck.Data.Models;

namespace Pluck.UI.Models;

/// <summary>
/// UI state for a single floating clipboard bubble, including layout and user customization.
/// </summary>
public sealed class BubbleModel
{
    /// <summary>
    /// Gets the unique identifier for this bubble instance.
    /// </summary>
    public Guid BubbleId { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the underlying clipboard item displayed by the bubble.
    /// </summary>
    public ClipboardItem Item { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether the bubble is pinned and exempt from auto-dismiss and trim rules.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets or sets whether the bubble is currently shown in a collapsed stack representation.
    /// </summary>
    public bool IsStackCollapsed { get; set; }

    /// <summary>
    /// Gets or sets the bubble's screen X position in device-independent pixels when user-positioned.
    /// </summary>
    public double ScreenLeft { get; set; }

    /// <summary>
    /// Gets or sets the bubble's screen Y position in device-independent pixels when user-positioned.
    /// </summary>
    public double ScreenTop { get; set; }

    /// <summary>
    /// Gets or sets whether the user has manually repositioned the bubble away from the stack.
    /// </summary>
    public bool HasUserPosition { get; set; }

    /// <summary>
    /// Gets or sets a custom canvas X offset reserved for future layout use.
    /// </summary>
    public double CustomX { get; set; }

    /// <summary>
    /// Gets or sets a custom canvas Y offset reserved for future layout use.
    /// </summary>
    public double CustomY { get; set; }

    /// <summary>
    /// Gets or sets the user-resized bubble width in device-independent pixels.
    /// </summary>
    public double CustomWidth { get; set; } = 220;

    /// <summary>
    /// Gets or sets the user-resized bubble height in device-independent pixels; zero means auto height.
    /// </summary>
    public double CustomHeight { get; set; }

    /// <summary>
    /// Gets or sets the bubble's assigned canvas Y coordinate during automatic stack layout.
    /// </summary>
    public double LayoutY { get; set; }

    /// <summary>
    /// Gets or sets when the bubble was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
