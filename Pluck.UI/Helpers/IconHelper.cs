using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Pluck.Core.Native;

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

    /// <summary>
    /// Creates a 16×16 tray icon from the multi-size embedded ICO resource.
    /// </summary>
    /// <returns>A cloned <see cref="Icon"/> suitable for legacy tray hosts.</returns>
    /// <remarks>Do not use <see cref="Bitmap.GetHicon"/> for the notification area; use a proper ICO instead.</remarks>
    public static Icon CreateTrayIcon()
    {
        using var stream = OpenPackResource(IconIcoPackUri);
        using var loaded = new Icon(stream, 16, 16);
        return (Icon)loaded.Clone();
    }

    /// <summary>
    /// Loads a 16×16 tray icon handle for the Shell notification area API.
    /// </summary>
    /// <returns>An icon handle that must be destroyed with <see cref="NativeMethods.DestroyIcon"/>.</returns>
    public static IntPtr LoadTrayIconHandle()
    {
        try
        {
            using var icon = CreateTrayIcon();
            var copied = NativeMethods.CopyIcon(icon.Handle);
            if (copied != IntPtr.Zero)
                return copied;
        }
        catch
        {
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(path))
        {
            var handle = NativeMethods.LoadImage(
                IntPtr.Zero,
                path,
                NativeMethods.IMAGE_ICON,
                16,
                16,
                NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);

            if (handle != IntPtr.Zero)
                return handle;
        }

        throw new InvalidOperationException("Could not load the tray icon.");
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

    /// <summary>
    /// Opens an embedded pack URI resource stream, falling back to a file on disk when needed.
    /// </summary>
    /// <param name="packUri">Absolute pack URI of the resource to open.</param>
    /// <returns>A readable stream positioned at the start of the resource.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource cannot be found.</exception>
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
