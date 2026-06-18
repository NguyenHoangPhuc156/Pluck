using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;
using Pluck.Data.Models;
using Pluck.Data.Services;

namespace Pluck.Core.Services;

/// <summary>
/// Debounces clipboard updates, reads clipboard content, and persists captured items.
/// </summary>
public sealed class ClipboardCaptureService
{
    private const int DebounceMilliseconds = 280;
    private const int DuplicateWindowMilliseconds = 2500;

    private readonly ClipboardRepository _repository;
    private readonly DispatcherTimer _debounceTimer;

    private int _deferPasses;

    private IntPtr _pendingSourceWindow;
    private string _pendingSourceAppName = "";
    private string _pendingSourceProcessPath = "";

    private string? _lastFingerprint;
    private long _lastFingerprintTick;

    /// <summary>
    /// Raised when a new clipboard item has been captured and stored.
    /// </summary>
    public event EventHandler<ClipboardItem>? ItemCaptured;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardCaptureService"/> class.
    /// </summary>
    /// <param name="repository">The repository used to persist captured clipboard items.</param>
    public ClipboardCaptureService(ClipboardRepository repository)
    {
        _repository = repository;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds) };
        _debounceTimer.Tick += (_, _) => FlushPendingCapture();
    }

    /// <summary>
    /// Coalesces rapid WM_CLIPBOARDUPDATE bursts (e.g. screenshots registering several formats).
    /// </summary>
    /// <param name="sourceWindow">Handle of the window that owned the foreground when the update occurred.</param>
    /// <param name="sourceAppName">Short display name of the source application.</param>
    /// <param name="sourceProcessPath">Full path to the source application executable.</param>
    public void ProcessClipboardUpdate(IntPtr sourceWindow, string sourceAppName, string sourceProcessPath)
    {
        if (PasteService.Instance.IsCaptureSuppressed)
            return;

        _pendingSourceWindow = sourceWindow;
        _pendingSourceAppName = sourceAppName;
        _pendingSourceProcessPath = sourceProcessPath;

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>
    /// Reads the pending clipboard state, deduplicates recent captures, and persists a new item when applicable.
    /// </summary>
    private void FlushPendingCapture()
    {
        _debounceTimer.Stop();

        if (PasteService.Instance.IsCaptureSuppressed)
            return;

        if (HasPendingImageFormats() && _deferPasses < 4)
        {
            _deferPasses++;
            _debounceTimer.Start();
            return;
        }

        _deferPasses = 0;

        var item = TryReadClipboard(_pendingSourceWindow, _pendingSourceAppName, _pendingSourceProcessPath);
        if (item is null)
            return;

        var fingerprint = ComputeFingerprint(item);
        var now = Environment.TickCount64;
        if (fingerprint == _lastFingerprint && now - _lastFingerprintTick < DuplicateWindowMilliseconds)
            return;

        _lastFingerprint = fingerprint;
        _lastFingerprintTick = now;

        _repository.Insert(item);
        ItemCaptured?.Invoke(this, item);
    }

    /// <summary>
    /// Reads the current clipboard content and maps it to a <see cref="ClipboardItem"/> when supported.
    /// </summary>
    /// <param name="sourceWindow">Handle of the source window associated with the capture.</param>
    /// <param name="sourceAppName">Short display name of the source application.</param>
    /// <param name="sourceProcessPath">Full path to the source application executable.</param>
    /// <returns>A captured clipboard item, or <see langword="null"/> when nothing usable is present.</returns>
    private static ClipboardItem? TryReadClipboard(IntPtr sourceWindow, string sourceAppName, string sourceProcessPath)
    {
        var iconPng = SourceAppDetector.TryExtractIconPng(sourceProcessPath);
        var copiedAt = DateTimeOffset.Now;
        var hasImage = Clipboard.ContainsImage();

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            var paths = new string[files.Count];
            files.CopyTo(paths, 0);
            if (paths.Length > 0)
            {
                var preview = paths.Length == 1 ? paths[0] : $"{paths.Length} files";
                return new ClipboardItem
                {
                    Type = ClipboardItemType.Files,
                    Preview = Truncate(preview, 60),
                    FilePathsJson = JsonSerializer.Serialize(paths),
                    SourceAppName = sourceAppName,
                    SourceAppIconPng = iconPng,
                    SourceWindowHandle = sourceWindow,
                    CopiedAt = copiedAt
                };
            }
        }

        if (hasImage)
        {
            var imageItem = TryReadImageItem(sourceWindow, sourceAppName, iconPng, copiedAt);
            if (imageItem is not null)
                return imageItem;
        }

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText() ?? "";
            if (!ClipboardFormatHelper.IsIncidentalText(text, hasImage))
            {
                return new ClipboardItem
                {
                    Type = ClipboardItemType.Text,
                    Preview = Truncate(text.ReplaceLineEndings(" "), 60),
                    TextContent = text,
                    SourceAppName = sourceAppName,
                    SourceAppIconPng = iconPng,
                    SourceWindowHandle = sourceWindow,
                    CopiedAt = copiedAt
                };
            }
        }

        if (hasImage)
            return null;

        return TryReadUnknownItem(sourceWindow, sourceAppName, iconPng, copiedAt);
    }

    /// <summary>
    /// Determines whether image-related formats are present before WPF exposes a decoded image.
    /// </summary>
    /// <returns><see langword="true"/> when pending image formats suggest a deferred capture should wait.</returns>
    private static bool HasPendingImageFormats()
    {
        if (Clipboard.ContainsImage())
            return false;

        var data = Clipboard.GetDataObject();
        if (data is null)
            return false;

        foreach (var format in data.GetFormats(autoConvert: false))
        {
            if (format.Equals("Bitmap", StringComparison.OrdinalIgnoreCase)
                || format.Equals("PNG", StringComparison.OrdinalIgnoreCase)
                || format.Equals("DeviceIndependentBitmap", StringComparison.OrdinalIgnoreCase)
                || format.Contains("DIB", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to read an image from the clipboard and encode thumbnail and full PNG payloads.
    /// </summary>
    /// <param name="sourceWindow">Handle of the source window associated with the capture.</param>
    /// <param name="sourceAppName">Short display name of the source application.</param>
    /// <param name="iconPng">Optional PNG bytes for the source application icon.</param>
    /// <param name="copiedAt">Timestamp when the clipboard content was read.</param>
    /// <returns>A captured image item, or <see langword="null"/> when reading or encoding fails.</returns>
    private static ClipboardItem? TryReadImageItem(
        IntPtr sourceWindow,
        string sourceAppName,
        byte[]? iconPng,
        DateTimeOffset copiedAt)
    {
        var image = Clipboard.GetImage();
        if (image is null)
            return null;

        try
        {
            var (thumbPng, fullPng) = ImageThumbnailService.CreateThumbnailAndFull(image);
            return new ClipboardItem
            {
                Type = ClipboardItemType.Image,
                Preview = "Image",
                ImageThumbnailPng = thumbPng,
                ImageFullPng = fullPng,
                SourceAppName = sourceAppName,
                SourceAppIconPng = iconPng,
                SourceWindowHandle = sourceWindow,
                CopiedAt = copiedAt
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates an unknown-type clipboard item labeled from unrecognized format names.
    /// </summary>
    /// <param name="sourceWindow">Handle of the source window associated with the capture.</param>
    /// <param name="sourceAppName">Short display name of the source application.</param>
    /// <param name="iconPng">Optional PNG bytes for the source application icon.</param>
    /// <param name="copiedAt">Timestamp when the clipboard content was read.</param>
    /// <returns>An unknown clipboard item, or <see langword="null"/> when no formats are available.</returns>
    private static ClipboardItem? TryReadUnknownItem(
        IntPtr sourceWindow,
        string sourceAppName,
        byte[]? iconPng,
        DateTimeOffset copiedAt)
    {
        var data = Clipboard.GetDataObject();
        if (data is null)
            return null;

        var formats = data.GetFormats(autoConvert: false);
        if (formats.Length == 0)
            return null;

        if (HasPendingImageFormats())
            return null;

        var label = ClipboardFormatHelper.DescribeUnknownFormats(formats);
        return new ClipboardItem
        {
            Type = ClipboardItemType.Unknown,
            Preview = label,
            SourceAppName = sourceAppName,
            SourceAppIconPng = iconPng,
            SourceWindowHandle = sourceWindow,
            CopiedAt = copiedAt
        };
    }

    /// <summary>
    /// Computes a stable fingerprint string used to suppress duplicate captures.
    /// </summary>
    /// <param name="item">The clipboard item to fingerprint.</param>
    /// <returns>A type-specific fingerprint string.</returns>
    private static string ComputeFingerprint(ClipboardItem item)
    {
        return item.Type switch
        {
            ClipboardItemType.Text => $"t:{HashText(item.TextContent ?? item.Preview)}",
            ClipboardItemType.Image => $"i:{HashBytes(item.ImageFullPng)}",
            ClipboardItemType.Files => $"f:{item.FilePathsJson}",
            _ => $"u:{item.Preview}"
        };
    }

    /// <summary>
    /// Computes a short hash fingerprint for text content.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>A hexadecimal hash prefix suitable for deduplication.</returns>
    private static string HashText(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return HashBytes(bytes);
    }

    /// <summary>
    /// Computes a short hash fingerprint for binary content.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>A hexadecimal hash prefix, or <c>empty</c> when input is null or empty.</returns>
    private static string HashBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return "empty";

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    /// <summary>
    /// Truncates a string to a maximum length, appending an ellipsis when shortened.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="max">The maximum allowed length including the ellipsis character.</param>
    /// <returns>The original string or a truncated variant.</returns>
    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..(max - 1)] + "…";
    }
}
