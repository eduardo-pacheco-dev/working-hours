using System.Drawing;
using WorkingHours.Core.Models;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace WorkingHours.Services;

/// <summary>
/// Native notification-area integration using the shell's NotifyIcon.
/// The icon redraws on every tick and the context menu mirrors the timer state.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.ToolStripMenuItem _resetItem;
    private readonly Forms.ToolStripMenuItem _skipItem;
    private readonly Forms.ToolStripMenuItem _showItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private Icon? _currentIcon;
    private bool _disposed;

    public TrayService()
    {
        _toggleItem = new Forms.ToolStripMenuItem("Start / pause");
        _resetItem = new Forms.ToolStripMenuItem("Reset");
        _skipItem = new Forms.ToolStripMenuItem("Skip");
        _showItem = new Forms.ToolStripMenuItem("Show Working Hours") { Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _exitItem = new Forms.ToolStripMenuItem("Exit");

        _toggleItem.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        _resetItem.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        _skipItem.Click += (_, _) => SkipRequested?.Invoke(this, EventArgs.Empty);
        _showItem.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _toggleItem, _resetItem, _skipItem,
            new Forms.ToolStripSeparator(),
            _showItem, _exitItem
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Working Hours",
            ContextMenuStrip = _menu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ToggleRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? SkipRequested;
    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public bool IsVisible => _notifyIcon.Visible;

    public void Update(
        bool visible,
        string tooltip,
        RoundType round,
        double progress,
        bool isRunning,
        bool isPaused,
        string toggleText,
        string resetText,
        string skipText,
        string showText,
        string exitText)
    {
        if (_disposed)
            return;

        _notifyIcon.Visible = visible;
        _toggleItem.Text = toggleText;
        _resetItem.Text = resetText;
        _skipItem.Text = skipText;
        _showItem.Text = showText;
        _exitItem.Text = exitText;

        if (!visible)
            return;

        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        var accent = ReadColor("AccentBrush", Color.FromArgb(0x05, 0xEC, 0x8C));
        var background = ReadColor("BackgroundBrush", Color.FromArgb(0x2F, 0x38, 0x4B));
        accent = ReadColor(round switch
        {
            RoundType.Work => "FocusBrush",
            RoundType.ShortBreak => "ShortBreakBrush",
            _ => "LongBreakBrush"
        }, accent);

        var size = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
        var newIcon = TrayIconRenderer.Create(size, round, progress, isRunning, isPaused, accent, background);
        var previous = _currentIcon;
        _currentIcon = newIcon;
        _notifyIcon.Icon = newIcon;
        previous?.Dispose();
    }

    public void ShowBalloon(string title, string message)
    {
        if (_disposed || !_notifyIcon.Visible)
            return;

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.None;
        _notifyIcon.ShowBalloonTip(6000);
    }

    public void Hide()
    {
        if (!_disposed)
            _notifyIcon.Visible = false;
    }

    private static Color ReadColor(string resourceKey, Color fallback)
    {
        if (WpfApplication.Current?.Resources[resourceKey] is MediaBrush brush)
        {
            var c = brush.Color;
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        return fallback;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _menu.Dispose();
    }
}
