using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Pluck.UI.Helpers;

public static class IconHelper
{
    private const string IconPngPackUri = "pack://application:,,,/Assets/app-icon.png";
    private const string IconIcoPackUri = "pack://application:,,,/Assets/app.ico";

    public static BitmapImage LoadAppIconImage()
    {
        var stream = OpenPackResource(IconPngPackUri);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        stream.Dispose();
        return image;
    }

    /// <summary>Proper multi-size .ico for the notification area (do not use Bitmap.GetHicon).</summary>
    public static Icon CreateTrayIcon()
    {
        using var stream = OpenPackResource(IconIcoPackUri);
        using var loaded = new Icon(stream, 16, 16);
        return (Icon)loaded.Clone();
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

    private static Stream OpenPackResource(string packUri)
    {
        var stream = System.Windows.Application.GetResourceStream(new Uri(packUri, UriKind.Absolute))?.Stream;
        if (stream is not null)
            return stream;

        // Fallback when running from output folder without pack URI (e.g. some publish layouts).
        var fileName = packUri.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            ? "app.ico"
            : "app-icon.png";
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (File.Exists(path))
            return File.OpenRead(path);

        throw new InvalidOperationException($"App icon resource not found: {packUri}");
    }
}
