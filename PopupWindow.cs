using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WinPods
{
    /// <summary>The macOS-style card that slides up from the bottom of the screen.</summary>
    public class PopupWindow : Window
    {
        const double CountdownSeconds = 10;
        const double RingSize = 22, RingStroke = 2.5;

        static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58));
        static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        readonly TextBlock _title = new TextBlock();
        readonly TextBlock _status = new TextBlock();
        readonly TextBlock _statusIcon = new TextBlock();
        readonly StackPanel _pods = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        readonly PodView _left = new PodView(PodKind.Left);
        readonly PodView _right = new PodView(PodKind.Right);
        readonly PodView _case = new PodView(PodKind.Case);
        readonly Ellipse _ring;
        readonly TranslateTransform _slide = new TranslateTransform(0, 60);
        readonly double _dashLength;   // ring circumference in units of the stroke width
        bool _closing;
        int _session;                  // changes on every Present, so an old countdown cannot close a newer popup

        public PopupWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            var card = new Border
            {
                Margin = new Thickness(28),
                Padding = new Thickness(26, 20, 26, 18),
                MinWidth = 352,
                CornerRadius = new CornerRadius(26),
                Background = new LinearGradientBrush(Color.FromRgb(0x2C, 0x2C, 0x30), Color.FromRgb(0x1C, 0x1C, 0x1F), 90),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Opacity = 0.55, Color = Colors.Black },
                RenderTransform = _slide
            };

            _title.FontSize = 18; _title.FontWeight = FontWeights.SemiBold; _title.Foreground = Brushes.White;
            _title.HorizontalAlignment = HorizontalAlignment.Center; _title.Text = "AirPods Pro";

            _statusIcon.FontSize = 11; _statusIcon.Margin = new Thickness(0, 1, 5, 0); _statusIcon.VerticalAlignment = VerticalAlignment.Center;
            _status.FontSize = 13; _status.Foreground = new SolidColorBrush(Color.FromRgb(0xB5, 0xB5, 0xBD));
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 16) };
            statusRow.Children.Add(_statusIcon); statusRow.Children.Add(_status);

            _pods.Children.Add(_left); _pods.Children.Add(_right); _pods.Children.Add(_case);

            var content = new StackPanel();
            content.Children.Add(_title); content.Children.Add(statusRow); content.Children.Add(_pods);

            // Countdown ring in the top-right corner. It empties in 10 seconds, then the popup closes.
            double diameter = RingSize - RingStroke;
            _dashLength = Math.PI * diameter / RingStroke;
            _ring = new Ellipse
            {
                Width = diameter, Height = diameter,
                Stroke = new SolidColorBrush(Color.FromRgb(0xB5, 0xB5, 0xBD)),
                StrokeThickness = RingStroke,
                StrokeDashArray = new DoubleCollection { _dashLength, _dashLength },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-90)   // start at 12 o'clock
            };
            var cross = new TextBlock { Text = "✕", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(0xB5, 0xB5, 0xBD)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var ringHost = new Grid
            {
                Width = RingSize, Height = RingSize, Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -12, -18, 0), Background = Brushes.Transparent
            };
            ringHost.Children.Add(_ring); ringHost.Children.Add(cross);

            var root = new Grid();
            root.Children.Add(content); root.Children.Add(ringHost);
            card.Child = root;
            Content = card;
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");

            MouseLeftButtonDown += (s, e) => Dismiss();
            ShowConnected();
        }

        public void SetName(string name) { if (!string.IsNullOrWhiteSpace(name)) _title.Text = name; }

        public void SetBattery(BatteryInfo b)
        {
            _left.Set(b.Left, b.LeftCharging);
            _right.Set(b.Right, b.RightCharging);
            _case.Set(b.Case, b.CaseCharging);
        }

        public void ShowConnected()
        {
            _statusIcon.Text = "✔"; _statusIcon.Foreground = Green;
            _status.Text = "Connected";
            _pods.Visibility = Visibility.Visible;
        }

        public void ShowDisconnected()
        {
            _statusIcon.Text = "✕"; _statusIcon.Foreground = Red;
            _status.Text = "Disconnected";
            _pods.Visibility = Visibility.Collapsed;
        }

        public void Present()
        {
            int session = ++_session;
            bool alreadyShown = IsVisible && !_closing;   // e.g. "Connected" is on screen when the AirPods disconnect
            _closing = false;
            if (!alreadyShown) { Opacity = 0; _slide.Y = 60; }
            if (!IsVisible) Show();
            UpdateLayout();
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2;
            Top = wa.Bottom - ActualHeight + 14;

            if (!alreadyShown)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)));
                _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
            }

            // Ring empties: the dash moves along the circle until nothing is left.
            var countdown = new DoubleAnimation(0, _dashLength, TimeSpan.FromSeconds(CountdownSeconds));
            countdown.Completed += (s, e) => { if (session == _session) Dismiss(); };
            _ring.BeginAnimation(Shape.StrokeDashOffsetProperty, countdown);
        }

        public void Dismiss()
        {
            if (_closing || !IsVisible) return;
            _closing = true;
            _session++;   // cancel the countdown
            _ring.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
            fade.Completed += (s, e) => { if (_closing) Hide(); };
            BeginAnimation(OpacityProperty, fade);
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(60, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
        }
    }
}
