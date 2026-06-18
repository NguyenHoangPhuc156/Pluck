using Pluck.Data.Models;

namespace Pluck.UI.ViewModels;

/// <summary>
/// Read-only presentation model for a clipboard history list row.
/// </summary>
public sealed class HistoryItemViewModel
{
    /// <summary>
    /// Initializes a view model from a persisted clipboard item.
    /// </summary>
    /// <param name="item">The clipboard item to present.</param>
    public HistoryItemViewModel(ClipboardItem item)
    {
        Id = item.Id;
        TypeLabel = item.Type switch
        {
            ClipboardItemType.Text => "Text",
            ClipboardItemType.Image => "Image",
            ClipboardItemType.Files => "Files",
            ClipboardItemType.Unknown => "Other",
            _ => "Other"
        };
        Preview = item.Preview;
        SourceAppName = item.SourceAppName;
        TimeLabel = FormatTime(item.CopiedAt);
        IsPinned = item.IsPinned;
        Model = item;
    }

    /// <summary>
    /// Gets the database identifier of the history item.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Gets the human-readable clipboard content type label.
    /// </summary>
    public string TypeLabel { get; }

    /// <summary>
    /// Gets the preview text shown in the history list.
    /// </summary>
    public string Preview { get; }

    /// <summary>
    /// Gets the name of the application that originated the copy.
    /// </summary>
    public string SourceAppName { get; }

    /// <summary>
    /// Gets the formatted local time label for when the item was copied.
    /// </summary>
    public string TimeLabel { get; }

    /// <summary>
    /// Gets whether the history item is pinned.
    /// </summary>
    public bool IsPinned { get; }

    /// <summary>
    /// Gets the underlying clipboard item model for host operations.
    /// </summary>
    public ClipboardItem Model { get; }

    /// <summary>
    /// Formats a timestamp for list display, using time-only for today and date plus time otherwise.
    /// </summary>
    /// <param name="at">The UTC or offset timestamp to format.</param>
    /// <returns>A localized display string.</returns>
    private static string FormatTime(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("MM/dd HH:mm");
    }
}
