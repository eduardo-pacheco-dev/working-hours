using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using WorkingHours.Core.Models;

namespace WorkingHours.Services;

/// <summary>Draws the live notification-area icon: a progress ring plus state glyph.</summary>
public static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(
        int size,
        RoundType round,
        double progress,
        bool isRunning,
        bool isPaused,
        Color accent,
        Color background)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var pad = size * 0.09f;
        var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);

        using (var back = new SolidBrush(Color.FromArgb(235, background)))
            graphics.FillEllipse(back, rect);

        using (var ring = new Pen(Color.FromArgb(80, accent), Math.Max(2f, size * 0.09f)))
        {
            ring.StartCap = LineCap.Round;
            ring.EndCap = LineCap.Round;
            graphics.DrawEllipse(ring, rect);
        }

        progress = Math.Clamp(progress, 0, 1);
        if (progress > 0.001)
        {
            using var arc = new Pen(accent, Math.Max(2f, size * 0.11f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            var sweep = (float)(progress * 360.0);
            graphics.DrawArc(arc, rect, -90, Math.Min(sweep, 359.9f));
        }

        var center = new PointF(size / 2f, size / 2f);
        if (isPaused)
        {
            var barWidth = Math.Max(1.6f, size * 0.075f);
            var barHeight = size * 0.24f;
            using var brush = new SolidBrush(accent);
            graphics.FillRectangle(brush, center.X - barWidth * 1.35f, center.Y - barHeight / 2, barWidth, barHeight);
            graphics.FillRectangle(brush, center.X + barWidth * 0.35f, center.Y - barHeight / 2, barWidth, barHeight);
        }
        else if (!isRunning)
        {
            var points = new[]
            {
                new PointF(center.X - size * 0.07f, center.Y - size * 0.11f),
                new PointF(center.X - size * 0.07f, center.Y + size * 0.11f),
                new PointF(center.X + size * 0.12f, center.Y)
            };
            using var brush = new SolidBrush(accent);
            graphics.FillPolygon(brush, points);
        }
        else
        {
            using var brush = new SolidBrush(accent);
            var dot = Math.Max(2f, size * 0.11f);
            graphics.FillEllipse(brush, center.X - dot / 2, center.Y - dot / 2, dot, dot);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
