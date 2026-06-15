using System.Windows;
using System.Windows.Controls;
using Pluck.UI.Controls;
using Pluck.UI.Models;

namespace Pluck.UI.Views;

public partial class BubbleOverlayWindow : Window
{
    public const double BubbleWidth = 220;
    public const double BubbleMargin = 10;
    public const double TopPadding = 16;
    public const double RightPadding = 8;
    public const double StripExtraWidth = 24;

    private Rect? _savedBounds;

    public BubbleOverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => FitToStripMode();
    }

    /// <summary>Overlay occupies only the right edge strip so clicks elsewhere reach other apps.</summary>
    public void FitToStripMode()
    {
        var area = SystemParameters.WorkArea;
        Width = BubbleWidth + RightPadding * 2 + StripExtraWidth;
        Height = area.Height;
        Left = area.Left + area.Width - Width;
        Top = area.Top;
        _savedBounds = null;
    }

    public void EnterDragMode()
    {
        if (_savedBounds is null)
            _savedBounds = new Rect(Left, Top, Width, Height);

        var area = SystemParameters.WorkArea;
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;
    }

    public void ExitDragMode()
    {
        if (_savedBounds is { } saved)
        {
            Left = saved.X;
            Top = saved.Y;
            Width = saved.Width;
            Height = saved.Height;
            _savedBounds = null;
        }
        else
            FitToStripMode();
    }

    public Point ScreenToCanvas(Point screenPoint) => PointFromScreen(screenPoint);

    public void LayoutBubbleStack(IReadOnlyList<(BubbleControl Control, BubbleModel Model)> items)
    {
        var left = Width - BubbleWidth - RightPadding;
        var y = TopPadding;

        foreach (var (control, model) in items)
        {
            if (model.HasUserPosition)
            {
                Canvas.SetLeft(control, model.CustomX);
                Canvas.SetTop(control, model.CustomY);
                model.LayoutY = model.CustomY;
                continue;
            }

            control.Measure(new System.Windows.Size(BubbleWidth, double.PositiveInfinity));
            var height = Math.Max(control.DesiredSize.Height, control.ActualHeight);
            if (height < 1)
                height = 64;

            Canvas.SetLeft(control, left);
            Canvas.SetTop(control, y);
            model.LayoutY = y;
            y += height + BubbleMargin;
        }
    }

    public void UpdateStackBadge(int count, bool collapsed)
    {
        if (!collapsed || count < 5)
        {
            StackBadge.Visibility = Visibility.Collapsed;
            return;
        }
        StackCountText.Text = $"{count}";
        StackBadge.Visibility = Visibility.Visible;
    }

    private void StackBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OnStackExpandRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OnStackExpandRequested;
}
