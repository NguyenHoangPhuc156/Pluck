using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Pluck.UI.Helpers;

public static class IconHelper
{
    public static BitmapImage LoadAppIconImage()
    {
        var uri = new Uri("pack://application:,,,/Assets/app-icon.png", UriKind.Absolute);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static System.Drawing.Icon CreateTrayIcon()
    {
        var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app-icon.png"))?.Stream
            ?? throw new InvalidOperationException("App icon resource not found.");
        using var bitmap = new Bitmap(stream);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public static BitmapSource? FromPngBytes(byte[]? png)
    {
        if (png is null || png.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = ms;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
