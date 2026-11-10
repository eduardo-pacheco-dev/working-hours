using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WorkingHours.Core.Models;

namespace WorkingHours.Controls;

/// <summary>
/// The main timer face: a circular progress ring with tick marks and the
/// remaining time rendered in the centre. Everything is vector-drawn, so it
/// stays crisp at any DPI and instantly follows the active theme.
/// </summary>
public sealed class ProgressDial : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ProgressDial),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RoundProperty = DependencyProperty.Register(
        nameof(Round), typeof(RoundType), typeof(ProgressDial),
        new FrameworkPropertyMetadata(RoundType.Work, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText), typeof(string), typeof(ProgressDial),
        new FrameworkPropertyMetadata("25:00", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(ProgressDial),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DialCountdownProperty = DependencyProperty.Register(
        nameof(DialCountdown), typeof(bool), typeof(ProgressDial),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused), typeof(bool), typeof(ProgressDial),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
        nameof(IsRunning), typeof(bool), typeof(ProgressDial),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public RoundType Round
    {
        get => (RoundType)GetValue(RoundProperty);
        set => SetValue(RoundProperty, value);
    }

    public string TimeText
    {
        get => (string)GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public bool DialCountdown
    {
        get => (bool)GetValue(DialCountdownProperty);
        set => SetValue(DialCountdownProperty, value);
    }

    public bool IsPaused
    {
        get => (bool)GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size < 40)
            return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var thickness = Math.Clamp(size * 0.032, 5, 14);
        var radius = size / 2 - thickness - size * 0.075;

        DrawTicks(dc, center, radius, size);

        var trackColor = LookupBrush("BackgroundLightestBrush", Color.FromRgb(0x9C, 0xA5, 0xB5));
        var track = new Pen(new SolidColorBrush(Color.FromArgb(48, trackColor.R,
            trackColor.G, trackColor.B)), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        track.Freeze();
        dc.DrawEllipse(null, track, center, radius, radius);

        var accent = LookupBrush(Round switch
        {
            RoundType.ShortBreak => "ShortBreakBrush",
            RoundType.LongBreak => "LongBreakBrush",
            _ => "FocusBrush"
        }, Color.FromRgb(0xFF, 0x4E, 0x4D));

        var fraction = Math.Clamp(Progress, 0, 1);
        if (DialCountdown)
            fraction = 1 - fraction;

        var opacity = IsPaused ? 0.45 : 1.0;
        var arcBrush = new SolidColorBrush(accent) { Opacity = opacity };
        arcBrush.Freeze();
        var arcPen = new Pen(arcBrush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        arcPen.Freeze();

        if (fraction >= 0.9999)
        {
            dc.DrawEllipse(null, arcPen, center, radius, radius);
        }
        else if (fraction > 0.0006)
        {
            dc.DrawGeometry(null, arcPen, BuildArc(center, radius, fraction));

            var end = PointOnCircle(center, radius, -90 + fraction * 360);
            var dotBrush = new SolidColorBrush(accent) { Opacity = opacity };
            dotBrush.Freeze();
            var dotRadius = thickness * 0.5;
            var backdrop = LookupBrush("BackgroundBrush", Color.FromRgb(0x2F, 0x38, 0x4B));
            dc.DrawEllipse(new SolidColorBrush(backdrop), null, end, dotRadius + 1.5, dotRadius + 1.5);
            dc.DrawEllipse(dotBrush, null, end, dotRadius, dotRadius);
        }

        DrawText(dc, center, size);
    }

    private static void DrawTicks(DrawingContext dc, Point center, double radius, double size)
    {
        var tickColor = LookupBrush("ForegroundDarkerBrush", Color.FromRgb(0xC0, 0xC9, 0xDA));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, tickColor.R,
            tickColor.G, tickColor.B)), Math.Max(1, size * 0.0035));
        pen.Freeze();

        for (var i = 0; i < 60; i++)
        {
            var angle = i * 6 - 90;
            var major = i % 5 == 0;
            var inner = PointOnCircle(center, radius + size * 0.045, angle);
            var outer = PointOnCircle(center, radius + size * (major ? 0.075 : 0.063), angle);
            dc.DrawLine(pen, inner, outer);
        }
    }

    private void DrawText(DrawingContext dc, Point center, double size)
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var foreground = LookupBrush("ForegroundBrush", Color.FromRgb(0xF6, 0xF2, 0xEB));
        var muted = LookupBrush("ForegroundDarkerBrush", Color.FromRgb(0xC0, 0xC9, 0xDA));

        var timeText = new FormattedText(
            TimeText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal,
                FontWeights.SemiBold, FontStretches.Normal),
            size * 0.235,
            new SolidColorBrush(foreground),
            pixelsPerDip);

        var statusText = new FormattedText(
            StatusText.ToUpperInvariant(),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal,
                FontWeights.SemiBold, FontStretches.Normal),
            Math.Max(9, size * 0.043),
            new SolidColorBrush(muted),
            pixelsPerDip);

        var timeY = center.Y - timeText.Height / 2;
        dc.DrawText(timeText, new Point(center.X - timeText.Width / 2, timeY));
        dc.DrawText(statusText, new Point(center.X - statusText.Width / 2,
            timeY - statusText.Height - size * 0.02));
    }

    private static PathGeometry BuildArc(Point center, double radius, double fraction)
    {
        var sweep = Math.Min(fraction * 360, 359.999);
        var start = PointOnCircle(center, radius, -90);
        var end = PointOnCircle(center, radius, -90 + sweep);

        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false
        };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0,
            sweep > 180, SweepDirection.Clockwise, true));

        return new PathGeometry(new[] { figure });
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var angle = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
    }

    private static Color LookupBrush(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            return brush.Color;
        return fallback;
    }

}
