using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x504C; // PL
    private HwndSource? _source;
    private Action? _callback;

    public event EventHandler? HotkeyPressed;

    public void Register(string hotkeyDisplay, Action callback)
    {
        _callback = callback;
        Stop();

        var parameters = new HwndSourceParameters("PluckHotkeyHost")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        if (!TryParseHotkey(hotkeyDisplay, out var mods, out var vk))
            TryParseHotkey("Ctrl+Shift+V", out mods, out vk);

        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, mods, vk))
            throw new InvalidOperationException($"Failed to register hotkey: {hotkeyDisplay}");
    }

    public void Update(string hotkeyDisplay)
    {
        if (_callback is null)
            return;
        var cb = _callback;
        Stop();
        Register(hotkeyDisplay, cb);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            _callback?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public static bool TryParseHotkey(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => NativeMethods.MOD_CONTROL,
                "alt" => NativeMethods.MOD_ALT,
                "shift" => NativeMethods.MOD_SHIFT,
                "win" => NativeMethods.MOD_WIN,
                _ => 0
            };
        }

        var keyPart = parts[^1];
        if (keyPart.Length == 1)
        {
            vk = char.ToUpperInvariant(keyPart[0]);
            return true;
        }

        vk = keyPart.ToUpperInvariant() switch
        {
            "V" => 0x56,
            "C" => 0x43,
            "X" => 0x58,
            "A" => 0x41,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            _ => 0
        };
        return vk != 0;
    }

    public void Stop()
    {
        if (_source is null)
            return;
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    public void Dispose() => Stop();
}
