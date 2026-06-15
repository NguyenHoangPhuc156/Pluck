using System.Windows;
using System.Windows.Threading;
using Pluck.UI.Helpers;
using Pluck.Core.Native;
using Pluck.Core.Services;
using Pluck.Data.Models;
using Pluck.UI.Controls;
using Pluck.UI.Models;
using Pluck.UI.Views;

namespace Pluck.UI.Services;

/// <summary>
/// Drag uses a small floating ghost window — NOT a fullscreen overlay — so drop targets real apps.
/// </summary>
public sealed class BubblePasteDragSession
{
    private readonly BubbleOverlayWindow _overlay;
    private PluckSettings _settings;
    private readonly Action<BubbleModel> _onComplete;

    private BubbleControl? _sourceControl;
    private BubbleModel? _model;
    private BubbleDragWindow? _dragWindow;
    private Point _grabOffset;
    private bool _active;
    private DispatcherTimer? _trackTimer;

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

    public void Start(
        BubbleControl sourceControl,
        BubbleModel model,
        Point grabOffsetInBubble)
    {
        if (_active)
            return;

        _sourceControl = sourceControl;
        _model = model;
        _grabOffset = grabOffsetInBubble;
        _active = true;

        sourceControl.Visibility = Visibility.Collapsed;

        _dragWindow = new BubbleDragWindow();
        PluckWindowGuard.Instance.Register(_dragWindow);
        _dragWindow.SetBubbleContent(model, _settings, _settings.OpacityPercent);
        _dragWindow.Show();
        _dragWindow.UpdateLayout();

        MoveDragWindowToCursor();

        _trackTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            OnTrackTick,
            System.Windows.Application.Current.Dispatcher);
        _trackTimer.Start();
    }

    private void OnTrackTick(object? sender, EventArgs e)
    {
        if (!_active || _dragWindow is null)
            return;

        if (!IsLeftButtonDown())
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
            cursorDip.X - _grabOffset.X,
            cursorDip.Y - _grabOffset.Y));
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

        if (model is not null && hasPoint)
        {
            PluckWindowGuard.Instance.RunHidden(() =>
            {
                PasteService.Instance.SuppressCaptureFor(3000);
                var root = WindowTargetService.FindExternalWindowAtPoint(screenX, screenY);
                PasteDiagnostics.LogDrop(screenX, screenY, root);

                if (root != IntPtr.Zero)
                    PasteService.Instance.PasteToWindow(root, model.Item, screenX, screenY);
            });

            _onComplete(model);
        }

        _sourceControl = null;
        _model = null;
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
        if (_trackTimer is null)
            return;

        _trackTimer.Stop();
        _trackTimer = null;
    }

    private void CloseDragWindow()
    {
        if (_dragWindow is null)
            return;

        PluckWindowGuard.Instance.Unregister(_dragWindow);
        _dragWindow.Close();
        _dragWindow = null;
    }

    private static bool IsLeftButtonDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0;
}
