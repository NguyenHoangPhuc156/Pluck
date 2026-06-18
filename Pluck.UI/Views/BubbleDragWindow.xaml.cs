using System.Windows;
using Pluck.Data.Models;
using Pluck.UI.Controls;
using Pluck.UI.Helpers;
using Pluck.UI.Models;

namespace Pluck.UI.Views;

/// <summary>
/// Lightweight top-level window that displays a bubble clone during paste-drag operations.
/// </summary>
public partial class BubbleDragWindow : Window
{
    /// <summary>
    /// Initializes the drag window and hides it from the Alt+Tab switcher.
    /// </summary>
    public BubbleDragWindow()
    {
        InitializeComponent();
        WindowChromeHelper.HideFromAltTab(this);
    }

    /// <summary>
    /// Binds bubble content and applies size derived from the model to the drag window.
    /// </summary>
    /// <param name="model">Bubble model whose content and dimensions are shown.</param>
    /// <param name="settings">Application settings controlling bubble appearance.</param>
    /// <param name="opacityPercent">Bubble opacity percentage from settings.</param>
    public void SetBubbleContent(BubbleModel model, PluckSettings settings, double opacityPercent)
    {
        DragBubble.Bind(model, settings, opacityPercent);
        ApplyDragBubbleSize(model);
    }

    /// <summary>
    /// Sizes the host window to match the embedded bubble control.
    /// </summary>
    /// <param name="model">Bubble model supplying custom width and height.</param>
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

    /// <summary>
    /// Moves the window so its top-left corner is at the given screen DIP coordinates.
    /// </summary>
    /// <param name="dipTopLeft">Screen position in device-independent pixels.</param>
    public void MoveToScreen(Point dipTopLeft)
    {
        Left = dipTopLeft.X;
        Top = dipTopLeft.Y;
    }

    /// <summary>
    /// Locks the window size after layout so it does not resize during drag tracking.
    /// </summary>
    public void FreezeSize()
    {
        UpdateLayout();
        Width = ActualWidth;
        Height = ActualHeight;
        SizeToContent = SizeToContent.Manual;
    }
}
