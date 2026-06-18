using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pluck.Core.Services;

/// <summary>
/// Creates scaled thumbnail PNG data and full-size PNG encodings from clipboard images.
/// </summary>
public static class ImageThumbnailService
{
    /// <summary>
    /// Maximum thumbnail width in pixels.
    /// </summary>
    public const int ThumbMaxWidth = 200;

    /// <summary>
    /// Maximum thumbnail height in pixels.
    /// </summary>
    public const int ThumbMaxHeight = 150;

    /// <summary>
    /// Encodes a full-size PNG and a bounded thumbnail PNG from a bitmap source.
    /// </summary>
    /// <param name="source">The source image to encode.</param>
    /// <returns>A tuple containing the thumbnail PNG bytes and the full-size PNG bytes.</returns>
    public static (byte[] ThumbnailPng, byte[] FullPng) CreateThumbnailAndFull(BitmapSource source)
    {
        var fullPng = EncodePng(source);
        var thumb = CreateThumbnail(source);
        var thumbPng = EncodePng(thumb);
        return (thumbPng, fullPng);
    }

    /// <summary>
    /// Scales the source image down to fit within the thumbnail bounds when necessary.
    /// </summary>
    /// <param name="source">The source image.</param>
    /// <returns>A thumbnail-sized bitmap, or the original source when already within bounds.</returns>
    private static BitmapSource CreateThumbnail(BitmapSource source)
    {
        var scale = Math.Min(
            (double)ThumbMaxWidth / source.PixelWidth,
            (double)ThumbMaxHeight / source.PixelHeight);
        if (scale >= 1.0)
            return source;

        var w = Math.Max(1, (int)(source.PixelWidth * scale));
        var h = Math.Max(1, (int)(source.PixelHeight * scale));
        var tb = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        return new CroppedBitmap(tb, new Int32Rect(0, 0, w, h));
    }

    /// <summary>
    /// Encodes a bitmap source as PNG bytes.
    /// </summary>
    /// <param name="source">The bitmap to encode.</param>
    /// <returns>PNG-encoded image bytes.</returns>
    private static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
