using System.Windows.Media;

namespace Pluck.UI.Services;

/// <summary>
/// Drives subtle vertical sine-wave motion for floating clipboard bubbles.
/// </summary>
public sealed class BubbleAnimationController : IDisposable
{
    private readonly Dictionary<Guid, double> _baseY = new();
    private bool _running;
    private double _phase;

    /// <summary>
    /// Registers a bubble for floating animation at the given base Y coordinate.
    /// </summary>
    /// <param name="bubbleId">Unique bubble identifier.</param>
    /// <param name="baseY">Canvas Y coordinate used as the animation baseline.</param>
    public void Register(Guid bubbleId, double baseY)
    {
        _baseY[bubbleId] = baseY;
        EnsureRunning();
    }

    /// <summary>
    /// Removes a bubble from the animation registry.
    /// </summary>
    /// <param name="bubbleId">Unique bubble identifier.</param>
    public void Unregister(Guid bubbleId) => _baseY.Remove(bubbleId);

    /// <summary>
    /// Returns the current vertical offset for a bubble's floating animation.
    /// </summary>
    /// <param name="bubbleId">Unique bubble identifier.</param>
    /// <param name="enabled">Whether floating animation is enabled in settings.</param>
    /// <returns>Vertical offset in device-independent pixels, or zero when disabled.</returns>
    public double GetOffsetY(Guid bubbleId, bool enabled)
    {
        if (!enabled || !_running)
            return 0;
        return Math.Sin(_phase + bubbleId.GetHashCode() * 0.15) * 6.0;
    }

    /// <summary>
    /// Subscribes to composition rendering when animation is not already active.
    /// </summary>
    private void EnsureRunning()
    {
        if (_running)
            return;
        _running = true;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>
    /// Advances the shared animation phase on each render frame.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Rendering event arguments containing elapsed time.</param>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs args)
            _phase = args.RenderingTime.TotalSeconds * 2.0;
    }

    /// <summary>
    /// Unsubscribes from rendering and stops animation updates.
    /// </summary>
    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        _running = false;
    }
}
