using System.Runtime.InteropServices;
using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

/// <summary>
/// Listens for system clipboard change notifications via a hidden message window.
/// </summary>
public sealed class ClipboardMonitor : IDisposable
{
    private HwndSource? _hwndSource;
    private bool _listening;

    /// <summary>
    /// Raised when the system clipboard content changes.
    /// </summary>
    public event EventHandler? ClipboardChanged;

    /// <summary>
    /// Starts listening for clipboard update notifications.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when clipboard listener registration fails.</exception>
    public void Start()
    {
        if (_listening)
            return;

        var parameters = new HwndSourceParameters("PluckClipboardListener")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = IntPtr.Zero
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        if (!NativeMethods.AddClipboardFormatListener(_hwndSource.Handle))
            throw new InvalidOperationException("AddClipboardFormatListener failed.");

        _listening = true;
    }

    /// <summary>
    /// Stops listening for clipboard update notifications and disposes the host window.
    /// </summary>
    public void Stop()
    {
        if (!_listening || _hwndSource is null)
            return;

        NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
        _hwndSource = null;
        _listening = false;
    }

    /// <summary>
    /// Handles window messages and raises <see cref="ClipboardChanged"/> on clipboard updates.
    /// </summary>
    /// <param name="hwnd">Handle of the window receiving the message.</param>
    /// <param name="msg">The message identifier.</param>
    /// <param name="wParam">The message <paramref name="wParam"/> value.</param>
    /// <param name="lParam">The message <paramref name="lParam"/> value.</param>
    /// <param name="handled">Set to <see langword="true"/> when the message is handled.</param>
    /// <returns>The message result, or <see cref="IntPtr.Zero"/>.</returns>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Releases clipboard listener resources by stopping monitoring.
    /// </summary>
    public void Dispose() => Stop();
}
