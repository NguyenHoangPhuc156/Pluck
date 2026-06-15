using System.Windows.Media;

namespace Pluck.UI.Services;

public sealed class BubbleAnimationController : IDisposable
{
    private readonly Dictionary<Guid, double> _baseY = new();
    private bool _running;
    private double _phase;

    public void Register(Guid bubbleId, double baseY)
    {
        _baseY[bubbleId] = baseY;
        EnsureRunning();
    }

    public void Unregister(Guid bubbleId) => _baseY.Remove(bubbleId);

    public double GetOffsetY(Guid bubbleId, bool enabled)
    {
        if (!enabled || !_running)
            return 0;
        return Math.Sin(_phase + bubbleId.GetHashCode() * 0.15) * 6.0;
    }

    private void EnsureRunning()
    {
        if (_running)
            return;
        _running = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs args)
            _phase = args.RenderingTime.TotalSeconds * 2.0;
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        _running = false;
    }
}
