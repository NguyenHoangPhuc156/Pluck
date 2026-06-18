using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Pluck.Data.Models;
using Pluck.UI.Helpers;
using Pluck.UI.Models;
using Pluck.UI.Views;

namespace Pluck.UI.Controls;

/// <summary>
/// Interactive floating clipboard bubble with configurable click, drag, resize, and context menu behavior.
/// </summary>
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

    /// <summary>
    /// Gets the bubble model currently bound to this control.
    /// </summary>
    public BubbleModel Model => _model;

    /// <summary>
    /// Initializes bubble control XAML and resources.
    /// </summary>
    public BubbleControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Binds clipboard content and appearance settings to the bubble UI elements.
    /// </summary>
    /// <param name="model">Bubble model containing item data and layout state.</param>
    /// <param name="settings">Application settings controlling visible fields and content mode.</param>
    /// <param name="opacity">Bubble opacity percentage from settings.</param>
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

    /// <summary>
    /// Applies width and height constraints to the bubble and its preview elements.
    /// </summary>
    /// <param name="width">Desired bubble width in DIP; non-positive values use the default width.</param>
    /// <param name="height">Desired bubble height in DIP; zero enables automatic height.</param>
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

    /// <summary>
    /// Sets a translate transform used during live reposition dragging.
    /// </summary>
    /// <param name="x">Horizontal offset in DIP.</param>
    /// <param name="y">Vertical offset in DIP.</param>
    public void SetMoveTransform(double x, double y)
    {
        MoveTransform.X = x;
        MoveTransform.Y = y;
    }

    /// <summary>
    /// Clears the reposition translate transform.
    /// </summary>
    public void ClearMoveTransform()
    {
        MoveTransform.X = 0;
        MoveTransform.Y = 0;
    }

    /// <summary>
    /// Returns the bubble's effective canvas position including any active move transform.
    /// </summary>
    /// <returns>Canvas coordinates of the bubble's top-left corner.</returns>
    public Point GetCanvasPosition()
    {
        var left = Canvas.GetLeft(this);
        var top = Canvas.GetTop(this);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        return new Point(left + MoveTransform.X, top + MoveTransform.Y);
    }

    /// <summary>
    /// Formats preview text for unknown clipboard content types.
    /// </summary>
    /// <param name="preview">Raw preview string from the clipboard item.</param>
    /// <returns>User-facing preview text for unknown content.</returns>
    private static string FormatUnknownPreview(string? preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
            return "Other clip";
        return preview.StartsWith("Unsupported", StringComparison.OrdinalIgnoreCase)
            ? preview
            : $"Other · {preview}";
    }

    /// <summary>
    /// Resolves a named XAML element, throwing when it was not initialized.
    /// </summary>
    /// <typeparam name="T">Expected element type.</typeparam>
    /// <param name="field">Cached field reference updated on first lookup.</param>
    /// <param name="name">XAML element name.</param>
    /// <returns>The resolved element instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the named element is missing.</exception>
    private T RequireElement<T>(ref T? field, string name) where T : class
    {
        field ??= FindName(name) as T;
        return field ?? throw new InvalidOperationException($"BubbleControl XAML element '{name}' was not initialized.");
    }

    /// <summary>
    /// Returns the screen-space center point of the bubble for visual effects.
    /// </summary>
    /// <returns>Screen coordinates of the bubble center in DIP.</returns>
    public Point GetScreenCenter()
    {
        return PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
    }

    /// <summary>
    /// Plays the hover-in animation when the pointer enters the bubble.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse event data.</param>
    private void UserControl_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isResizing)
            (Resources["HoverIn"] as Storyboard)?.Begin();
    }

    /// <summary>
    /// Plays the hover-out animation when the pointer leaves the bubble.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse event data.</param>
    private void UserControl_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isResizing)
            (Resources["HoverOut"] as Storyboard)?.Begin();
    }

    /// <summary>
    /// Captures the mouse and begins tracking a potential click or drag gesture.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
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

    /// <summary>
    /// Tracks mouse movement to start paste-drag or reposition gestures after the drag threshold.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse event data.</param>
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

    /// <summary>
    /// Updates live reposition coordinates while the user drags the bubble on the canvas.
    /// </summary>
    /// <param name="e">Mouse event data relative to the bubble control.</param>
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

    /// <summary>
    /// Completes click, drag, or reposition gestures on mouse button release.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
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

    /// <summary>
    /// Determines whether the mouse event occurred inside the bubble bounds.
    /// </summary>
    /// <param name="e">Mouse event data relative to the bubble control.</param>
    /// <returns><see langword="true"/> when the pointer is over the bubble client area.</returns>
    private bool IsPointerOverBubble(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var w = ActualWidth > 1 ? ActualWidth : Width;
        var h = ActualHeight > 1 ? ActualHeight : (Height > 0 ? Height : MinBubbleHeight);
        return pos.X >= 0 && pos.Y >= 0 && pos.X <= w && pos.Y <= h;
    }

    /// <summary>
    /// Invokes the configured click action for the active mouse binding.
    /// </summary>
    /// <param name="action">Click action to execute.</param>
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

    /// <summary>
    /// Begins a resize gesture from the bottom-right resize grip.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
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

    /// <summary>
    /// Updates bubble dimensions while the resize grip is dragged.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse event data.</param>
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

    /// <summary>
    /// Ends the resize gesture and notifies listeners that resizing completed.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Mouse button event data.</param>
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

    /// <summary>
    /// Resets transient mouse interaction state after a gesture completes.
    /// </summary>
    private void ResetInteractionState()
    {
        _isDragging = false;
        _isRepositioning = false;
        _dragHandledByManager = false;
        _bindingMatched = false;
        _lastRepositionCanvas = new Point(double.NaN, double.NaN);
    }

    /// <summary>
    /// Determines whether a visual originates from the resize grip element.
    /// </summary>
    /// <param name="source">Original hit-test source from a mouse event.</param>
    /// <returns><see langword="true"/> when the source is the resize grip or a descendant.</returns>
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

    /// <summary>
    /// Displays the bubble context menu with paste, pin, copy, and delete actions.
    /// </summary>
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

    /// <summary>
    /// Creates a context menu item that executes an action when clicked.
    /// </summary>
    /// <param name="header">Menu item header text.</param>
    /// <param name="action">Action invoked when the item is chosen.</param>
    /// <returns>The configured menu item.</returns>
    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>
    /// Formats a copy timestamp for display in the bubble header.
    /// </summary>
    /// <param name="at">Timestamp to format.</param>
    /// <returns>A localized time or date-time display string.</returns>
    private static string FormatTime(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        return local.Date == DateTime.Today ? local.ToString("HH:mm") : local.ToString("MM/dd HH:mm");
    }
}

/// <summary>
/// Event data raised when a paste-drag gesture starts from a bubble.
/// </summary>
/// <param name="model">Bubble model being dragged.</param>
/// <param name="grabOffsetInBubble">Cursor offset within the bubble at drag start.</param>
/// <param name="button">Mouse button that initiated the drag.</param>
public sealed class PasteDragStartEventArgs(BubbleModel model, Point grabOffsetInBubble, MouseButton button) : EventArgs
{
    /// <summary>
    /// Gets the bubble model being dragged.
    /// </summary>
    public BubbleModel Model { get; } = model;

    /// <summary>
    /// Gets the cursor offset within the bubble when the drag started.
    /// </summary>
    public Point GrabOffsetInBubble { get; } = grabOffsetInBubble;

    /// <summary>
    /// Gets the mouse button that initiated the paste drag.
    /// </summary>
    public MouseButton Button { get; } = button;
}

/// <summary>
/// Event data raised while a bubble is being repositioned on the overlay canvas.
/// </summary>
/// <param name="model">Bubble model being repositioned.</param>
/// <param name="canvasTopLeft">Target top-left position in canvas coordinates.</param>
public sealed class RepositionEventArgs(BubbleModel model, Point canvasTopLeft) : EventArgs
{
    /// <summary>
    /// Gets the bubble model being repositioned.
    /// </summary>
    public BubbleModel Model { get; } = model;

    /// <summary>
    /// Gets the target top-left canvas position for the bubble.
    /// </summary>
    public Point CanvasTopLeft { get; } = canvasTopLeft;
}

/// <summary>
/// Event data raised while a bubble is being resized.
/// </summary>
/// <param name="model">Bubble model being resized.</param>
/// <param name="width">Current width in device-independent pixels.</param>
/// <param name="height">Current height in device-independent pixels.</param>
public sealed class BubbleResizeEventArgs(BubbleModel model, double width, double height) : EventArgs
{
    /// <summary>
    /// Gets the bubble model being resized.
    /// </summary>
    public BubbleModel Model { get; } = model;

    /// <summary>
    /// Gets the current bubble width during the resize gesture.
    /// </summary>
    public double Width { get; } = width;

    /// <summary>
    /// Gets the current bubble height during the resize gesture.
    /// </summary>
    public double Height { get; } = height;
}
