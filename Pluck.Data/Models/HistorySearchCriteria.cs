namespace Pluck.Data.Models;

/// <summary>
/// Predefined time windows for filtering clipboard history queries.
/// </summary>
public enum HistoryTimeRange
{
    /// <summary>No time filter; all history entries are eligible.</summary>
    All,

    /// <summary>Entries copied within the last 24 hours.</summary>
    Last24Hours,

    /// <summary>Entries copied since the start of the current local calendar day.</summary>
    Today,

    /// <summary>Entries copied within the last 7 days.</summary>
    Last7Days,

    /// <summary>Entries copied within the last 30 days.</summary>
    Last30Days
}

/// <summary>
/// Filter and pagination options for searching clipboard history.
/// </summary>
public sealed class HistorySearchCriteria
{
    /// <summary>
    /// Gets or sets optional text matched against preview, text content, and file path fields.
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Gets or sets an optional exact source application name filter.
    /// </summary>
    public string? SourceAppName { get; set; }

    /// <summary>
    /// Gets or sets an optional clipboard item type filter.
    /// </summary>
    public ClipboardItemType? Type { get; set; }

    /// <summary>
    /// Gets or sets the time window used to restrict results by copy time.
    /// </summary>
    public HistoryTimeRange TimeRange { get; set; } = HistoryTimeRange.All;

    /// <summary>
    /// Gets or sets the maximum number of matching entries to return, ordered by most recent first.
    /// </summary>
    public int Limit { get; set; } = 500;
}
