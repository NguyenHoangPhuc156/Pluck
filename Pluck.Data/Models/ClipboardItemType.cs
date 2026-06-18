namespace Pluck.Data.Models;

/// <summary>
/// Identifies the kind of data stored in a clipboard history entry.
/// </summary>
public enum ClipboardItemType
{
    /// <summary>Plain text or rich text content.</summary>
    Text,

    /// <summary>Bitmap image data.</summary>
    Image,

    /// <summary>One or more file system paths.</summary>
    Files,

    /// <summary>An unrecognized or unsupported clipboard format.</summary>
    Unknown
}
