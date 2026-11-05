using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace BedrockLauncher.Pages.General
{
    /// <summary>
    /// Port of General_LoaderIcon: three blocks drop into a 2x2 square, then a full-size block slides off, every 1.3 s.
    /// The WPF storyboards are evaluated by hand on a 120x120 canvas scaled to the control's size.
    /// </summary>
    public class LoaderIcon : Control
    {
        private const double Canvas = 120, Cube = 60, DropPoint = -65, Cycle = 1.3;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private DispatcherTimer timer;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => InvalidateVisual());
            timer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            timer?.Stop();
            base.OnDetachedFromVisualTree(e);
        }

        private static double Lerp(double t, double begin, double duration, double from, double to)
        {
            if (t <= begin) return from;
            if (duration <= 0 || t >= begin + duration) return to;
            return from + (to - from) * (t - begin) / duration;
        }

        public override void Render(DrawingContext context)
        {
            double t = clock.Elapsed.TotalSeconds % Cycle;
            double scale = Math.Min(Bounds.Width, Bounds.Height) / Canvas;
            if (scale <= 0) return;
            double offsetX = (Bounds.Width - Canvas * scale) / 2, offsetY = (Bounds.Height - Canvas * scale) / 2;

            using (context.PushClip(new Rect(offsetX, offsetY, Canvas * scale, Canvas * scale)))
            {
                void Block(double left, double top, double size, double opacity)
                {
                    if (opacity <= 0) return;
                    using (context.PushOpacity(opacity))
                        context.FillRectangle(Brushes.White, new Rect(offsetX + left * scale, offsetY + top * scale, size * scale, size * scale));
                }

                double dropsOpacity = t < 0.8 ? 1 : 0;

                // ShrinkingCube
                double shrinkOpacity = t < 0.8 ? 0 : (t < 1.2 ? 1 : Lerp(t, 1.2, 0.1, 1, 0));
                Block(Lerp(t, 1.0, 0.2, 0, -Cube), Lerp(t, 1.0, 0.2, 0, Cube), Canvas, shrinkOpacity);
                // Static bottom-left block
                Block(0, Cube, Cube, 1);
                // GridDrop1 (bottom right), GridDrop2 (top left), GridDrop3 (top right)
                Block(Cube, Lerp(t, 0.0, 0.2, DropPoint, Cube), Cube, dropsOpacity);
                Block(0, Lerp(t, 0.3, 0.3, DropPoint, 0), Cube, dropsOpacity);
                Block(Cube, Lerp(t, 0.4, 0.3, DropPoint, 0), Cube, dropsOpacity);
            }
        }
    }
}
