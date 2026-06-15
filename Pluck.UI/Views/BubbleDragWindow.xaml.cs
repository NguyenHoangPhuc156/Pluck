using System.Windows;
using Pluck.Data.Models;
using Pluck.UI.Controls;
using Pluck.UI.Helpers;
using Pluck.UI.Models;

namespace Pluck.UI.Views;

public partial class BubbleDragWindow : Window
{
    public BubbleDragWindow()
    {
        InitializeComponent();
        WindowChromeHelper.HideFromAltTab(this);
    }

    public void SetBubbleContent(BubbleModel model, PluckSettings settings, double opacityPercent)
    {
        DragBubble.Bind(model, settings, opacityPercent);
        ApplyDragBubbleSize(model);
    }

    private void ApplyDragBubbleSize(BubbleModel model)
    {
        var width = model.CustomWidth > 0 ? model.CustomWidth : BubbleOverlayWindow.BubbleWidth;
        var height = model.CustomHeight > 0 ? model.CustomHeight : 0;
        DragBubble.ApplySize(width, height);
        DragBubble.UpdateLayout();

        Width = DragBubble.Width;
        if (height > 0)
            Height = DragBubble.Height;
        else
            Height = Math.Max(DragBubble.ActualHeight, DragBubble.MinHeight);

        SizeToContent = SizeToContent.Manual;
    }

    public void MoveToScreen(Point dipTopLeft)
    {
        Left = dipTopLeft.X;
        Top = dipTopLeft.Y;
    }

    public void FreezeSize()
    {
        UpdateLayout();
        Width = ActualWidth;
        Height = ActualHeight;
        SizeToContent = SizeToContent.Manual;
    }
}
