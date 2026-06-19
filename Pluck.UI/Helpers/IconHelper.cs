using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Pluck.UI.Helpers;

/// <summary>
/// Loads embedded application icons for WPF windows and the notification area.
/// </summary>
public static class IconHelper
{
    private const string IconPngPackUri = "pack://application:,,,/Assets/app-icon.png";
    private const string IconIcoPackUri = "pack://application:,,,/Assets/app.ico";

    /// <summary>
    /// Loads the application PNG icon as a frozen <see cref="BitmapImage"/> for WPF chrome.
    /// </summary>
    /// <returns>A frozen bitmap suitable for assigning to <see cref="Window.Icon"/>.</returns>
    public static BitmapImage LoadAppIconImage()
    {
        var stream = TryOpenPackResource(IconPngPackUri);
        if (stream is not null)
        {
            using (stream)
                return DecodeBitmapImage(stream);
        }

        var fromExe = TryBitmapImageFromExeIcon();
        if (fromExe is not null)
            return fromExe;

        throw new InvalidOperationException($"App icon resource not found: {IconPngPackUri}");
    }

    /// <summary>
    /// Creates a 16×16 tray icon from the multi-size embedded ICO resource.
    /// </summary>
    /// <returns>A cloned <see cref="Icon"/> suitable for <see cref="System.Windows.Forms.NotifyIcon"/>.</returns>
    /// <remarks>Do not use <see cref="Bitmap.GetHicon"/> for the notification area; use a proper ICO instead.</remarks>
    public static Icon CreateTrayIcon()
    {
        var fromExe = TryTrayIconFromExe();
        if (fromExe is not null)
            return fromExe;

        var stream = TryOpenPackResource(IconIcoPackUri);
        if (stream is not null)
        {
            using (stream)
            {
                using var loaded = new Icon(stream, 16, 16);
                return (Icon)loaded.Clone();
            }
        }

        throw new InvalidOperationException($"App icon resource not found: {IconIcoPackUri}");
    }

    /// <summary>
    /// Decodes PNG bytes into a frozen WPF bitmap source.
    /// </summary>
    /// <param name="png">PNG-encoded image bytes, or null/empty to skip decoding.</param>
    /// <returns>The decoded image, or <see langword="null"/> when input is missing or invalid.</returns>
    public static BitmapSource? FromPngBytes(byte[]? png)
    {
        if (png is null || png.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(png);
            return DecodeBitmapImage(ms);
        }
        catch
        {
            return null;
        }
    }

    private static Icon? TryTrayIconFromExe()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return null;

        try
        {
            var icon = Icon.ExtractAssociatedIcon(exePath);
            return icon is null ? null : (Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? TryBitmapImageFromExeIcon()
    {
        try
        {
            using var icon = TryTrayIconFromExe();
            if (icon is null)
                return null;

            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return DecodeBitmapImage(ms);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage DecodeBitmapImage(Stream stream)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Opens an embedded pack URI resource stream, falling back to a file on disk when needed.
    /// </summary>
    /// <param name="packUri">Absolute pack URI of the resource to open.</param>
    /// <returns>A readable stream positioned at the start of the resource, or null when not found.</returns>
    private static Stream? TryOpenPackResource(string packUri)
    {
        try
        {
            var stream = Application.GetResourceStream(new Uri(packUri, UriKind.Absolute))?.Stream;
            if (stream is not null)
                return stream;
        }
        catch
        {
            // Pack URI lookups can fail in single-file published builds.
        }

        var fileName = packUri.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            ? "app.ico"
            : "app-icon.png";
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (File.Exists(path))
            return File.OpenRead(path);

        return null;
    }
}
