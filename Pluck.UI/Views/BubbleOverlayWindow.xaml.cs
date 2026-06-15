using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Pluck.Core.Native;
using Pluck.UI.Controls;
using Pluck.UI.Helpers;
using Pluck.UI.Models;

namespace Pluck.UI.Views;

public partial class BubbleOverlayWindow : Window
{
    public const double BubbleWidth = 220;
    public const double BubbleMargin = 10;
    public const double TopPadding = 12;
    public const double RightPadding = 0;

    private HwndSource? _hwndSource;

    public BubbleOverlayWindow()
    {
        InitializeComponent();
        WindowChromeHelper.HideFromAltTab(this);
        Loaded += (_, _) => EnsureVirtualScreenMode();
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>Full virtual desktop with click-through on empty areas (always on).</summary>
    public void EnsureVirtualScreenMode()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public Point ScreenToCanvas(Point screenPoint) => PointFromScreen(screenPoint);

    public Point CanvasToScreen(Point canvasPoint) => PointToScreen(canvasPoint);

    public void LayoutBubbleStack(IReadOnlyList<(BubbleControl Control, BubbleModel Model)> items)
    {
        var stackOrigin = MonitorHelper.GetPrimaryBubbleStackScreenDip(this, BubbleWidth, RightPadding, TopPadding);
        var canvasOrigin = ScreenToCanvas(stackOrigin);

        var left = canvasOrigin.X;
        var y = canvasOrigin.Y;

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

            var stackLeft = ScreenToCanvas(MonitorHelper.GetPrimaryBubbleStackScreenDip(this, bubbleWidth, RightPadding, TopPadding)).X;
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
            left = stackLeft;
            y += height + BubbleMargin;
        }
    }

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

    private void PositionStackBadgeOnPrimaryStack()
    {
        var anchor = MonitorHelper.GetPrimaryBubbleStackScreenDip(this, 48, RightPadding + 4, TopPadding - 4);
        var badgeAnchor = ScreenToCanvas(anchor);
        Canvas.SetLeft(StackBadge, badgeAnchor.X);
        Canvas.SetTop(StackBadge, badgeAnchor.Y);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
    }

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

    private void StackBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OnStackExpandRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OnStackExpandRequested;
}
