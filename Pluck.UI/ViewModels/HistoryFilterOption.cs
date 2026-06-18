namespace Pluck.UI.ViewModels;

/// <summary>
/// Represents a labeled filter value for populating history filter combo boxes.
/// </summary>
/// <typeparam name="T">The underlying filter value type.</typeparam>
public sealed class HistoryFilterOption<T>
{
    /// <summary>
    /// Initializes a filter option with display text and value.
    /// </summary>
    /// <param name="label">Text shown in the UI.</param>
    /// <param name="value">Filter value applied when this option is selected.</param>
    public HistoryFilterOption(string label, T value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>
    /// Gets the display label for this filter option.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the filter value associated with this option.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Returns the display label for combo box rendering.
    /// </summary>
    /// <returns>The option label.</returns>
    public override string ToString() => Label;
}
