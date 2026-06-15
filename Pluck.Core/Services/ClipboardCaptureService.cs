using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Clipboard = System.Windows.Clipboard;
using Pluck.Data.Models;
using Pluck.Data.Services;

namespace Pluck.Core.Services;

public sealed class ClipboardCaptureService
{
    private readonly ClipboardRepository _repository;

    public event EventHandler<ClipboardItem>? ItemCaptured;

    public ClipboardCaptureService(ClipboardRepository repository)
    {
        _repository = repository;
    }

    public void ProcessClipboardUpdate(IntPtr sourceWindow, string sourceAppName, string sourceProcessPath)
    {
        if (PasteService.Instance.IsCaptureSuppressed)
            return;

        var item = TryReadClipboard(sourceWindow, sourceAppName, sourceProcessPath);
        if (item is null)
            return;

        _repository.Insert(item);
        ItemCaptured?.Invoke(this, item);
    }

    private static ClipboardItem? TryReadClipboard(IntPtr sourceWindow, string sourceAppName, string sourceProcessPath)
    {
        if (!Clipboard.ContainsText() && !Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList())
            return null;

        var iconPng = SourceAppDetector.TryExtractIconPng(sourceProcessPath);
        var copiedAt = DateTimeOffset.Now;

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            var paths = new string[files.Count];
            files.CopyTo(paths, 0);
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

        if (Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image is null)
                return null;

            byte[] thumbPng;
            byte[] fullPng;
            try
            {
                (thumbPng, fullPng) = ImageThumbnailService.CreateThumbnailAndFull(image);
            }
            catch
            {
                return null;
            }

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

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText() ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return null;

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

        return null;
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..(max - 1)] + "…";
    }
}
