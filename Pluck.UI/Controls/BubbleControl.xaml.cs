using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Pluck.Data.Models;
using Pluck.UI.Helpers;
using Pluck.UI.Models;

namespace Pluck.UI.Controls;

public partial class BubbleControl : System.Windows.Controls.UserControl
{
    public event EventHandler<BubbleModel>? PasteRequested;
    public event EventHandler<BubbleModel>? PinRequested;
    public event EventHandler<BubbleModel>? CopyAgainRequested;
    public event EventHandler<BubbleModel>? DeleteRequested;
    public event EventHandler<PasteDragStartEventArgs>? PasteDragStarted;
    public event EventHandler<BubbleModel>? UserRepositioned;

    private BubbleModel _model = null!;
    private Point _dragStart;
    private bool _isDragging;
    private bool _ctrlHeld;
    private bool _dragHandledByManager;

    public BubbleModel Model => _model;

    public BubbleControl()
    {
        InitializeComponent();
    }

    public void Bind(BubbleModel model, PluckSettings settings, double opacity)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;
        settings ??= new PluckSettings();
        var item = model.Item ?? throw new InvalidOperationException("BubbleModel.Item is null.");

        Opacity = Math.Clamp(opacity, 10, 90) / 100.0;

        var sourceIcon = RequireElement(ref SourceIcon, "SourceIcon");
        var sourceName = RequireElement(ref SourceName, "SourceName");
        var timestamp = RequireElement(ref Timestamp, "Timestamp");
        var pinBadge = RequireElement(ref PinBadge, "PinBadge");
        var previewText = RequireElement(ref PreviewText, "PreviewText");
        var previewImage = RequireElement(ref PreviewImage, "PreviewImage");

        sourceIcon.Visibility = settings.ShowSourceAppIcon ? Visibility.Visible : Visibility.Collapsed;
        sourceName.Visibility = settings.ShowSourceAppName ? Visibility.Visible : Visibility.Collapsed;
        timestamp.Visibility = settings.ShowCopyTimestamp ? Visibility.Visible : Visibility.Collapsed;
        pinBadge.Visibility = model.IsPinned ? Visibility.Visible : Visibility.Collapsed;

        sourceName.Text = item.SourceAppName ?? "";
        timestamp.Text = FormatTime(item.CopiedAt);

        if (settings.ShowSourceAppIcon)
            sourceIcon.Source = IconHelper.FromPngBytes(item.SourceAppIconPng);

        previewText.Visibility = Visibility.Collapsed;
        previewImage.Visibility = Visibility.Collapsed;

        switch (settings.ContentDisplay)
        {
            case BubbleContentDisplayMode.Disabled:
                previewText.Text = item.Type switch
                {
                    ClipboardItemType.Text => "T",
                    ClipboardItemType.Image => "🖼",
                    ClipboardItemType.Files => "📁",
                    _ => "?"
                };
                previewText.Visibility = Visibility.Visible;
                break;
            case BubbleContentDisplayMode.KnownContentOnly when item.Type == ClipboardItemType.Unknown:
                previewText.Text = "?";
                previewText.Visibility = Visibility.Visible;
                break;
            default:
                if (item.Type == ClipboardItemType.Image && item.ImageThumbnailPng is not null)
                {
                    previewImage.Source = IconHelper.FromPngBytes(item.ImageThumbnailPng);
                    previewImage.Visibility = Visibility.Visible;
                }
                else
                {
                    previewText.Text = item.Preview ?? "";
                    previewText.Visibility = Visibility.Visible;
                }
                break;
        }
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
        (Resources["HoverIn"] as Storyboard)?.Begin();
    }

    private void UserControl_MouseLeave(object sender, MouseEventArgs e)
    {
        (Resources["HoverOut"] as Storyboard)?.Begin();
    }

    private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _ctrlHeld = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        _isDragging = false;
        _dragHandledByManager = false;
        CaptureMouse();
        e.Handled = true;
    }

    private void UserControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(this);
        if (!_isDragging && (pos - _dragStart).Length > 6)
        {
            _isDragging = true;

            if (_ctrlHeld)
                return;

            var parent = Parent as Canvas;
            if (parent is null)
                return;

            _dragHandledByManager = true;
            PasteDragStarted?.Invoke(this, new PasteDragStartEventArgs(_model, _dragStart));
            ReleaseMouseCapture();
            return;
        }

        if (_isDragging && _ctrlHeld)
        {
            var parent = Parent as Canvas;
            if (parent is null)
                return;

            var canvasPos = e.GetPosition(parent);
            _model.HasUserPosition = true;
            _model.CustomX = Math.Max(0, canvasPos.X - ActualWidth / 2);
            _model.CustomY = Math.Max(0, canvasPos.Y - ActualHeight / 2);
            Canvas.SetLeft(this, _model.CustomX);
            Canvas.SetTop(this, _model.CustomY);
            _model.LayoutY = _model.CustomY;
        }
    }

    private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseCaptured)
        {
            if (_isDragging && _ctrlHeld)
                UserRepositioned?.Invoke(this, _model);
            else if (!_isDragging && !_dragHandledByManager)
                PasteRequested?.Invoke(this, _model);
            ReleaseMouseCapture();
        }
        _isDragging = false;
        e.Handled = true;
    }

    private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            DeleteRequested?.Invoke(this, _model);
            e.Handled = true;
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Paste", () => PasteRequested?.Invoke(this, _model)));
        menu.Items.Add(CreateMenuItem(_model.IsPinned ? "Unpin" : "Pin", () => PinRequested?.Invoke(this, _model)));
        menu.Items.Add(CreateMenuItem("Copy again", () => CopyAgainRequested?.Invoke(this, _model)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Delete", () => DeleteRequested?.Invoke(this, _model)));
        menu.IsOpen = true;
        e.Handled = true;
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

public sealed class PasteDragStartEventArgs(BubbleModel model, Point grabOffsetInBubble) : EventArgs
{
    public BubbleModel Model { get; } = model;
    /// <summary>Where the user pressed inside the bubble (DIP, top-left origin).</summary>
    public Point GrabOffsetInBubble { get; } = grabOffsetInBubble;
}
