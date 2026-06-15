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

    public event EventHandler<ClipboardItem>? ItemCaptured;

    public ClipboardCaptureService(ClipboardRepository repository)
    {
        _repository = repository;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds) };
        _debounceTimer.Tick += (_, _) => FlushPendingCapture();
    }

    /// <summary>
    /// Coalesces rapid WM_CLIPBOARDUPDATE bursts (e.g. screenshots registering several formats).
    /// </summary>
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

    private static string HashText(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return HashBytes(bytes);
    }

    private static string HashBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return "empty";

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..(max - 1)] + "…";
    }
}
