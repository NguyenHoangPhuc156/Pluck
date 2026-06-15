using System.Runtime.InteropServices;
using System.Windows.Interop;
using Pluck.Core.Native;

namespace Pluck.Core.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private HwndSource? _hwndSource;
    private bool _listening;

    public event EventHandler? ClipboardChanged;

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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose() => Stop();
}
