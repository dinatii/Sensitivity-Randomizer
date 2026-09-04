using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SensitivityRandomizer
{
    internal sealed class NumericField : UserControl
    {
        private static Brush InputBrush { get { return UiPalette.Current.Input; } }
        private static Brush BorderBrushNormal { get { return UiPalette.Current.Outline; } }
        private static Brush BorderBrushFocus { get { return UiPalette.Current.Accent; } }
        private static Brush BorderBrushError { get { return UiPalette.Current.Danger; } }
        private static Brush TextBrush { get { return UiPalette.Current.TextPrimary; } }
        private static Brush DisabledTextBrush { get { return UiPalette.Current.DisabledText; } }

        private readonly Border _border;
        private readonly TextBox _textBox;
        private bool _suppressTextChanged;
        private decimal _lastValidValue;
        private bool _hasValidValue;

        public event EventHandler ValueChanged;
        public event EventHandler InputStateChanged;

        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
        public decimal Increment { get; set; }
        public int DecimalPlaces { get; set; }

        public bool HasValidValue
        {
            get { return _hasValidValue; }
        }

        public decimal Value
        {
            get { return _lastValidValue; }
            set
            {
                var clamped = Math.Max(Minimum, Math.Min(Maximum, value));
                var changed = clamped != _lastValidValue || !_hasValidValue;
                _lastValidValue = clamped;
                _hasValidValue = true;
                WriteFormattedValue();
                UpdateBorder();
                if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
                InputStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public NumericField()
        {
            Minimum = decimal.MinValue;
            Maximum = decimal.MaxValue;
            Increment = 1m;
            DecimalPlaces = 0;
            Height = 36;
            MinWidth = 72;

            _textBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = TextBrush,
                CaretBrush = TextBrush,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(10, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            _border = new Border
            {
                Background = InputBrush,
                BorderBrush = BorderBrushNormal,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = _textBox
            };
            Content = _border;

            _textBox.TextChanged += TextBoxOnTextChanged;
            _textBox.GotKeyboardFocus += (sender, args) =>
            {
                UpdateBorder();
                _textBox.SelectAll();
            };
            _textBox.LostKeyboardFocus += (sender, args) =>
            {
                if (_hasValidValue) WriteFormattedValue();
                UpdateBorder();
            };
            _textBox.PreviewTextInput += (sender, args) =>
            {
                foreach (var character in args.Text)
                {
                    if (!char.IsDigit(character) && character != '.' && character != ',' && character != '-')
                    {
                        args.Handled = true;
                        return;
                    }
                }
            };
            _textBox.PreviewKeyDown += TextBoxOnPreviewKeyDown;
            _textBox.PreviewMouseWheel += TextBoxOnPreviewMouseWheel;
            IsEnabledChanged += (sender, args) =>
            {
                _textBox.IsReadOnly = !IsEnabled;
                _textBox.Foreground = IsEnabled ? TextBrush : DisabledTextBrush;
                Opacity = IsEnabled ? 1.0 : 0.62;
                UpdateBorder();
            };
        }

        public void RefreshText()
        {
            WriteFormattedValue();
        }

        private void TextBoxOnTextChanged(object sender, TextChangedEventArgs args)
        {
            if (_suppressTextChanged) return;

            decimal parsed;
            var valid = TryParse(_textBox.Text, out parsed) && parsed >= Minimum && parsed <= Maximum;
            var changed = valid && (!_hasValidValue || parsed != _lastValidValue);
            _hasValidValue = valid;
            if (valid) _lastValidValue = parsed;
            UpdateBorder();
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
            InputStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TextBoxOnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter)
            {
                if (_hasValidValue) WriteFormattedValue();
                Keyboard.ClearFocus();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                _hasValidValue = true;
                WriteFormattedValue();
                UpdateBorder();
                InputStateChanged?.Invoke(this, EventArgs.Empty);
                args.Handled = true;
            }
            else if (args.Key == Key.Up || args.Key == Key.Down)
            {
                Step(args.Key == Key.Up ? Increment : -Increment);
                args.Handled = true;
            }
        }

        private void TextBoxOnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
        {
            if (!_textBox.IsKeyboardFocusWithin || !IsEnabled) return;
            Step(args.Delta > 0 ? Increment : -Increment);
            args.Handled = true;
        }

        private void Step(decimal delta)
        {
            if (!IsEnabled) return;
            var origin = _hasValidValue ? _lastValidValue : Math.Max(Minimum, Math.Min(Maximum, 0m));
            Value = origin + delta;
            _textBox.SelectAll();
        }

        private void WriteFormattedValue()
        {
            _suppressTextChanged = true;
            try
            {
                _textBox.Text = _lastValidValue.ToString("F" + DecimalPlaces, CultureInfo.InvariantCulture);
            }
            finally
            {
                _suppressTextChanged = false;
            }
        }

        private void UpdateBorder()
        {
            if (!_hasValidValue)
                _border.BorderBrush = BorderBrushError;
            else if (_textBox.IsKeyboardFocusWithin && IsEnabled)
                _border.BorderBrush = BorderBrushFocus;
            else
                _border.BorderBrush = BorderBrushNormal;
        }

        private static bool TryParse(string text, out decimal value)
        {
            var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool _isChecked;

        public event EventHandler CheckedChanged;

        public string Text { get; set; }

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                InvalidateVisual();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            Focusable = true;
            Cursor = Cursors.Hand;
            Height = 28;
            MinWidth = 48;
            SnapsToDevicePixels = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
        {
            base.OnMouseLeftButtonUp(args);
            if (!IsEnabled) return;
            IsChecked = !IsChecked;
            Focus();
            args.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs args)
        {
            base.OnKeyDown(args);
            if (IsEnabled && (args.Key == Key.Space || args.Key == Key.Enter))
            {
                IsChecked = !IsChecked;
                args.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var track = new Rect(1, Math.Max(1, (ActualHeight - 22) / 2), 42, 22);
            var trackBrush = !IsEnabled
                ? BrushFrom(UiPalette.Current.ToggleDisabledColor)
                : IsChecked ? BrushFrom(UiPalette.Current.AccentStrongColor) : BrushFrom(UiPalette.Current.ToggleOffColor);
            var trackPen = new Pen(
                IsChecked && IsEnabled ? UiPalette.Current.Accent : UiPalette.Current.Outline,
                1);
            drawingContext.DrawRoundedRectangle(trackBrush, trackPen, track, 11, 11);

            var knobX = IsChecked ? track.Right - 18 : track.Left + 4;
            var knobBrush = IsEnabled ? Brushes.White : UiPalette.Current.DisabledText;
            drawingContext.DrawEllipse(knobBrush, null, new Point(knobX + 7, track.Top + 11), 7, 7);

            if (!string.IsNullOrEmpty(Text))
            {
                var textBrush = IsEnabled ? UiPalette.Current.TextPrimary : UiPalette.Current.DisabledText;
                var formatted = new FormattedText(
                    Text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                    12,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                drawingContext.DrawText(formatted, new Point(51, Math.Max(0, (ActualHeight - formatted.Height) / 2)));
            }

            if (IsKeyboardFocused)
            {
                var focusPen = new Pen(UiPalette.Current.Purple, 1);
                focusPen.DashStyle = DashStyles.Dot;
                drawingContext.DrawRoundedRectangle(null, focusPen,
                    new Rect(0.5, 0.5, Math.Max(1, ActualWidth - 1), Math.Max(1, ActualHeight - 1)), 7, 7);
            }
        }

        private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush BrushFrom(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    internal sealed class SmoothScrollHost : UserControl
    {
        public SmoothScrollHost(UIElement content)
        {
            var viewer = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                CanContentScroll = false,
                PanningMode = PanningMode.VerticalOnly
            };
            var contentMargin = new Border
            {
                Padding = new Thickness(1, 0, 5, 0),
                Background = Brushes.Transparent,
                Child = viewer
            };
            var bar = new SlimScrollBar(viewer)
            {
                Width = 10,
                Margin = new Thickness(2, 2, 0, 2)
            };

            var layout = new Grid { Background = Brushes.Transparent };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            Grid.SetColumn(contentMargin, 0);
            Grid.SetColumn(bar, 1);
            layout.Children.Add(contentMargin);
            layout.Children.Add(bar);
            Content = layout;
        }
    }

    internal sealed class SlimScrollBar : FrameworkElement
    {
        private static Brush RailBrush { get { return BrushFrom(UiPalette.Current.ScrollRailColor); } }
        private static Brush ThumbBrush { get { return BrushFrom(UiPalette.Current.ScrollThumbColor); } }
        private static Brush ThumbHoverBrush { get { return BrushFrom(UiPalette.Current.ScrollThumbHoverColor); } }
        private readonly ScrollViewer _viewer;
        private bool _dragging;
        private double _dragOffset;
        private Rect _thumbBounds;

        public SlimScrollBar(ScrollViewer viewer)
        {
            _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
            _viewer.ScrollChanged += (sender, args) => InvalidateVisual();
            _viewer.SizeChanged += (sender, args) => InvalidateVisual();
            MouseEnter += (sender, args) => InvalidateVisual();
            MouseLeave += (sender, args) => InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var usableHeight = Math.Max(0, ActualHeight);
            if (usableHeight < 20 || _viewer.ScrollableHeight <= 0.5 || _viewer.ExtentHeight <= 0)
            {
                _thumbBounds = Rect.Empty;
                return;
            }

            drawingContext.DrawRoundedRectangle(
                RailBrush,
                null,
                new Rect(Math.Max(0, (ActualWidth - 3) / 2), 1, 3, Math.Max(1, usableHeight - 2)),
                1.5,
                1.5);

            var fractionVisible = Math.Min(1.0, _viewer.ViewportHeight / _viewer.ExtentHeight);
            var thumbHeight = Math.Max(30, usableHeight * fractionVisible);
            thumbHeight = Math.Min(usableHeight, thumbHeight);
            var travel = Math.Max(0, usableHeight - thumbHeight);
            var ratio = _viewer.ScrollableHeight <= 0 ? 0 : _viewer.VerticalOffset / _viewer.ScrollableHeight;
            var top = travel * Math.Max(0, Math.Min(1, ratio));
            _thumbBounds = new Rect(Math.Max(0, (ActualWidth - 7) / 2), top, 7, thumbHeight);
            drawingContext.DrawRoundedRectangle(IsMouseOver || _dragging ? ThumbHoverBrush : ThumbBrush,
                null, _thumbBounds, 3.5, 3.5);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
        {
            base.OnMouseLeftButtonDown(args);
            if (_viewer.ScrollableHeight <= 0.5 || _thumbBounds.IsEmpty) return;
            var point = args.GetPosition(this);
            _dragOffset = _thumbBounds.Contains(point) ? point.Y - _thumbBounds.Top : _thumbBounds.Height / 2;
            _dragging = true;
            CaptureMouse();
            ScrollToPointer(point.Y);
            args.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs args)
        {
            base.OnMouseMove(args);
            if (!_dragging) return;
            ScrollToPointer(args.GetPosition(this).Y);
            args.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
        {
            base.OnMouseLeftButtonUp(args);
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            InvalidateVisual();
            args.Handled = true;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs args)
        {
            base.OnMouseWheel(args);
            _viewer.ScrollToVerticalOffset(_viewer.VerticalOffset - args.Delta);
            args.Handled = true;
        }

        private void ScrollToPointer(double pointerY)
        {
            var travel = Math.Max(1, ActualHeight - _thumbBounds.Height);
            var top = Math.Max(0, Math.Min(travel, pointerY - _dragOffset));
            _viewer.ScrollToVerticalOffset(_viewer.ScrollableHeight * top / travel);
        }

        private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush BrushFrom(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    internal sealed class WpfMultiplierChart : FrameworkElement
    {
        private sealed class ChartPoint
        {
            public DateTime TimestampUtc { get; set; }
            public double Multiplier { get; set; }
        }

        private readonly List<ChartPoint> _points = new List<ChartPoint>();
        private int _windowSeconds = 120;
        private double _configuredMinimum = 0.50;
        private double _configuredMaximum = 2.00;

        public WpfMultiplierChart()
        {
            MinWidth = 260;
            MinHeight = 155;
            SnapsToDevicePixels = true;
        }

        public void Configure(double minimum, double maximum, int windowSeconds)
        {
            _configuredMinimum = Math.Max(0.001, minimum);
            _configuredMaximum = Math.Max(_configuredMinimum + 0.001, maximum);
            _windowSeconds = Math.Max(30, Math.Min(600, windowSeconds));
            InvalidateVisual();
        }

        public void AddPoint(DateTime timestampUtc, double multiplier)
        {
            _points.Add(new ChartPoint { TimestampUtc = timestampUtc, Multiplier = multiplier });
            Trim(timestampUtc);
            InvalidateVisual();
        }

        public void ClearPoints()
        {
            _points.Clear();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(BrushFrom(UiPalette.Current.ChartBackgroundColor), null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (ActualWidth < 120 || ActualHeight < 90) return;

            var plot = new Rect(48, 13, Math.Max(1, ActualWidth - 60), Math.Max(1, ActualHeight - 39));
            var configuredLogMinimum = Math.Log(_configuredMinimum);
            var configuredLogMaximum = Math.Log(_configuredMaximum);
            var logPadding = Math.Max(0.025, (configuredLogMaximum - configuredLogMinimum) * 0.08);
            var yMinimum = Math.Exp(configuredLogMinimum - logPadding);
            var yMaximum = Math.Exp(configuredLogMaximum + logPadding);
            var gridPen = new Pen(BrushFrom(UiPalette.Current.ChartGridColor), 1);
            var basePen = new Pen(BrushFrom(UiPalette.Current.ChartBaseColor), 1) { DashStyle = DashStyles.Dash };
            var labelBrush = UiPalette.Current.TextSecondary;

            for (var index = 0; index <= 4; index++)
            {
                var y = plot.Top + plot.Height * index / 4.0;
                drawingContext.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
                var logValue = Math.Log(yMaximum) - (Math.Log(yMaximum) - Math.Log(yMinimum)) * index / 4.0;
                DrawText(drawingContext, Math.Exp(logValue).ToString("0.###", CultureInfo.InvariantCulture), 10.5,
                    labelBrush, new Point(3, y - 7));
            }
            for (var index = 0; index <= 4; index++)
            {
                var x = plot.Left + plot.Width * index / 4.0;
                drawingContext.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            }

            if (1.0 >= yMinimum && 1.0 <= yMaximum)
            {
                var baseY = MapY(1.0, yMinimum, yMaximum, plot);
                drawingContext.DrawLine(basePen, new Point(plot.Left, baseY), new Point(plot.Right, baseY));
                DrawText(drawingContext, "BASE", 10, labelBrush, new Point(plot.Right - 30, baseY - 15));
            }
            DrawText(drawingContext, "-" + _windowSeconds + "s", 10, labelBrush, new Point(plot.Left, plot.Bottom + 7));
            DrawText(drawingContext, "now", 10, labelBrush, new Point(plot.Right - 20, plot.Bottom + 7));

            if (_points.Count == 0)
            {
                var message = UiText.T("График появится после первой смены", "The chart appears after the first change");
                var formatted = CreateText(message, 11.5, BrushFrom(UiPalette.Current.ChartEmptyColor));
                drawingContext.DrawText(formatted, new Point(
                    plot.Left + Math.Max(0, (plot.Width - formatted.Width) / 2),
                    plot.Top + Math.Max(0, (plot.Height - formatted.Height) / 2)));
                return;
            }

            var now = DateTime.UtcNow;
            var points = new List<Point>(_points.Count);
            foreach (var item in _points)
            {
                var ageSeconds = Math.Max(0, (now - item.TimestampUtc).TotalSeconds);
                var x = plot.Right - ageSeconds / _windowSeconds * plot.Width;
                points.Add(new Point(x, MapY(item.Multiplier, yMinimum, yMaximum, plot)));
            }
            if (points.Count == 0) return;

            drawingContext.PushClip(new RectangleGeometry(plot));
            if (points.Count > 1)
            {
                var fillGeometry = new StreamGeometry();
                using (var context = fillGeometry.Open())
                {
                    context.BeginFigure(new Point(points[0].X, plot.Bottom), true, true);
                    context.LineTo(points[0], true, false);
                    for (var index = 1; index < points.Count; index++)
                        context.LineTo(points[index], true, false);
                    context.LineTo(new Point(points[points.Count - 1].X, plot.Bottom), true, false);
                }
                fillGeometry.Freeze();
                var fillColor = UiPalette.Current.BlueColor;
                drawingContext.DrawGeometry(BrushFrom(55, fillColor.R, fillColor.G, fillColor.B), null, fillGeometry);

                var lineGeometry = new StreamGeometry();
                using (var context = lineGeometry.Open())
                {
                    context.BeginFigure(points[0], false, false);
                    for (var index = 1; index < points.Count; index++)
                        context.LineTo(points[index], true, false);
                }
                lineGeometry.Freeze();
                drawingContext.DrawGeometry(null, new Pen(UiPalette.Current.Accent, 2.1), lineGeometry);
            }
            var last = points[points.Count - 1];
            drawingContext.DrawEllipse(Brushes.White, new Pen(UiPalette.Current.Accent, 1.5), last, 3.5, 3.5);
            drawingContext.Pop();
        }

        private void Trim(DateTime nowUtc)
        {
            var cutoff = nowUtc.AddSeconds(-_windowSeconds - 5);
            while (_points.Count > 0 && _points[0].TimestampUtc < cutoff)
                _points.RemoveAt(0);
        }

        private static double MapY(double value, double minimum, double maximum, Rect plot)
        {
            var safe = Math.Max(minimum, Math.Min(maximum, value));
            var normalized = (Math.Log(safe) - Math.Log(minimum)) /
                (Math.Log(maximum) - Math.Log(minimum));
            return plot.Bottom - normalized * plot.Height;
        }

        private void DrawText(DrawingContext context, string text, double size, Brush brush, Point point)
        {
            context.DrawText(CreateText(text, size, brush), point);
        }

        private FormattedText CreateText(string text, double size, Brush brush)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                size,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
        {
            return BrushFrom(255, red, green, blue);
        }

        private static SolidColorBrush BrushFrom(byte alpha, byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush BrushFrom(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    internal sealed class LogoMark : FrameworkElement
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var width = Math.Max(1, ActualWidth);
            var height = Math.Max(1, ActualHeight);
            var size = Math.Min(width, height);
            var left = (width - size) / 2;
            var top = (height - size) / 2;
            var bounds = new Rect(left, top, size, size);
            var radius = size * 0.29;
            drawingContext.DrawRoundedRectangle(
                new LinearGradientBrush(Color.FromRgb(72, 211, 203), Color.FromRgb(153, 137, 239), 35),
                null,
                bounds,
                radius,
                radius);

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(left + size * 0.73, top + size * 0.30), false, false);
                context.BezierTo(
                    new Point(left + size * 0.60, top + size * 0.15),
                    new Point(left + size * 0.26, top + size * 0.17),
                    new Point(left + size * 0.25, top + size * 0.39), true, false);
                context.BezierTo(
                    new Point(left + size * 0.24, top + size * 0.57),
                    new Point(left + size * 0.77, top + size * 0.48),
                    new Point(left + size * 0.73, top + size * 0.70), true, false);
                context.BezierTo(
                    new Point(left + size * 0.68, top + size * 0.88),
                    new Point(left + size * 0.36, top + size * 0.88),
                    new Point(left + size * 0.24, top + size * 0.73), true, false);
            }
            geometry.Freeze();
            var pen = new Pen(Brushes.White, Math.Max(2.2, size * 0.09))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            drawingContext.DrawGeometry(null, pen, geometry);
        }
    }
}
