namespace Pluck.Core.Services;

/// <summary>
/// Helpers for interpreting clipboard format names and incidental text content.
/// </summary>
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

    /// <summary>
    /// Determines whether clipboard text should be ignored when an image is also present.
    /// </summary>
    /// <param name="text">The text content from the clipboard.</param>
    /// <param name="clipboardHasImage"><see langword="true"/> when the clipboard also contains an image.</param>
    /// <returns><see langword="true"/> if the text is empty or incidental placeholder text; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Builds a short human-readable label for unrecognized clipboard formats.
    /// </summary>
    /// <param name="formats">The raw format names present on the clipboard.</param>
    /// <returns>A friendly description of up to three unknown formats, or a generic fallback label.</returns>
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

    /// <summary>
    /// Determines whether a clipboard format name can be safely ignored when labeling unknown content.
    /// </summary>
    /// <param name="format">The clipboard format name.</param>
    /// <returns><see langword="true"/> if the format is known or ignorable; otherwise, <see langword="false"/>.</returns>
    private static bool IsIgnorableFormat(string format) =>
        KnownHandledFormats.Contains(format)
        || format.StartsWith("Ole", StringComparison.OrdinalIgnoreCase)
        || format.Contains("DataObject", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts a raw clipboard format name into a shorter, user-facing label.
    /// </summary>
    /// <param name="format">The clipboard format name.</param>
    /// <returns>A friendly display name for the format.</returns>
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
