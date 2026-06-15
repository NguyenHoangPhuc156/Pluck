namespace Pluck.Data.Models;

public sealed class ClipboardItem
{
    public long Id { get; set; }
    public ClipboardItemType Type { get; set; }
    public string Preview { get; set; } = "";
    public string? TextContent { get; set; }
    public byte[]? ImageThumbnailPng { get; set; }
    public byte[]? ImageFullPng { get; set; }
    public string? FilePathsJson { get; set; }
    public string SourceAppName { get; set; } = "";
    public byte[]? SourceAppIconPng { get; set; }
    public IntPtr SourceWindowHandle { get; set; }
    public DateTimeOffset CopiedAt { get; set; }
    public bool IsPinned { get; set; }
}
