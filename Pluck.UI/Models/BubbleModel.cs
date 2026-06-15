using Pluck.Data.Models;

namespace Pluck.UI.Models;

public sealed class BubbleModel
{
    public Guid BubbleId { get; } = Guid.NewGuid();
    public ClipboardItem Item { get; set; } = null!;
    public bool IsPinned { get; set; }
    public bool IsStackCollapsed { get; set; }
    /// <summary>User moved bubble with right-drag (screen DIP, top-left).</summary>
    public double ScreenLeft { get; set; }
    public double ScreenTop { get; set; }
    public bool HasUserPosition { get; set; }
    public double CustomX { get; set; }
    public double CustomY { get; set; }
    public double CustomWidth { get; set; } = 220;
    public double CustomHeight { get; set; }
    public double LayoutY { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
