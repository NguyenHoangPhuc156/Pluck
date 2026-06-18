using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Pluck.Data.Models;
using Pluck.UI.Helpers;
using Pluck.UI.Models;
using Pluck.UI.Views;

namespace Pluck.UI.Controls;

public partial class BubbleControl : System.Windows.Controls.UserControl
{
    public const double MinBubbleWidth = 160;
    public const double MinBubbleHeight = 56;
    public const double MaxBubbleWidth = 520;
    public const double MaxBubbleHeight = 420;
    private const double CardPadding = 10;
    private const double HeaderRowHeight = 24;
    private const double ContentTopGap = 4;
    private const double DragThreshold = 6;
    private const double RepositionMoveThreshold = 2;

    public event EventHandler<BubbleModel>? PasteRequested;
    public event EventHandler<BubbleModel>? PinRequested;
    public event EventHandler<BubbleModel>? CopyAgainRequested;
    public event EventHandler<BubbleModel>? DeleteRequested;
    public event EventHandler<PasteDragStartEventArgs>? PasteDragStarted;
    public event EventHandler<BubbleModel>? RepositionPrepare;
    public event EventHandler<BubbleModel>? RepositionCancelled;
    public event EventHandler<RepositionEventArgs>? Repositioning;
    public event EventHandler<BubbleModel>? UserRepositioned;
    public event EventHandler<BubbleResizeEventArgs>? Resizing;
    public event EventHandler<BubbleModel>? ResizeCompleted;

    private BubbleModel _model = null!;
    private PluckSettings _settings = new();
    private Point _pressStart;
    private Point _grabOffset;
    private Point _lastRepositionCanvas = new(double.NaN, double.NaN);
    private MouseButton _activeButton;
    private bool _isDragging;
    private bool _isRepositioning;
    private bool _isResizing;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private Point _resizeStartMouse;
    private bool _dragHandledByManager;
    private bool _bindingMatched;

    public BubbleModel Model => _model;

    public BubbleControl()
    {
        InitializeComponent();
    }

    public void Bind(BubbleModel model, PluckSettings settings, double opacity)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;
        _settings = settings ?? new PluckSettings();
        var item = model.Item ?? throw new InvalidOperationException("BubbleModel.Item is null.");

        Opacity = Math.Clamp(opacity, 10, 90) / 100.0;

        var sourceIcon = RequireElement(ref SourceIcon, "SourceIcon");
        var sourceName = RequireElement(ref SourceName, "SourceName");
        var timestamp = RequireElement(ref Timestamp, "Timestamp");
        var pinBadge = RequireElement(ref PinBadge, "PinBadge");
        var previewText = RequireElement(ref PreviewText, "PreviewText");
        var previewImage = RequireElement(ref PreviewImage, "PreviewImage");

        sourceIcon.Visibility = _settings.ShowSourceAppIcon ? Visibility.Visible : Visibility.Collapsed;
        sourceName.Visibility = _settings.ShowSourceAppName ? Visibility.Visible : Visibility.Collapsed;
        timestamp.Visibility = _settings.ShowCopyTimestamp ? Visibility.Visible : Visibility.Collapsed;
        pinBadge.Visibility = model.IsPinned ? Visibility.Visible : Visibility.Collapsed;

        sourceName.Text = item.SourceAppName ?? "";
        timestamp.Text = FormatTime(item.CopiedAt);

        if (_settings.ShowSourceAppIcon)
            sourceIcon.Source = IconHelper.FromPngBytes(item.SourceAppIconPng);

        previewText.Visibility = Visibility.Collapsed;
        previewImage.Visibility = Visibility.Collapsed;

        switch (_settings.ContentDisplay)
        {
            case BubbleContentDisplayMode.Disabled:
                previewText.Text = item.Type switch
                {
                    ClipboardItemType.Text => "T",
                    ClipboardItemType.Image => "🖼",
                    ClipboardItemType.Files => "📁",
                    ClipboardItemType.Unknown => "◆",
                    _ => "?"
                };
                previewText.Visibility = Visibility.Visible;
                break;
            case BubbleContentDisplayMode.KnownContentOnly when item.Type == ClipboardItemType.Unknown:
                previewText.Text = FormatUnknownPreview(item.Preview);
                previewText.Visibility = Visibility.Visible;
                break;
            default:
                if (item.Type == ClipboardItemType.Image && item.ImageThumbnailPng is not null)
                {
                    previewImage.Source = IconHelper.FromPngBytes(item.ImageThumbnailPng);
                    previewImage.Visibility = Visibility.Visible;
                }
                else if (item.Type == ClipboardItemType.Unknown)
                {
                    previewText.Text = FormatUnknownPreview(item.Preview);
                    previewText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66));
                    previewText.FontStyle = FontStyles.Italic;
                    previewText.Visibility = Visibility.Visible;
                }
                else
                {
                    previewText.Text = item.Preview ?? "";
                    previewText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));
                    previewText.FontStyle = FontStyles.Normal;
                    previewText.Visibility = Visibility.Visible;
                }
                break;
        }

        ApplySize(model.CustomWidth, model.CustomHeight);
    }

    public void ApplySize(double width, double height)
    {
        var w = Math.Clamp(width > 0 ? width : BubbleOverlayWindow.BubbleWidth, MinBubbleWidth, MaxBubbleWidth);
        Width = w;

        var innerWidth = Math.Max(40, w - CardPadding * 2);
        PreviewImage.MaxWidth = innerWidth;

        if (height > 0)
        {
            Height = Math.Clamp(height, MinBubbleHeight, MaxBubbleHeight);
            MinHeight = MinBubbleHeight;

            var contentMax = height - (CardPadding * 2 + HeaderRowHeight + ContentTopGap);
            contentMax = Math.Max(24, contentMax);
            PreviewText.MaxHeight = contentMax;
            PreviewImage.MaxHeight = contentMax;
        }
        else
        {
            Height = double.NaN;
            MinHeight = MinBubbleHeight;

            PreviewText.MaxHeight = 120;
            PreviewImage.MaxHeight = 100;
        }
    }

    public void SetMoveTransform(double x, double y)
    {
        MoveTransform.X = x;
        MoveTransform.Y = y;
    }

    public void ClearMoveTransform()
    {
        MoveTransform.X = 0;
        MoveTransform.Y = 0;
    }

    public Point GetCanvasPosition()
    {
        var left = Canvas.GetLeft(this);
        var top = Canvas.GetTop(this);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        return new Point(left + MoveTransform.X, top + MoveTransform.Y);
    }

    private static string FormatUnknownPreview(string? preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
            return "Other clip";
        return preview.StartsWith("Unsupported", StringComparison.OrdinalIgnoreCase)
            ? preview
            : $"Other · {preview}";
    }

    private T RequireElement<T>(ref T? field, string name) where T : class
    {
        field ??= FindName(name) as T;
        return field ?? throw new InvalidOperationException($"BubbleControl XAML element '{name}' was not initialized.");
    }

    public Point GetScreenCenter()
    {
        return PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
    }

    private void UserControl_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isResizing)
            (Resources["HoverIn"] as Storyboard)?.Begin();
    }

    private void UserControl_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isResizing)
            (Resources["HoverOut"] as Storyboard)?.Begin();
    }

    private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing)
            return;

        if (e.OriginalSource is DependencyObject source && IsFromResizeGrip(source))
            return;

        _activeButton = e.ChangedButton;
        _pressStart = e.GetPosition(this);
        _grabOffset = _pressStart;
        _isDragging = false;
        _isRepositioning = false;
        _dragHandledByManager = false;
        _bindingMatched = BubbleMouseBindingHelper.ModifiersMatch(
            BubbleMouseBindingHelper.GetBinding(_settings, _activeButton),
            Keyboard.Modifiers);
        _lastRepositionCanvas = new Point(double.NaN, double.NaN);

        CaptureMouse();
        e.Handled = true;
    }

    private void UserControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isResizing || !IsMouseCaptured)
            return;

        if (e.OriginalSource is DependencyObject source && IsFromResizeGrip(source))
            return;

        if (!_bindingMatched)
            return;

        var binding = BubbleMouseBindingHelper.GetBinding(_settings, _activeButton);
        var pos = e.GetPosition(this);

        if (_activeButton == MouseButton.Left && e.LeftButton != MouseButtonState.Pressed)
            return;
        if (_activeButton == MouseButton.Right && e.RightButton != MouseButtonState.Pressed)
            return;
        if (_activeButton == MouseButton.Middle && e.MiddleButton != MouseButtonState.Pressed)
            return;

        if (!_isDragging && (pos - _pressStart).Length <= DragThreshold)
            return;

        if (binding.DragAction == BubbleDragAction.PasteDrag)
        {
            _isDragging = true;
            _dragHandledByManager = true;
            PasteDragStarted?.Invoke(this, new PasteDragStartEventArgs(_model, _grabOffset, _activeButton));
            ReleaseMouseCapture();
            return;
        }

        if (binding.DragAction == BubbleDragAction.MoveDrag)
            HandleRepositionMove(e);
    }

    private void HandleRepositionMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (!_isRepositioning && (pos - _grabOffset).Length <= DragThreshold)
            return;

        if (!_isRepositioning)
        {
            _isRepositioning = true;
            RepositionPrepare?.Invoke(this, _model);
        }

        if (Parent is not Canvas canvas)
            return;

        var mouseOnCanvas = e.GetPosition(canvas);
        var canvasTopLeft = new Point(
            mouseOnCanvas.X - _grabOffset.X,
            mouseOnCanvas.Y - _grabOffset.Y);

        if (!double.IsNaN(_lastRepositionCanvas.X)
            && (canvasTopLeft - _lastRepositionCanvas).Length < RepositionMoveThreshold)
            return;

        _lastRepositionCanvas = canvasTopLeft;
        Repositioning?.Invoke(this, new RepositionEventArgs(_model, canvasTopLeft));
    }

    private void UserControl_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing)
            return;

        if (e.ChangedButton != _activeButton || !IsMouseCaptured)
            return;

        if (_bindingMatched && !_isDragging && !_dragHandledByManager && !_isRepositioning
            && IsPointerOverBubble(e))
            ExecuteClickAction(BubbleMouseBindingHelper.GetBinding(_settings, _activeButton).ClickAction);

        if (_isRepositioning)
            UserRepositioned?.Invoke(this, _model);
        else if (_activeButton == MouseButton.Right && _bindingMatched)
            RepositionCancelled?.Invoke(this, _model);

        ReleaseMouseCapture();
        ResetInteractionState();
        e.Handled = true;
    }

    private bool IsPointerOverBubble(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var w = ActualWidth > 1 ? ActualWidth : Width;
        var h = ActualHeight > 1 ? ActualHeight : (Height > 0 ? Height : MinBubbleHeight);
        return pos.X >= 0 && pos.Y >= 0 && pos.X <= w && pos.Y <= h;
    }

    private void ExecuteClickAction(BubbleClickAction action)
    {
        switch (action)
        {
            case BubbleClickAction.Paste:
                PasteRequested?.Invoke(this, _model);
                break;
            case BubbleClickAction.Delete:
                DeleteRequested?.Invoke(this, _model);
                break;
            case BubbleClickAction.ContextMenu:
                ShowContextMenu();
                break;
        }
    }

    private void ResizeGrip_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
            return;

        _isResizing = true;
        _resizeStartWidth = ActualWidth > 1 ? ActualWidth : Width;
        _resizeStartHeight = ActualHeight > 1 ? ActualHeight : (Height > 0 ? Height : MinBubbleHeight);
        _resizeStartMouse = e.GetPosition(this);
        ResizeGrip.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeGrip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isResizing || !ResizeGrip.IsMouseCaptured)
            return;

        var pos = e.GetPosition(this);
        var delta = pos - _resizeStartMouse;

        // Bottom-right grip: top-left anchor fixed; size grows right and down.
        var newWidth = Math.Clamp(_resizeStartWidth + delta.X, MinBubbleWidth, MaxBubbleWidth);
        var newHeight = Math.Clamp(_resizeStartHeight + delta.Y, MinBubbleHeight, MaxBubbleHeight);

        ApplySize(newWidth, newHeight);
        Resizing?.Invoke(this, new BubbleResizeEventArgs(_model, newWidth, newHeight));
        e.Handled = true;
    }

    private void ResizeGrip_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isResizing)
            return;

        _isResizing = false;
        if (ResizeGrip.IsMouseCaptured)
            ResizeGrip.ReleaseMouseCapture();

        ResizeCompleted?.Invoke(this, _model);
        e.Handled = true;
    }

    private void ResetInteractionState()
    {
        _isDragging = false;
        _isRepositioning = false;
        _dragHandledByManager = false;
        _bindingMatched = false;
        _lastRepositionCanvas = new Point(double.NaN, double.NaN);
    }

    private bool IsFromResizeGrip(DependencyObject source)
    {
        var el = source;
        while (el is not null)
        {
            if (el == ResizeGrip)
                return true;
            el = System.Windows.Media.VisualTreeHelper.GetParent(el);
        }

        return false;
    }

    private void ShowContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Paste", () => PasteRequested?.Invoke(this, _model)));
        menu.Items.Add(CreateMenuItem(_model.IsPinned ? "Unpin" : "Pin", () => PinRequested?.Invoke(this, _model)));
        menu.Items.Add(CreateMenuItem("Copy again", () => CopyAgainRequested?.Invoke(this, _model)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Delete", () => DeleteRequested?.Invoke(this, _model)));
        menu.IsOpen = true;
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static string FormatTime(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        return local.Date == DateTime.Today ? local.ToString("HH:mm") : local.ToString("MM/dd HH:mm");
    }
}

public sealed class PasteDragStartEventArgs(BubbleModel model, Point grabOffsetInBubble, MouseButton button) : EventArgs
{
    public BubbleModel Model { get; } = model;
    public Point GrabOffsetInBubble { get; } = grabOffsetInBubble;
    public MouseButton Button { get; } = button;
}

public sealed class RepositionEventArgs(BubbleModel model, Point canvasTopLeft) : EventArgs
{
    public BubbleModel Model { get; } = model;
    public Point CanvasTopLeft { get; } = canvasTopLeft;
}

public sealed class BubbleResizeEventArgs(BubbleModel model, double width, double height) : EventArgs
{
    public BubbleModel Model { get; } = model;
    public double Width { get; } = width;
    public double Height { get; } = height;
}
