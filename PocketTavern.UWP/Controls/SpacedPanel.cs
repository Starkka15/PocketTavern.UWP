using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PocketTavern.UWP.Controls
{
    /// <summary>
    /// A Panel that arranges children like StackPanel with a configurable Spacing gap.
    /// Uses a custom DependencyProperty so it works on all SDK versions including 15063.
    /// </summary>
    public sealed class SpacedPanel : Panel
    {
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register("Spacing", typeof(double), typeof(SpacedPanel),
                new PropertyMetadata(0d, OnLayoutChanged));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(SpacedPanel),
                new PropertyMetadata(Orientation.Vertical, OnLayoutChanged));

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((SpacedPanel)d).InvalidateMeasure();

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            bool horiz = Orientation == Orientation.Horizontal;
            double totalMain = 0d, totalCross = 0d;
            int count = 0;

            foreach (UIElement child in Children)
            {
                child.Measure(availableSize);
                var s = child.DesiredSize;
                if (horiz) { totalMain += s.Width;  totalCross = Math.Max(totalCross, s.Height); }
                else        { totalMain += s.Height; totalCross = Math.Max(totalCross, s.Width); }
                count++;
            }

            if (count > 1) totalMain += Spacing * (count - 1);

            return horiz ? new Size(totalMain, totalCross) : new Size(totalCross, totalMain);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            bool horiz = Orientation == Orientation.Horizontal;
            double offset = 0d;

            foreach (UIElement child in Children)
            {
                var s = child.DesiredSize;
                Rect rect = horiz
                    ? new Rect(offset, 0d, s.Width, finalSize.Height)
                    : new Rect(0d, offset, finalSize.Width, s.Height);
                child.Arrange(rect);
                offset += (horiz ? s.Width : s.Height) + Spacing;
            }

            return finalSize;
        }
    }
}
