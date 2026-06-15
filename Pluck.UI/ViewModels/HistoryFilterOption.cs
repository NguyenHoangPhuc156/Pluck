namespace Pluck.UI.ViewModels;

public sealed class HistoryFilterOption<T>
{
    public HistoryFilterOption(string label, T value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public T Value { get; }

    public override string ToString() => Label;
}
