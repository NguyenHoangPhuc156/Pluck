namespace Pluck.Data.Models;

/// <summary>
/// Defines the action performed when a bubble is clicked.
/// </summary>
public enum BubbleClickAction
{
    /// <summary>No action is performed.</summary>
    None,

    /// <summary>Pastes the clipboard item into the target application.</summary>
    Paste,

    /// <summary>Removes the bubble and its associated history entry.</summary>
    Delete,

    /// <summary>Opens the bubble context menu.</summary>
    ContextMenu
}

/// <summary>
/// Defines the action performed when a bubble is dragged.
/// </summary>
public enum BubbleDragAction
{
    /// <summary>No drag action is performed.</summary>
    None,

    /// <summary>Initiates a paste operation via drag-and-drop.</summary>
    PasteDrag,

    /// <summary>Allows repositioning the bubble on screen.</summary>
    MoveDrag
}

/// <summary>
/// Maps a mouse button to click and drag behaviors, optionally gated by modifier keys.
/// </summary>
public sealed class BubbleMouseBinding
{
    /// <summary>
    /// Gets or sets the action performed on a single click.
    /// </summary>
    public BubbleClickAction ClickAction { get; set; }

    /// <summary>
    /// Gets or sets the action performed when the button is dragged.
    /// </summary>
    public BubbleDragAction DragAction { get; set; }

    /// <summary>
    /// Gets or sets whether the Control modifier must be held for the binding to apply.
    /// </summary>
    public bool RequireCtrl { get; set; }

    /// <summary>
    /// Gets or sets whether the Shift modifier must be held for the binding to apply.
    /// </summary>
    public bool RequireShift { get; set; }

    /// <summary>
    /// Gets or sets whether the Alt modifier must be held for the binding to apply.
    /// </summary>
    public bool RequireAlt { get; set; }

    /// <summary>
    /// Creates the default binding for the left mouse button (paste on click, paste on drag).
    /// </summary>
    /// <returns>A <see cref="BubbleMouseBinding"/> configured for left-button interaction.</returns>
    public static BubbleMouseBinding DefaultLeft() => new()
    {
        ClickAction = BubbleClickAction.Paste,
        DragAction = BubbleDragAction.PasteDrag
    };

    /// <summary>
    /// Creates the default binding for the right mouse button (context menu on click, move on drag).
    /// </summary>
    /// <returns>A <see cref="BubbleMouseBinding"/> configured for right-button interaction.</returns>
    public static BubbleMouseBinding DefaultRight() => new()
    {
        ClickAction = BubbleClickAction.ContextMenu,
        DragAction = BubbleDragAction.MoveDrag
    };

    /// <summary>
    /// Creates the default binding for the middle mouse button (delete on click, no drag action).
    /// </summary>
    /// <returns>A <see cref="BubbleMouseBinding"/> configured for middle-button interaction.</returns>
    public static BubbleMouseBinding DefaultMiddle() => new()
    {
        ClickAction = BubbleClickAction.Delete,
        DragAction = BubbleDragAction.None
    };
}
