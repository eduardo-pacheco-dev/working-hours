using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WorkingHours.Controls;

/// <summary>Lightweight rounded bar chart used by the statistics views.</summary>
public sealed class BarChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(BarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(
        nameof(Labels), typeof(IReadOnlyList<string>), typeof(BarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightIndexProperty = DependencyProperty.Register(
        nameof(HighlightIndex), typeof(int), typeof(BarChart),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IReadOnlyList<string>? Labels
    {
        get => (IReadOnlyList<string>?)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public int HighlightIndex
    {
        get => (int)GetValue(HighlightIndexProperty);
        set => SetValue(HighlightIndexProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var values = Values;
        if (values is null || values.Count == 0 || ActualWidth < 20 || ActualHeight < 20)
            return;

        var accent = LookupBrush("AccentBrush", Color.FromRgb(0, 200, 130));
        var muted = LookupBrush("ForegroundDarkerBrush", Color.FromRgb(0xC0, 0xC9, 0xDA));
        var faint = LookupBrush("BackgroundLightestBrush", Color.FromRgb(0x9C, 0xA5, 0xB5));
        var baselineBrush = new SolidColorBrush(Color.FromArgb(46, faint.R, faint.G, faint.B));
        baselineBrush.Freeze();

        const double labelHeight = 22;
        const double topPad = 8;
        var chartHeight = Math.Max(10, ActualHeight - labelHeight - topPad);
        var baselineY = ActualHeight - labelHeight;
        dc.DrawLine(new Pen(baselineBrush, 1), new Point(0, baselineY + 0.5),
            new Point(ActualWidth, baselineY + 0.5));

        var max = Math.Max(1, values.Max());
        var slot = ActualWidth / values.Count;
        var barWidth = Math.Max(3, Math.Min(28, slot * 0.56));
        var radius = Math.Min(6, barWidth / 2);

        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var labelStep = values.Count <= 12 ? 1 : Math.Max(1, (int)Math.Ceiling(values.Count / 8.0));

        for (var i = 0; i < values.Count; i++)
        {
            var value = Math.Max(0, values[i]);
            var height = value <= 0 ? 3 : Math.Max(4, chartHeight * (value / max));
            var x = i * slot + (slot - barWidth) / 2;
            var y = baselineY - height;

            var brush = new SolidColorBrush(i == HighlightIndex ? accent : WithAlpha(accent, 0.78));
            brush.Freeze();

            if (value <= 0)
            {
                var empty = new SolidColorBrush(WithAlpha(faint, 0.28));
                empty.Freeze();
                dc.DrawRoundedRectangle(empty, null, new Rect(x, baselineY - 3, barWidth, 3), 1.5, 1.5);
            }
            else
            {
                dc.DrawRoundedRectangle(brush, null, new Rect(x, y, barWidth, height), radius, radius);
            }

            var labels = Labels;
            if (labels is not null && i < labels.Count && i % labelStep == 0 && !string.IsNullOrEmpty(labels[i]))
            {
                var text = new FormattedText(labels[i], CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI, Segoe UI"), FontStyles.Normal,
                        FontWeights.Normal, FontStretches.Normal),
                    10.5, new SolidColorBrush(muted), pixelsPerDip);
                dc.DrawText(text, new Point(i * slot + (slot - text.Width) / 2, baselineY + 6));
            }
        }
    }

    private static Color LookupBrush(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            return brush.Color;
        return fallback;
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);
}
