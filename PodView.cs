using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPods
{
    public enum PodKind { Left, Right, Case }

    /// <summary>One battery ring with an icon in the middle and a percent label below.</summary>
    public class PodView : Grid
    {
        const double Size = 84, Stroke = 5, R = (Size - Stroke) / 2;
        readonly PodKind _kind;
        readonly Path _arc = new Path { StrokeThickness = Stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        readonly TextBlock _percent = new TextBlock();
        readonly TextBlock _bolt = new TextBlock { Text = "⚡", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        readonly Ellipse _led = new Ellipse { Width = 5, Height = 5, Fill = Brushes.LightGray };

        public PodView(PodKind kind)
        {
            _kind = kind;
            Width = 100;
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var ring = new Grid { Width = Size, Height = Size, HorizontalAlignment = HorizontalAlignment.Center };
            ring.Children.Add(new Ellipse { Width = Size - Stroke, Height = Size - Stroke, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)), StrokeThickness = Stroke });
            ring.Children.Add(_arc);
            ring.Children.Add(BuildIcon());
            SetRow(ring, 0);
            Children.Add(ring);

            var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            _percent.FontSize = 17; _percent.FontWeight = FontWeights.SemiBold; _percent.Foreground = Brushes.White; _percent.Text = "--";
            line.Children.Add(_bolt);
            line.Children.Add(_percent);
            SetRow(line, 1);
            Children.Add(line);

            var label = new TextBlock
            {
                Text = kind == PodKind.Left ? "Left" : kind == PodKind.Right ? "Right" : "Case",
                FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xA2)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            SetRow(label, 2);
            Children.Add(label);

            Set(-1, false);
        }

        FrameworkElement BuildIcon()
        {
            var white = new LinearGradientBrush(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xD8, 0xD8, 0xDE), 90);
            var c = new Canvas { Width = 44, Height = 56, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (_kind == PodKind.Case)
            {
                c.Height = 40;
                var body = new Rectangle { Width = 44, Height = 36, RadiusX = 13, RadiusY = 13, Fill = white };
                Canvas.SetTop(body, 2);
                var lid = new Rectangle { Width = 44, Height = 1.5, Fill = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xB0)) };
                Canvas.SetTop(lid, 15);
                Canvas.SetLeft(_led, 19.5); Canvas.SetTop(_led, 24);
                c.Children.Add(body); c.Children.Add(lid); c.Children.Add(_led);
            }
            else
            {
                var stem = new Rectangle { Width = 9, Height = 34, RadiusX = 4.5, RadiusY = 4.5, Fill = white, RenderTransformOrigin = new Point(0.5, 0), RenderTransform = new RotateTransform(-9) };
                Canvas.SetLeft(stem, 22); Canvas.SetTop(stem, 18);
                var head = new Ellipse { Width = 30, Height = 28, Fill = white };
                Canvas.SetLeft(head, 8); Canvas.SetTop(head, 2);
                var tip = new Ellipse { Width = 11, Height = 11, Fill = new SolidColorBrush(Color.FromRgb(0xC9, 0xC9, 0xD0)) };
                Canvas.SetLeft(tip, 1); Canvas.SetTop(tip, 12);
                c.Children.Add(stem); c.Children.Add(head); c.Children.Add(tip);
                if (_kind == PodKind.Right) { c.RenderTransformOrigin = new Point(0.5, 0.5); c.RenderTransform = new ScaleTransform(-1, 1); }
            }
            return c;
        }

        /// <summary>percent: 0-100, or -1 when unknown.</summary>
        public void Set(int percent, bool charging)
        {
            _bolt.Visibility = charging && percent >= 0 ? Visibility.Visible : Visibility.Collapsed;
            _led.Fill = charging ? new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)) : Brushes.LightGray;

            if (percent < 0)
            {
                _percent.Text = "--";
                _arc.Data = null;
                return;
            }
            _percent.Text = percent + "%";
            Color col = charging || percent > 20 ? Color.FromRgb(0x30, 0xD1, 0x58)
                      : percent > 10 ? Color.FromRgb(0xFF, 0x9F, 0x0A)
                      : Color.FromRgb(0xFF, 0x45, 0x3A);
            _arc.Stroke = new SolidColorBrush(col);
            _arc.Data = BuildArc(percent);
        }

        static Geometry BuildArc(int percent)
        {
            double sweep = Math.Max(1, Math.Min(percent, 100)) * 3.6;
            if (sweep > 359.5) sweep = 359.5;
            double cx = Size / 2, cy = Size / 2;
            double rad = (sweep - 90) * Math.PI / 180;
            var end = new Point(cx + R * Math.Cos(rad), cy + R * Math.Sin(rad));
            var fig = new PathFigure { StartPoint = new Point(cx, cy - R), IsClosed = false };
            fig.Segments.Add(new ArcSegment(end, new Size(R, R), 0, sweep > 180, SweepDirection.Clockwise, true));
            return new PathGeometry(new[] { fig });
        }
    }
}
