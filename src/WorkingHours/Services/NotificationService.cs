using System.Windows;
using WorkingHours.Views;

namespace WorkingHours.Services;

/// <summary>
/// Uses the shell balloon through the tray icon when available, otherwise an
/// in-app toast window so notifications still work with the tray disabled.
/// </summary>
public sealed class NotificationService
{
    private readonly TrayService _tray;
    private ToastWindow? _toast;

    public NotificationService(TrayService tray)
    {
        _tray = tray;
    }

    public bool Enabled { get; set; } = true;

    public void Show(string title, string body)
    {
        if (!Enabled)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
            return;

        dispatcher.Invoke(() =>
        {
            if (_tray.IsVisible)
            {
                _tray.ShowBalloon(title, body);
                return;
            }

            _toast?.Close();
            _toast = new ToastWindow(title, body);
            _toast.Closed += (_, _) => _toast = null;
            _toast.Show();
        });
    }
}
