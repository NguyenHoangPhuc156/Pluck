using System.Windows;
using Pluck.Data.Models;
using Pluck.UI.Controls;
using Pluck.UI.Models;

namespace Pluck.UI.Views;

public partial class BubbleDragWindow : Window
{
    public BubbleDragWindow()
    {
        InitializeComponent();
    }

    public void SetBubbleContent(BubbleModel model, PluckSettings settings, double opacityPercent)
    {
        DragBubble.Bind(model, settings, opacityPercent);
    }

    /// <param name="dipTopLeft">Screen position in WPF device-independent units.</param>
    public void MoveToScreen(Point dipTopLeft)
    {
        Left = dipTopLeft.X;
        Top = dipTopLeft.Y;
    }
}
