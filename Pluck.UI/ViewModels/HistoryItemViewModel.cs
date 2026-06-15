using Pluck.Data.Models;

namespace Pluck.UI.ViewModels;

public sealed class HistoryItemViewModel
{
    public HistoryItemViewModel(ClipboardItem item)
    {
        Id = item.Id;
        TypeLabel = item.Type switch
        {
            ClipboardItemType.Text => "Text",
            ClipboardItemType.Image => "Image",
            ClipboardItemType.Files => "Files",
            _ => "Unknown"
        };
        Preview = item.Preview;
        SourceAppName = item.SourceAppName;
        TimeLabel = FormatTime(item.CopiedAt);
        IsPinned = item.IsPinned;
        Model = item;
    }

    public long Id { get; }
    public string TypeLabel { get; }
    public string Preview { get; }
    public string SourceAppName { get; }
    public string TimeLabel { get; }
    public bool IsPinned { get; }
    public ClipboardItem Model { get; }

    private static string FormatTime(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("MM/dd HH:mm");
    }
}
