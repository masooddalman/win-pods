using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WinPods
{
    /// <summary>The macOS-style card that slides up from the bottom of the screen.</summary>
    public class PopupWindow : Window
    {
        readonly TextBlock _title = new TextBlock();
        readonly TextBlock _status = new TextBlock();
        readonly PodView _left = new PodView(PodKind.Left);
        readonly PodView _right = new PodView(PodKind.Right);
        readonly PodView _case = new PodView(PodKind.Case);
        readonly TranslateTransform _slide = new TranslateTransform(0, 60);
        readonly DispatcherTimer _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        bool _closing;

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
                CornerRadius = new CornerRadius(26),
                Background = new LinearGradientBrush(Color.FromRgb(0x2C, 0x2C, 0x30), Color.FromRgb(0x1C, 0x1C, 0x1F), 90),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Opacity = 0.55, Color = Colors.Black },
                RenderTransform = _slide
            };

            _title.FontSize = 18; _title.FontWeight = FontWeights.SemiBold; _title.Foreground = Brushes.White;
            _title.HorizontalAlignment = HorizontalAlignment.Center; _title.Text = "AirPods Pro";

            var check = new TextBlock { Text = "✔", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)), Margin = new Thickness(0, 1, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            _status.Text = "Connected"; _status.FontSize = 13; _status.Foreground = new SolidColorBrush(Color.FromRgb(0xB5, 0xB5, 0xBD));
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 16) };
            statusRow.Children.Add(check); statusRow.Children.Add(_status);

            var pods = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            pods.Children.Add(_left); pods.Children.Add(_right); pods.Children.Add(_case);

            var root = new StackPanel();
            root.Children.Add(_title); root.Children.Add(statusRow); root.Children.Add(pods);
            card.Child = root;
            Content = card;
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");

            _hideTimer.Tick += (s, e) => { _hideTimer.Stop(); Dismiss(); };
            MouseLeftButtonDown += (s, e) => Dismiss();
        }

        public void SetName(string name) { if (!string.IsNullOrWhiteSpace(name)) _title.Text = name; }

        public void SetBattery(BatteryInfo b)
        {
            _left.Set(b.Left, b.LeftCharging);
            _right.Set(b.Right, b.RightCharging);
            _case.Set(b.Case, b.CaseCharging);
        }

        public void Present()
        {
            _closing = false;
            _hideTimer.Stop();
            Opacity = 0; _slide.Y = 60;
            if (!IsVisible) Show();
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2;
            Top = wa.Bottom - ActualHeight + 14;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)));
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
            _hideTimer.Start();
        }

        /// <summary>Keep the card on screen a bit longer, e.g. when new battery data arrives.</summary>
        public void ExtendLife() { if (IsVisible && !_closing) { _hideTimer.Stop(); _hideTimer.Start(); } }

        public void Dismiss()
        {
            if (_closing || !IsVisible) return;
            _closing = true;
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
            fade.Completed += (s, e) => { if (_closing) Hide(); };
            BeginAnimation(OpacityProperty, fade);
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(60, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
        }
    }
}
