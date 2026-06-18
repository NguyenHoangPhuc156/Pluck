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
/// Manages paste-by-drag using a small floating ghost window so drop targets remain real applications.
/// Clipboard preparation and paste occur on mouse up; only the ghost moves during the drag.
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

    /// <summary>
    /// Initializes a paste-drag session bound to the overlay and completion callback.
    /// </summary>
    /// <param name="overlay">Bubble overlay window used for coordinate context.</param>
    /// <param name="settings">Current application settings.</param>
    /// <param name="onComplete">Callback invoked when a drag finishes successfully.</param>
    public BubblePasteDragSession(
        BubbleOverlayWindow overlay,
        PluckSettings settings,
        Action<BubbleModel> onComplete)
    {
        _overlay = overlay;
        _settings = settings;
        _onComplete = onComplete;
    }

    /// <summary>
    /// Gets whether a paste-drag operation is currently in progress.
    /// </summary>
    public bool IsActive => _active;

    /// <summary>
    /// Replaces the settings snapshot used for drag window appearance.
    /// </summary>
    /// <param name="settings">Updated application settings.</param>
    public void UpdateSettings(PluckSettings settings) => _settings = settings;

    /// <summary>
    /// Pre-creates and hides a drag window so the first real paste-drag starts without JIT delay.
    /// </summary>
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

    /// <summary>
    /// Begins a paste-drag from a bubble, showing a ghost window that follows the cursor.
    /// </summary>
    /// <param name="sourceControl">The bubble control being dragged.</param>
    /// <param name="model">Bubble model associated with the drag.</param>
    /// <param name="grabOffsetInBubble">Cursor offset within the bubble at drag start, in DIP.</param>
    /// <param name="dragButton">Mouse button that initiated the drag.</param>
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

    /// <summary>
    /// Tracks cursor movement each frame and completes the drag when the button is released.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Rendering event arguments.</param>
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

    /// <summary>
    /// Positions the ghost drag window under the cursor using the stored grab offset.
    /// </summary>
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

    /// <summary>
    /// Ends the drag, restores the source bubble, and schedules paste at the drop location.
    /// </summary>
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

    /// <summary>
    /// Queues a guarded paste operation at the given physical screen coordinates.
    /// </summary>
    /// <param name="screenX">Physical X coordinate of the drop point.</param>
    /// <param name="screenY">Physical Y coordinate of the drop point.</param>
    /// <param name="item">Clipboard item to paste.</param>
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

    /// <summary>
    /// Aborts an active drag without pasting and restores the source bubble visibility.
    /// </summary>
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

    /// <summary>
    /// Unsubscribes from per-frame rendering updates.
    /// </summary>
    private void StopTracking()
    {
        if (_renderHandler is null)
            return;

        CompositionTarget.Rendering -= _renderHandler;
        _renderHandler = null;
    }

    /// <summary>
    /// Closes and unregisters the active drag window.
    /// </summary>
    private void CloseDragWindow()
    {
        if (_dragWindow is null)
            return;

        PluckWindowGuard.Instance.Unregister(_dragWindow);
        _dragWindow.Close();
        _dragWindow = null;
    }

    /// <summary>
    /// Creates a minimal bubble model used only for drag-window prewarming.
    /// </summary>
    /// <returns>A dummy bubble model with placeholder content.</returns>
    private static BubbleModel CreateDummyModel() => new()
    {
        Item = new ClipboardItem
        {
            Type = ClipboardItemType.Text,
            Preview = " "
        }
    };

    /// <summary>
    /// Determines whether the drag-initiating mouse button is still held down.
    /// </summary>
    /// <returns><see langword="true"/> when the drag button is pressed; otherwise <see langword="false"/>.</returns>
    private bool IsDragButtonDown() =>
        _dragButton switch
        {
            MouseButton.Right => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0,
            MouseButton.Middle => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MBUTTON) & 0x8000) != 0,
            _ => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0
        };

}
