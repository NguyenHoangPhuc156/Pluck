namespace Pluck.Core.Services;

internal static class ClipboardFormatHelper
{
    private static readonly HashSet<string> KnownHandledFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "Text",
        "UnicodeText",
        "System.String",
        "Bitmap",
        "PNG",
        "JFIF",
        "GIF",
        "TIFF",
        "FileDrop",
        "FileName",
        "FileNameW",
        "Preferred DropEffect",
        "DropDescription",
        "InShellDragLoop",
        "Chromium Web Custom MIME Data Format",
        "Chromium internal source RFH token",
        "Chromium internal source URL",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
        "ExcludeClipboardContentFromMonitorProcessing",
    };

    public static bool IsIncidentalText(string text, bool clipboardHasImage)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!clipboardHasImage)
            return false;

        // Screenshot / snip tools sometimes add tiny placeholder text alongside the bitmap.
        var trimmed = text.Trim();
        return trimmed.Length <= 4;
    }

    public static string DescribeUnknownFormats(IEnumerable<string> formats)
    {
        var labels = formats
            .Where(f => !IsIgnorableFormat(f))
            .Select(ToFriendlyName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (labels.Count == 0)
            return "Unsupported clip";

        return labels.Count == 1
            ? labels[0]
            : string.Join(" · ", labels);
    }

    private static bool IsIgnorableFormat(string format) =>
        KnownHandledFormats.Contains(format)
        || format.StartsWith("Ole", StringComparison.OrdinalIgnoreCase)
        || format.Contains("DataObject", StringComparison.OrdinalIgnoreCase);

    private static string ToFriendlyName(string format) => format switch
    {
        "HTML Format" or "HTML" => "HTML",
        "Rich Text Format" or "Rich Text" => "Rich Text",
        "Xaml" => "XAML",
        "Csv" => "CSV",
        "Audio" => "Audio",
        "Video" => "Video",
        _ when format.Contains("HTML", StringComparison.OrdinalIgnoreCase) => "HTML",
        _ when format.Contains("Rich Text", StringComparison.OrdinalIgnoreCase) => "Rich Text",
        _ when format.Length > 28 => format[..25] + "…",
        _ => format
    };
}
