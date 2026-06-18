using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

/// <summary>
/// Registers and dispatches a global system hotkey via a hidden message-only window.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x504C; // PL
    private HwndSource? _source;
    private Action? _callback;

    /// <summary>
    /// Raised when the registered global hotkey is pressed.
    /// </summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey and associates it with the given callback.
    /// </summary>
    /// <param name="hotkeyDisplay">Human-readable hotkey string (for example, <c>Ctrl+Shift+V</c>).</param>
    /// <param name="callback">Action invoked when the hotkey is pressed.</param>
    /// <exception cref="InvalidOperationException">Thrown when hotkey registration fails.</exception>
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

    /// <summary>
    /// Re-registers the hotkey with a new key combination while preserving the existing callback.
    /// </summary>
    /// <param name="hotkeyDisplay">Human-readable hotkey string (for example, <c>Ctrl+Shift+V</c>).</param>
    public void Update(string hotkeyDisplay)
    {
        if (_callback is null)
            return;
        var cb = _callback;
        Stop();
        Register(hotkeyDisplay, cb);
    }

    /// <summary>
    /// Handles window messages and dispatches hotkey notifications.
    /// </summary>
    /// <param name="hwnd">Handle of the window receiving the message.</param>
    /// <param name="msg">The message identifier.</param>
    /// <param name="wParam">The message <paramref name="wParam"/> value.</param>
    /// <param name="lParam">The message <paramref name="lParam"/> value.</param>
    /// <param name="handled">Set to <see langword="true"/> when the message is handled.</param>
    /// <returns>The message result, or <see cref="IntPtr.Zero"/>.</returns>
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

    /// <summary>
    /// Parses a human-readable hotkey string into Win32 modifier flags and a virtual-key code.
    /// </summary>
    /// <param name="text">Hotkey text such as <c>Ctrl+Shift+V</c>.</param>
    /// <param name="modifiers">Receives the combined modifier flags when parsing succeeds.</param>
    /// <param name="vk">Receives the virtual-key code when parsing succeeds.</param>
    /// <returns><see langword="true"/> if the string was parsed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Unregisters the hotkey and disposes the hidden host window.
    /// </summary>
    public void Stop()
    {
        if (_source is null)
            return;
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    /// <summary>
    /// Releases hotkey resources by stopping registration.
    /// </summary>
    public void Dispose() => Stop();
}
