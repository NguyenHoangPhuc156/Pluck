namespace Pluck.Data.Models;

/// <summary>
/// Represents a single clipboard history entry with payload data and source metadata.
/// </summary>
public sealed class ClipboardItem
{
    /// <summary>
    /// Gets or sets the unique database identifier for this entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the kind of clipboard data stored in this entry.
    /// </summary>
    public ClipboardItemType Type { get; set; }

    /// <summary>
    /// Gets or sets a short human-readable summary shown in lists and bubbles.
    /// </summary>
    public string Preview { get; set; } = "";

    /// <summary>
    /// Gets or sets the full text payload when <see cref="Type"/> is <see cref="ClipboardItemType.Text"/>.
    /// </summary>
    public string? TextContent { get; set; }

    /// <summary>
    /// Gets or sets a PNG-encoded thumbnail when <see cref="Type"/> is <see cref="ClipboardItemType.Image"/>.
    /// </summary>
    public byte[]? ImageThumbnailPng { get; set; }

    /// <summary>
    /// Gets or sets the full-resolution PNG image data when <see cref="Type"/> is <see cref="ClipboardItemType.Image"/>.
    /// </summary>
    public byte[]? ImageFullPng { get; set; }

    /// <summary>
    /// Gets or sets a JSON-encoded list of file paths when <see cref="Type"/> is <see cref="ClipboardItemType.Files"/>.
    /// </summary>
    public string? FilePathsJson { get; set; }

    /// <summary>
    /// Gets or sets the display name of the application that owned the source window at copy time.
    /// </summary>
    public string SourceAppName { get; set; } = "";

    /// <summary>
    /// Gets or sets a PNG-encoded icon for the source application.
    /// </summary>
    public byte[]? SourceAppIconPng { get; set; }

    /// <summary>
    /// Gets or sets the native handle of the source window at copy time.
    /// </summary>
    public IntPtr SourceWindowHandle { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the item was copied to the clipboard.
    /// </summary>
    public DateTimeOffset CopiedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this entry is pinned and exempt from automatic history trimming.
    /// </summary>
    public bool IsPinned { get; set; }
}
