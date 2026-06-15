using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Pluck.Core.Native;
using Pluck.Core.Services;
using Pluck.Data.Models;
using Pluck.UI.Controls;
using Pluck.UI.Helpers;
using Pluck.UI.Models;
using Pluck.UI.Views;

namespace Pluck.UI.Services;

/// <summary>
/// Drag uses a small floating ghost window — NOT a fullscreen overlay — so drop targets real apps.
/// During drag only the ghost moves; clipboard/paste run on mouse up.
/// </summary>
public sealed class BubblePasteDragSession
{
    private readonly BubbleOverlayWindow _overlay;
    private PluckSettings _settings;
    private readonly Action<BubbleModel> _onComplete;

    private BubbleControl? _sourceControl;
    private BubbleModel? _model;
    private BubbleDragWindow? _dragWindow;
    private BubbleDragWindow? _prewarmedWindow;
    private Point _grabOffsetDip;
    private MouseButton _dragButton = MouseButton.Left;
    private bool _active;
    private EventHandler? _renderHandler;
    private TimeSpan _lastRenderTime = TimeSpan.MinValue;

    public BubblePasteDragSession(
        BubbleOverlayWindow overlay,
        PluckSettings settings,
        Action<BubbleModel> onComplete)
    {
        _overlay = overlay;
        _settings = settings;
        _onComplete = onComplete;
    }

    public bool IsActive => _active;

    public void UpdateSettings(PluckSettings settings) => _settings = settings;

    /// <summary>JIT WPF window + bubble binding so the first real paste-drag is instant.</summary>
    public void Prewarm()
    {
        if (_prewarmedWindow is not null || _active)
            return;

        _prewarmedWindow = new BubbleDragWindow();
        PluckWindowGuard.Instance.Register(_prewarmedWindow);
        _prewarmedWindow.SetBubbleContent(CreateDummyModel(), _settings, _settings.OpacityPercent);
        _prewarmedWindow.Opacity = 0;
        _prewarmedWindow.Show();
        _prewarmedWindow.Hide();
        _prewarmedWindow.Opacity = 1;
        _prewarmedWindow.FreezeSize();
    }

    public void Start(
        BubbleControl sourceControl,
        BubbleModel model,
        Point grabOffsetInBubble,
        MouseButton dragButton = MouseButton.Left)
    {
        if (_active)
            return;

        _sourceControl = sourceControl;
        _model = model;
        _grabOffsetDip = grabOffsetInBubble;
        _dragButton = dragButton;

        if (sourceControl.ActualWidth > 1)
            model.CustomWidth = sourceControl.ActualWidth;
        if (sourceControl.ActualHeight > 1)
            model.CustomHeight = sourceControl.ActualHeight;

        _active = true;
        _lastRenderTime = TimeSpan.MinValue;

        sourceControl.Visibility = Visibility.Collapsed;

        if (_prewarmedWindow is not null)
        {
            _dragWindow = _prewarmedWindow;
            _prewarmedWindow = null;
            _dragWindow.SetBubbleContent(model, _settings, _settings.OpacityPercent);
            _dragWindow.Opacity = Math.Clamp(_settings.OpacityPercent, 10, 90) / 100.0;
        }
        else
        {
            _dragWindow = new BubbleDragWindow();
            PluckWindowGuard.Instance.Register(_dragWindow);
            _dragWindow.SetBubbleContent(model, _settings, _settings.OpacityPercent);
        }

        _dragWindow.Show();
        _dragWindow.FreezeSize();

        MoveDragWindowToCursor();

        _renderHandler = OnRendering;
        CompositionTarget.Rendering += _renderHandler;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_active || _dragWindow is null)
            return;

        if (e is RenderingEventArgs renderArgs)
        {
            if (renderArgs.RenderingTime == _lastRenderTime)
                return;
            _lastRenderTime = renderArgs.RenderingTime;
        }

        if (!IsDragButtonDown())
        {
            CompleteDrag();
            return;
        }

        MoveDragWindowToCursor();
    }

    private void MoveDragWindowToCursor()
    {
        if (_dragWindow is null || !NativeMethods.GetCursorPos(out var pt))
            return;

        var cursorDip = ScreenCoordinateHelper.PhysicalScreenToDip(
            new Point(pt.X, pt.Y),
            _dragWindow);

        _dragWindow.MoveToScreen(new Point(
            cursorDip.X - _grabOffsetDip.X,
            cursorDip.Y - _grabOffsetDip.Y));
    }

    private void CompleteDrag()
    {
        if (!_active)
            return;

        StopTracking();

        var model = _model;
        var screenX = 0;
        var screenY = 0;
        var hasPoint = NativeMethods.GetCursorPos(out var pt);
        if (hasPoint)
        {
            screenX = pt.X;
            screenY = pt.Y;
        }

        CloseDragWindow();

        if (_sourceControl is not null)
            _sourceControl.Visibility = Visibility.Visible;

        _active = false;

        if (model is not null)
        {
            _onComplete(model);
            if (hasPoint)
                SchedulePaste(screenX, screenY, model.Item);
        }

        _sourceControl = null;
        _model = null;

        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => Prewarm());
    }

    private static void SchedulePaste(int screenX, int screenY, ClipboardItem item)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                PluckWindowGuard.Instance.RunHidden(() =>
                {
                    PasteService.Instance.SuppressCaptureFor(3000);
                    PasteService.Instance.PrepareClipboardForPaste(item);
                    var root = WindowTargetService.FindExternalWindowAtPoint(screenX, screenY);
                    PasteDiagnostics.LogDrop(screenX, screenY, root);

                    if (root != IntPtr.Zero)
                        PasteService.Instance.PasteToWindow(root, item, screenX, screenY, clipboardReady: true);
                });
            });
    }

    public void Cancel()
    {
        if (!_active)
            return;

        StopTracking();
        CloseDragWindow();

        if (_sourceControl is not null)
            _sourceControl.Visibility = Visibility.Visible;

        _active = false;
        _sourceControl = null;
        _model = null;
    }

    private void StopTracking()
    {
        if (_renderHandler is null)
            return;

        CompositionTarget.Rendering -= _renderHandler;
        _renderHandler = null;
    }

    private void CloseDragWindow()
    {
        if (_dragWindow is null)
            return;

        PluckWindowGuard.Instance.Unregister(_dragWindow);
        _dragWindow.Close();
        _dragWindow = null;
    }

    private static BubbleModel CreateDummyModel() => new()
    {
        Item = new ClipboardItem
        {
            Type = ClipboardItemType.Text,
            Preview = " "
        }
    };

    private bool IsDragButtonDown() =>
        _dragButton switch
        {
            MouseButton.Right => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0,
            MouseButton.Middle => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MBUTTON) & 0x8000) != 0,
            _ => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0
        };

}
