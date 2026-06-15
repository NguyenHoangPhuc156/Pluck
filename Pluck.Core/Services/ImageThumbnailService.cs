using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pluck.Core.Services;

public static class ImageThumbnailService
{
    public const int ThumbMaxWidth = 200;
    public const int ThumbMaxHeight = 150;

    public static (byte[] ThumbnailPng, byte[] FullPng) CreateThumbnailAndFull(BitmapSource source)
    {
        var fullPng = EncodePng(source);
        var thumb = CreateThumbnail(source);
        var thumbPng = EncodePng(thumb);
        return (thumbPng, fullPng);
    }

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

    private static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
