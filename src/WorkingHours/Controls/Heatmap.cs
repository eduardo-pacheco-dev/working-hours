using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WorkingHours.Controls;

/// <summary>
/// A 53-week contribution-style heatmap of daily focus minutes, in the spirit
/// of Pomotroid's all-time statistics view.
/// </summary>
public sealed class Heatmap : FrameworkElement
{
    private const int Weeks = 53;
    private const double LeftLabel = 30;
    private const double TopLabel = 18;

    private DateOnly _gridStart;
    private double _cell;
    private double _gap;
    private double _gridTop;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyDictionary<DateOnly, double>), typeof(Heatmap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyDictionary<DateOnly, double>? Values
    {
        get => (IReadOnlyDictionary<DateOnly, double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Heatmap()
    {
        ToolTipService.SetInitialShowDelay(this, 200);
        ToolTipService.SetShowDuration(this, 60000);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (ActualWidth < 60 || ActualHeight < 60)
            return;

        var faint = LookupBrush("BackgroundLightestBrush", Color.FromRgb(0x9C, 0xA5, 0xB5));
        var muted = LookupBrush("ForegroundDarkerBrush", Color.FromRgb(0xC0, 0xC9, 0xDA));
        var accent = LookupBrush("AccentBrush", Color.FromRgb(0, 200, 130));
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        _gap = Math.Max(2, ActualWidth / 700);
        var availableWidth = ActualWidth - LeftLabel - (Weeks - 1) * _gap;
        var availableHeight = ActualHeight - TopLabel - 6 * _gap;
        _cell = Math.Max(3, Math.Min(availableWidth / Weeks, availableHeight / 7));
        _gridTop = TopLabel;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysSinceSaturday = ((int)today.DayOfWeek + 1) % 7;
        var end = today.AddDays(daysSinceSaturday);
        _gridStart = end.AddDays(-(Weeks * 7 - 1));

        var culture = CultureInfo.CurrentCulture;
        var labelBrush = new SolidColorBrush(muted);
        labelBrush.Freeze();
        var typeface = new Typeface(new FontFamily("Segoe UI, Segoe UI"), FontStyles.Normal,
            FontWeights.Normal, FontStretches.Normal);

        // Month labels across the top.
        var lastMonth = -1;
        for (var week = 0; week < Weeks; week++)
        {
            var date = _gridStart.AddDays(week * 7);
            if (date.Month == lastMonth || date.Day > 7)
                continue;

            lastMonth = date.Month;
            var name = culture.DateTimeFormat.AbbreviatedMonthNames[date.Month - 1].TrimEnd('.');
            var text = new FormattedText(name, culture, FlowDirection.LeftToRight, typeface, 9.5,
                labelBrush, pixelsPerDip);
            var x = LeftLabel + week * (_cell + _gap);
            if (x + text.Width < ActualWidth)
                dc.DrawText(text, new Point(x, 1));
        }

        // Weekday labels (Mon / Wed / Fri).
        foreach (var row in new[] { 1, 3, 5 })
        {
            var date = _gridStart.AddDays(row);
            var name = culture.DateTimeFormat.AbbreviatedDayNames[(int)date.DayOfWeek].TrimEnd('.');
            if (name.Length > 3)
                name = name[..3];
            var text = new FormattedText(name, culture, FlowDirection.LeftToRight, typeface, 9.5,
                labelBrush, pixelsPerDip);
            var y = _gridTop + row * (_cell + _gap) + (_cell - text.Height) / 2;
            dc.DrawText(text, new Point(LeftLabel - text.Width - 6, y));
        }

        var values = Values;
        var emptyBrush = new SolidColorBrush(Color.FromArgb(40, faint.R, faint.G, faint.B));
        emptyBrush.Freeze();

        for (var week = 0; week < Weeks; week++)
        {
            for (var row = 0; row < 7; row++)
            {
                var date = _gridStart.AddDays(week * 7 + row);
                if (date > today)
                    continue;

                var minutes = values is not null && values.TryGetValue(date, out var m) ? m : 0;
                var brush = minutes <= 0 ? emptyBrush : LevelBrush(accent, minutes);
                var x = LeftLabel + week * (_cell + _gap);
                var y = _gridTop + row * (_cell + _gap);
                dc.DrawRoundedRectangle(brush, null, new Rect(x, y, _cell, _cell), 2, 2);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var position = e.GetPosition(this);

        var week = (int)Math.Floor((position.X - LeftLabel) / (_cell + _gap));
        var row = (int)Math.Floor((position.Y - _gridTop) / (_cell + _gap));
        var withinCell = position.X >= LeftLabel + week * (_cell + _gap) &&
                         position.Y >= _gridTop + row * (_cell + _gap);

        if (week < 0 || week >= Weeks || row < 0 || row >= 7 || !withinCell)
        {
            ToolTip = null;
            return;
        }

        var date = _gridStart.AddDays(week * 7 + row);
        if (date > DateOnly.FromDateTime(DateTime.Today))
        {
            ToolTip = null;
            return;
        }

        var minutes = Values is not null && Values.TryGetValue(date, out var value) ? value : 0;
        var culture = CultureInfo.CurrentCulture;
        var dateLabel = date.ToString("ddd, d MMM", culture);
        ToolTip = minutes <= 0
            ? dateLabel
            : $"{dateLabel} · {minutes:0} min";
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ToolTip = null;
    }

    private static SolidColorBrush LevelBrush(Color accent, double minutes)
    {
        var alpha = minutes switch
        {
            < 25 => 0.30,
            < 50 => 0.52,
            < 90 => 0.75,
            _ => 1.0
        };
        var brush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), accent.R, accent.G, accent.B));
        brush.Freeze();
        return brush;
    }

    private static Color LookupBrush(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            return brush.Color;
        return fallback;
    }
}
