using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pluck.Core.Native;
using Pluck.Core.Services;
using Pluck.Data.Models;
using Pluck.Data.Services;
using Pluck.UI.Controls;
using Pluck.UI.Models;
using Pluck.UI.Views;

namespace Pluck.UI.Services;

public sealed class BubbleManager : IDisposable
{
    private readonly BubbleOverlayWindow _overlay;
    private readonly BubbleAnimationController _animation;
    private readonly PopEffectService _popEffect;
    private readonly ClipboardRepository _repository;
    private readonly BubblePasteDragSession _pasteDrag;
    private readonly Dictionary<Guid, BubbleControl> _controls = new();
    private readonly List<BubbleModel> _bubbles = new();

    private PluckSettings _settings = new();
    private bool _stackCollapsed = true;
    private DispatcherTimer? _layoutTimer;

    public BubbleManager(ClipboardRepository repository)
    {
        _repository = repository;
        _overlay = new BubbleOverlayWindow();
        _animation = new BubbleAnimationController();
        _popEffect = new PopEffectService();
        _pasteDrag = new BubblePasteDragSession(_overlay, _settings, OnPasteDragCompleted);

        PluckWindowGuard.Instance.Register(_overlay);

        _overlay.OnStackExpandRequested += (_, _) => ExpandStack();
        _overlay.Show();
        _overlay.FitToStripMode();

        SystemParameters.StaticPropertyChanged += (_, _) =>
        {
            if (!_pasteDrag.IsActive)
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _overlay.FitToStripMode());
        };

        StartLayoutLoop();
    }

    public void ApplySettings(PluckSettings settings)
    {
        _settings = settings ?? new PluckSettings();
        _pasteDrag.UpdateSettings(_settings);
        RefreshAllBindings();
        if (!_pasteDrag.IsActive)
            Relayout();
    }

    public void AddBubble(ClipboardItem item)
    {
        TrimForMaxBubbles();

        var model = new BubbleModel { Item = item, IsPinned = item.IsPinned };
        _bubbles.Insert(0, model);

        var control = CreateBubbleControl(model);
        _controls[model.BubbleId] = control;
        _overlay.BubbleCanvas.Children.Add(control);
        control.Bind(model, _settings, _settings.OpacityPercent);

        if (_bubbles.Count >= 5)
            _stackCollapsed = true;

        if (!_pasteDrag.IsActive)
            Relayout();
        ScheduleAutoDismiss(model);
    }

    public void RemoveBubble(Guid bubbleId, bool withPopEffect = false)
    {
        if (!_controls.TryGetValue(bubbleId, out var control))
            return;

        if (withPopEffect && _settings.PopEffectOnPaste)
            _popEffect.Play(_overlay.EffectCanvas, control.GetScreenCenter());

        _overlay.BubbleCanvas.Children.Remove(control);
        _controls.Remove(bubbleId);
        _bubbles.RemoveAll(b => b.BubbleId == bubbleId);
        _animation.Unregister(bubbleId);

        if (!_pasteDrag.IsActive)
            Relayout();
    }

    public void SetPinned(Guid bubbleId, bool pinned)
    {
        var model = _bubbles.FirstOrDefault(b => b.BubbleId == bubbleId);
        if (model is null)
            return;
        model.IsPinned = pinned;
        model.Item.IsPinned = pinned;
        _repository.SetPinned(model.Item.Id, pinned);
        _controls[bubbleId].Bind(model, _settings, _settings.OpacityPercent);
    }

    private BubbleControl CreateBubbleControl(BubbleModel model)
    {
        var control = new BubbleControl();
        control.PasteRequested += (_, m) => PasteBubble(m);
        control.PinRequested += (_, m) => SetPinned(m.BubbleId, !m.IsPinned);
        control.CopyAgainRequested += (_, m) =>
        {
            PasteService.Instance.SuppressCaptureFor(2000);
            PasteService.Instance.CopyToClipboard(m.Item);
        };
        control.DeleteRequested += (_, m) =>
        {
            _repository.Delete(m.Item.Id);
            RemoveBubble(m.BubbleId);
        };
        control.PasteDragStarted += (_, args) =>
        {
            if (_controls.TryGetValue(args.Model.BubbleId, out var c))
                _pasteDrag.Start(c, args.Model, args.GrabOffsetInBubble);
        };
        control.UserRepositioned += (_, _) =>
        {
            if (!_pasteDrag.IsActive)
                Relayout();
        };
        return control;
    }

    private void OnPasteDragCompleted(BubbleModel model)
    {
        if (_controls.ContainsKey(model.BubbleId))
            Relayout();
    }

    private void PasteBubble(BubbleModel model)
    {
        var item = model.Item;
        var sourceHwnd = item.SourceWindowHandle;

        // Click focuses Pluck — paste back to the app where the copy came from when possible.
        if (sourceHwnd != IntPtr.Zero && !WindowTargetService.IsPluckWindow(sourceHwnd))
        {
            NativeMethods.GetCursorPos(out var pt);
            PasteService.Instance.PasteToWindow(sourceHwnd, item, pt.X, pt.Y);
        }
        else
        {
            PluckWindowGuard.Instance.RunHidden(() =>
            {
                if (NativeMethods.GetCursorPos(out var pt))
                    PasteService.Instance.PasteToPoint(pt.X, pt.Y, item);
            });
        }

        if (_controls.ContainsKey(model.BubbleId))
            Relayout();
    }

    private void TrimForMaxBubbles()
    {
        while (_bubbles.Count >= _settings.MaxBubbles)
        {
            var oldest = _bubbles.LastOrDefault(b => !b.IsPinned);
            if (oldest is null)
                break;
            RemoveBubble(oldest.BubbleId);
        }
    }

    private void ExpandStack()
    {
        _stackCollapsed = false;
        Relayout();
    }

    private void Relayout()
    {
        if (_pasteDrag.IsActive)
            return;

        var visibleModels = _stackCollapsed && _bubbles.Count >= 5
            ? _bubbles.Take(1).ToList()
            : _bubbles.ToList();

        foreach (var control in _controls.Values)
            control.Visibility = Visibility.Collapsed;

        var layoutItems = new List<(BubbleControl Control, BubbleModel Model)>();
        foreach (var model in visibleModels)
        {
            if (!_controls.TryGetValue(model.BubbleId, out var control))
                continue;
            control.Visibility = Visibility.Visible;
            control.Bind(model, _settings, _settings.OpacityPercent);
            layoutItems.Add((control, model));
        }

        _overlay.UpdateLayout();
        _overlay.LayoutBubbleStack(layoutItems);

        foreach (var (control, model) in layoutItems)
        {
            _animation.Register(model.BubbleId, model.LayoutY);
            if (_settings.FloatingAnimationEnabled && !model.HasUserPosition)
            {
                var offset = _animation.GetOffsetY(model.BubbleId, true);
                Canvas.SetTop(control, model.LayoutY + offset);
            }
        }

        _overlay.UpdateStackBadge(_bubbles.Count, _stackCollapsed);
    }

    private void StartLayoutLoop()
    {
        _layoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _layoutTimer.Tick += (_, _) =>
        {
            if (!_settings.FloatingAnimationEnabled || _pasteDrag.IsActive)
                return;
            foreach (var model in _bubbles)
            {
                if (!_controls.TryGetValue(model.BubbleId, out var control) || control.Visibility != Visibility.Visible)
                    continue;
                if (model.HasUserPosition)
                    continue;
                var offset = _animation.GetOffsetY(model.BubbleId, true);
                Canvas.SetTop(control, model.LayoutY + offset);
            }
        };
        _layoutTimer.Start();
    }

    private void ScheduleAutoDismiss(BubbleModel model)
    {
        if (!_settings.DisplayDurationEnabled)
            return;

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.DisplayDurationSeconds)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!model.IsPinned && !_pasteDrag.IsActive)
                RemoveBubble(model.BubbleId);
        };
        timer.Start();
    }

    private void RefreshAllBindings()
    {
        foreach (var model in _bubbles)
        {
            if (_controls.TryGetValue(model.BubbleId, out var c))
                c.Bind(model, _settings, _settings.OpacityPercent);
        }
    }

    public void RemoveByItemId(long itemId)
    {
        foreach (var model in _bubbles.Where(b => b.Item.Id == itemId).ToList())
            RemoveBubble(model.BubbleId);
    }

    public void SetPinnedByItemId(long itemId, bool pinned)
    {
        foreach (var model in _bubbles.Where(b => b.Item.Id == itemId))
            SetPinned(model.BubbleId, pinned);
    }

    public void Dispose()
    {
        _layoutTimer?.Stop();
        _pasteDrag.Cancel();
        _animation.Dispose();
        _overlay.Close();
    }
}
