using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Clipboard = System.Windows.Clipboard;
using Pluck.Core.Native;
using Pluck.Data.Models;

namespace Pluck.Core.Services;

public sealed class PasteService
{
    public static PasteService Instance { get; } = new();

    private long _suppressCaptureUntilTick;

    public void SuppressCaptureFor(int milliseconds = 2000) =>
        _suppressCaptureUntilTick = Math.Max(
            _suppressCaptureUntilTick,
            Environment.TickCount64 + milliseconds);

    public bool IsCaptureSuppressed => Environment.TickCount64 < _suppressCaptureUntilTick;

    /// <summary>Warm up WPF clipboard on the UI thread so first paste-drag is not delayed.</summary>
    public void PrewarmClipboard()
    {
        try
        {
            Clipboard.SetText(" ");
            Clipboard.Clear();
        }
        catch
        {
            // clipboard may be busy at startup
        }
    }

    public void CopyToClipboard(ClipboardItem item)
    {
        SuppressCaptureFor(2000);
        WriteClipboard(item);
    }

    /// <summary>Pre-stage clipboard at drag start so drop only needs focus + paste.</summary>
    public void PrepareClipboardForPaste(ClipboardItem item)
    {
        SuppressCaptureFor(3000);
        WriteClipboard(item);
    }

    public void PasteToForeground(ClipboardItem item)
    {
        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        PasteToPoint(pt.X, pt.Y, item);
    }

    public void PasteToPoint(int screenX, int screenY, ClipboardItem item)
    {
        var root = PasteTargetResolver.FindRootWindowAtPoint(screenX, screenY);
        if (root == IntPtr.Zero || WindowTargetService.IsPluckWindow(root))
            return;

        PasteToWindow(root, item, screenX, screenY);
    }

    public void PasteToWindow(
        IntPtr rootHwnd,
        ClipboardItem item,
        int? screenX = null,
        int? screenY = null,
        bool clipboardReady = false)
    {
        if (rootHwnd == IntPtr.Zero || WindowTargetService.IsPluckWindow(rootHwnd))
            return;

        SuppressCaptureFor(3000);

        var x = screenX ?? 0;
        var y = screenY ?? 0;
        if (screenX is null && NativeMethods.GetCursorPos(out var pt))
        {
            x = pt.X;
            y = pt.Y;
        }

        if (!clipboardReady)
            WriteClipboard(item);

        ActivateWindow(rootHwnd);

        if (screenX.HasValue && screenY.HasValue)
            ClickScreenPoint(x, y);
        else if (NativeMethods.GetCursorPos(out var cursor))
            ClickScreenPoint(cursor.X, cursor.Y);

        SendCtrlV();
        PasteDiagnostics.LogPaste(rootHwnd, x, y, clipboardReady ? "focus+click+ctrl+v" : "clipboard+click+ctrl+v");
        SuppressCaptureFor(3000);
    }

    private static void WriteClipboard(ClipboardItem item)
    {
        switch (item.Type)
        {
            case ClipboardItemType.Text:
                var text = item.TextContent ?? item.Preview ?? "";
                if (!string.IsNullOrEmpty(text))
                    Clipboard.SetText(text);
                break;
            case ClipboardItemType.Image when item.ImageFullPng is not null:
                using (var ms = new MemoryStream(item.ImageFullPng))
                {
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.StreamSource = ms;
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    Clipboard.SetImage(image);
                }
                break;
            case ClipboardItemType.Files when item.FilePathsJson is not null:
                var paths = JsonSerializer.Deserialize<string[]>(item.FilePathsJson);
                if (paths is { Length: > 0 })
                {
                    var collection = new System.Collections.Specialized.StringCollection();
                    collection.AddRange(paths);
                    Clipboard.SetFileDropList(collection);
                }
                break;
        }
    }

    private static void ClickScreenPoint(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
        Thread.Sleep(20);

        var inputs = new[]
        {
            MouseButton(NativeMethods.MOUSEEVENTF_LEFTDOWN),
            MouseButton(NativeMethods.MOUSEEVENTF_LEFTUP)
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        Thread.Sleep(35);
    }

    private static void ActivateWindow(IntPtr rootHwnd)
    {
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

        if (NativeMethods.IsIconic(rootHwnd))
            NativeMethods.ShowWindow(rootHwnd, NativeMethods.SW_RESTORE);

        var fg = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(fg, out var fgThread);
        NativeMethods.GetWindowThreadProcessId(rootHwnd, out var rootThread);

        if (fgThread != 0 && rootThread != 0 && fgThread != rootThread)
            NativeMethods.AttachThreadInput(fgThread, rootThread, true);

        NativeMethods.SetForegroundWindow(rootHwnd);
        NativeMethods.BringWindowToTop(rootHwnd);

        if (fgThread != 0 && rootThread != 0 && fgThread != rootThread)
            NativeMethods.AttachThreadInput(fgThread, rootThread, false);

        Thread.Sleep(60);
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyDown(NativeMethods.VK_CONTROL),
            KeyDown(NativeMethods.VK_V),
            KeyUp(NativeMethods.VK_V),
            KeyUp(NativeMethods.VK_CONTROL)
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT MouseButton(uint flags) => new()
    {
        Type = NativeMethods.INPUT_MOUSE,
        U = new NativeMethods.InputUnion
        {
            Mi = new NativeMethods.MOUSEINPUT { Flags = flags }
        }
    };

    private static NativeMethods.INPUT KeyDown(ushort vk) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            Ki = new NativeMethods.KEYBDINPUT { Vk = vk }
        }
    };

    private static NativeMethods.INPUT KeyUp(ushort vk) => new()
    {
        Type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            Ki = new NativeMethods.KEYBDINPUT
            {
                Vk = vk,
                Flags = NativeMethods.KEYEVENTF_KEYUP
            }
        }
    };
}
