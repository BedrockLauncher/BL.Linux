using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace BedrockLauncher.UI
{
    /// <summary>
    /// Port of upstream PageAnimator: with "Animate transitions" on, a page swipes in from the side it lies on (by tab
    /// order) while fading in; otherwise the content is swapped at once.
    /// </summary>
    public static class PageAnimator
    {
        private const double SwipeDistance = 150;
        private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(200);

        public static void Navigate(ContentControl frame, Control page, int previousIndex, int newIndex, bool vertical = false)
        {
            if (ReferenceEquals(frame.Content, page)) return;
            frame.Content = page;
            if (!Properties.LauncherSettings.Default.AnimatePageTransitions || previousIndex == newIndex) return;

            double offset = newIndex > previousIndex ? SwipeDistance : -SwipeDistance;
            Thickness from = vertical ? new Thickness(0, offset, 0, -offset) : new Thickness(offset, 0, -offset, 0);
            var animation = new Animation
            {
                Duration = Duration,
                Easing = new CubicEaseOut(),
                FillMode = FillMode.None,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0), new Setter(Layoutable.MarginProperty, from) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0), new Setter(Layoutable.MarginProperty, new Thickness(0)) } },
                }
            };
            _ = animation.RunAsync(page);
        }
    }
}
