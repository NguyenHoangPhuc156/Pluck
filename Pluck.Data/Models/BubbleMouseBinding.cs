namespace Pluck.Data.Models;

public enum BubbleClickAction
{
    None,
    Paste,
    Delete,
    ContextMenu
}

public enum BubbleDragAction
{
    None,
    PasteDrag,
    MoveDrag
}

public sealed class BubbleMouseBinding
{
    public BubbleClickAction ClickAction { get; set; }
    public BubbleDragAction DragAction { get; set; }
    public bool RequireCtrl { get; set; }
    public bool RequireShift { get; set; }
    public bool RequireAlt { get; set; }

    public static BubbleMouseBinding DefaultLeft() => new()
    {
        ClickAction = BubbleClickAction.Paste,
        DragAction = BubbleDragAction.PasteDrag
    };

    public static BubbleMouseBinding DefaultRight() => new()
    {
        ClickAction = BubbleClickAction.ContextMenu,
        DragAction = BubbleDragAction.MoveDrag
    };

    public static BubbleMouseBinding DefaultMiddle() => new()
    {
        ClickAction = BubbleClickAction.Delete,
        DragAction = BubbleDragAction.None
    };
}
