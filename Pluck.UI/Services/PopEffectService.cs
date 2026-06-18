using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Pluck.UI.Services;

/// <summary>
/// Plays a short particle burst effect when a bubble is removed after paste.
/// </summary>
public sealed class PopEffectService
{
    /// <summary>
    /// Spawns animated ellipses radiating from a screen-space center point on the given canvas.
    /// </summary>
    /// <param name="canvas">Canvas that hosts the transient effect elements.</param>
    /// <param name="center">Center point in canvas coordinates.</param>
    public void Play(Canvas canvas, Point center)
    {
        var rnd = Random.Shared;
        for (var i = 0; i < 14; i++)
        {
            var ellipse = new Ellipse
            {
                Width = 6 + rnd.NextDouble() * 6,
                Height = 6 + rnd.NextDouble() * 6,
                Fill = new SolidColorBrush(Color.FromArgb(220, 45, 160, 140))
            };
            Canvas.SetLeft(ellipse, center.X);
            Canvas.SetTop(ellipse, center.Y);
            canvas.Children.Add(ellipse);

            var angle = rnd.NextDouble() * Math.PI * 2;
            var speed = 40 + rnd.NextDouble() * 80;
            var dx = Math.Cos(angle) * speed;
            var dy = Math.Sin(angle) * speed;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            var frame = 0;
            timer.Tick += (_, _) =>
            {
                frame++;
                Canvas.SetLeft(ellipse, Canvas.GetLeft(ellipse) + dx * 0.06);
                Canvas.SetTop(ellipse, Canvas.GetTop(ellipse) + dy * 0.06);
                ellipse.Opacity = Math.Max(0, 1.0 - frame / 18.0);
                if (frame >= 18)
                {
                    timer.Stop();
                    canvas.Children.Remove(ellipse);
                }
            };
            timer.Start();
        }
    }
}
