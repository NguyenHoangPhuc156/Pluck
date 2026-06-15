namespace Pluck.Data.Models;

public enum HistoryTimeRange
{
    All,
    Last24Hours,
    Today,
    Last7Days,
    Last30Days
}

public sealed class HistorySearchCriteria
{
    public string? SearchText { get; set; }
    public string? SourceAppName { get; set; }
    public ClipboardItemType? Type { get; set; }
    public HistoryTimeRange TimeRange { get; set; } = HistoryTimeRange.All;
    public int Limit { get; set; } = 500;
}
