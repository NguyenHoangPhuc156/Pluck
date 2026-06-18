using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Pluck.Core.Native;
using Pluck.UI.Controls;
using Pluck.UI.Helpers;
using Pluck.UI.Models;

namespace Pluck.UI.Views;

/// <summary>
/// Full virtual-screen overlay that hosts floating bubbles with click-through on empty areas.
/// </summary>
public partial class BubbleOverlayWindow : Window
{
    /// <summary>
    /// Default bubble width in device-independent pixels when no custom width is set.
    /// </summary>
    public const double BubbleWidth = 220;

    /// <summary>
    /// Vertical spacing between stacked bubbles in device-independent pixels.
    /// </summary>
    public const double BubbleMargin = 10;

    /// <summary>
    /// Top padding from the primary monitor working area when stacking bubbles.
    /// </summary>
    public const double TopPadding = 12;

    /// <summary>
    /// Right padding from the primary monitor working area when stacking bubbles.
    /// </summary>
    public const double RightPadding = 8;

    private HwndSource? _hwndSource;

    /// <summary>
    /// Initializes the overlay, hooks source initialization, and ensures virtual-screen sizing on load.
    /// </summary>
    public BubbleOverlayWindow()
    {
        InitializeComponent();
        WindowChromeHelper.HideFromAltTab(this);
        Loaded += (_, _) => EnsureVirtualScreenMode();
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Sizes and positions the window to cover the full virtual desktop.
    /// </summary>
    public void EnsureVirtualScreenMode()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    /// <summary>
    /// Converts a screen DIP point to overlay canvas coordinates.
    /// </summary>
    /// <param name="screenPoint">Point in screen device-independent pixels.</param>
    /// <returns>The equivalent point in canvas coordinates.</returns>
    public Point ScreenToCanvas(Point screenPoint) => PointFromScreen(screenPoint);

    /// <summary>
    /// Converts an overlay canvas point to screen DIP coordinates.
    /// </summary>
    /// <param name="canvasPoint">Point in canvas coordinates.</param>
    /// <returns>The equivalent point in screen device-independent pixels.</returns>
    public Point CanvasToScreen(Point canvasPoint) => PointToScreen(canvasPoint);

    /// <summary>
    /// Lays out visible bubbles in a primary-monitor stack or at user-defined screen positions.
    /// </summary>
    /// <param name="items">Bubble controls and models to position on the canvas.</param>
    public void LayoutBubbleStack(IReadOnlyList<(BubbleControl Control, BubbleModel Model)> items)
    {
        var stackCanvas = MonitorHelper.GetPrimaryBubbleStackCanvasPoint(this, BubbleWidth, RightPadding, TopPadding);
        var y = stackCanvas.Y;

        foreach (var (control, model) in items)
        {
            var bubbleWidth = model.CustomWidth > 0 ? model.CustomWidth : BubbleWidth;
            control.ApplySize(bubbleWidth, model.CustomHeight);

            if (model.HasUserPosition)
            {
                var canvasPt = ScreenToCanvas(new Point(model.ScreenLeft, model.ScreenTop));
                Canvas.SetLeft(control, canvasPt.X);
                Canvas.SetTop(control, canvasPt.Y);
                control.ClearMoveTransform();
                model.LayoutY = canvasPt.Y;
                continue;
            }

            var stackLeft = MonitorHelper.GetPrimaryBubbleStackCanvasPoint(this, bubbleWidth, RightPadding, TopPadding).X;
            control.Measure(new System.Windows.Size(bubbleWidth, double.PositiveInfinity));
            var height = model.CustomHeight > 0
                ? model.CustomHeight
                : Math.Max(control.DesiredSize.Height, control.ActualHeight);
            if (height < 1)
                height = 64;

            Canvas.SetLeft(control, stackLeft);
            Canvas.SetTop(control, y);
            control.ClearMoveTransform();
            model.LayoutY = y;
            y += height + BubbleMargin;
        }
    }

    /// <summary>
    /// Shows or hides the collapsed-stack count badge and positions it on an anchor bubble when provided.
    /// </summary>
    /// <param name="count">Total number of bubbles in the stack.</param>
    /// <param name="showBadge">Whether the badge should be visible.</param>
    /// <param name="anchorBubble">Optional bubble used to position the badge; defaults to the primary stack anchor.</param>
    public void UpdateStackBadge(int count, bool showBadge, BubbleControl? anchorBubble = null)
    {
        if (!showBadge)
        {
            StackBadge.Visibility = Visibility.Collapsed;
            return;
        }

        StackCountText.Text = $"{count}";
        StackBadge.Visibility = Visibility.Visible;
        StackBadge.UpdateLayout();

        if (anchorBubble is not null)
            PositionStackBadgeOnBubble(anchorBubble);
        else
            PositionStackBadgeOnPrimaryStack();
    }

    /// <summary>
    /// Positions the stack badge near the top-right corner of the given bubble.
    /// </summary>
    /// <param name="bubble">Anchor bubble control.</param>
    private void PositionStackBadgeOnBubble(BubbleControl bubble)
    {
        bubble.UpdateLayout();

        var bubblePos = bubble.GetCanvasPosition();
        var bubbleLeft = bubblePos.X;
        var bubbleTop = bubblePos.Y;
        var bubbleWidth = bubble.ActualWidth > 1 ? bubble.ActualWidth : BubbleWidth;
        var badgeWidth = StackBadge.ActualWidth > 1 ? StackBadge.ActualWidth : 40;
        var badgeHeight = StackBadge.ActualHeight > 1 ? StackBadge.ActualHeight : 28;

        Canvas.SetLeft(StackBadge, bubbleLeft + bubbleWidth - badgeWidth * 0.55);
        Canvas.SetTop(StackBadge, bubbleTop - badgeHeight * 0.45);
    }

    /// <summary>
    /// Positions the stack badge at the default primary-monitor stack anchor.
    /// </summary>
    private void PositionStackBadgeOnPrimaryStack()
    {
        var badgeAnchor = MonitorHelper.GetPrimaryBubbleStackCanvasPoint(this, 48, RightPadding + 4, TopPadding - 4);
        Canvas.SetLeft(StackBadge, badgeAnchor.X);
        Canvas.SetTop(StackBadge, badgeAnchor.Y);
    }

    /// <summary>
    /// Hooks the window procedure after the HWND is created for click-through hit testing.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event data.</param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>
    /// Handles WM_NCHITTEST to pass mouse input through transparent areas while capturing hits on bubbles.
    /// </summary>
    /// <param name="hwnd">Window handle receiving the message.</param>
    /// <param name="msg">Message identifier.</param>
    /// <param name="wParam">Message wParam.</param>
    /// <param name="lParam">Message lParam containing screen coordinates.</param>
    /// <param name="handled">Set to true when the hit-test result is handled.</param>
    /// <returns>Hit-test code forwarded to Windows, or zero when unhandled.</returns>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_NCHITTEST)
            return IntPtr.Zero;

        var lx = (short)(lParam.ToInt32() & 0xFFFF);
        var ly = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
        var clientPt = PointFromScreen(new Point(lx, ly));
        var hit = InputHitTest(clientPt);

        if (IsInteractiveHit(hit))
        {
            handled = true;
            return new IntPtr(NativeMethods.HTCLIENT);
        }

        handled = true;
        return new IntPtr(NativeMethods.HTTRANSPARENT);
    }

    /// <summary>
    /// Determines whether a visual hit target belongs to an interactive bubble or stack badge element.
    /// </summary>
    /// <param name="hit">Hit-test result from the overlay visual tree.</param>
    /// <returns><see langword="true"/> when the hit should receive mouse input; otherwise <see langword="false"/>.</returns>
    private static bool IsInteractiveHit(object? hit)
    {
        if (hit is not DependencyObject current)
            return false;

        var el = current;
        while (el is not null)
        {
            if (el is BubbleControl)
                return true;
            if (el is FrameworkElement fe && fe.Name == nameof(StackBadge))
                return true;
            el = VisualTreeHelper.GetParent(el);
        }

        return false;
    }

    /// <summary>
    /// Raises <see cref="OnStackExpandRequested"/> when the user clicks the collapsed-stack badge.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
    private void StackBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OnStackExpandRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the user clicks the stack badge to expand a collapsed bubble stack.
    /// </summary>
    public event EventHandler? OnStackExpandRequested;
}
