using Pluck.Data.Models;

namespace Pluck.UI.Models;

public sealed class BubbleModel
{
    public Guid BubbleId { get; } = Guid.NewGuid();
    public ClipboardItem Item { get; set; } = null!;
    public bool IsPinned { get; set; }
    public bool IsStackCollapsed { get; set; }
    /// <summary>User moved bubble with Ctrl+drag.</summary>
    public bool HasUserPosition { get; set; }
    public double CustomX { get; set; }
    public double CustomY { get; set; }
    public double LayoutY { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
