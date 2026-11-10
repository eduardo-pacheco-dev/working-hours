using WorkingHours.Core.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Services;

/// <summary>Composition root for the long-lived application services.</summary>
public sealed class AppServices : IDisposable
{
    public AppServices(
        AppPaths paths,
        SettingsManager settings,
        StatsStore stats,
        ThemeService themes,
        AudioService audio,
        TrayService tray,
        NotificationService notifications,
        LocalizationService loc,
        AutoSaveService autoSave,
        MainViewModel main)
    {
        Paths = paths;
        Settings = settings;
        Stats = stats;
        Themes = themes;
        Audio = audio;
        Tray = tray;
        Notifications = notifications;
        Loc = loc;
        AutoSave = autoSave;
        Main = main;
    }

    public AppPaths Paths { get; }
    public SettingsManager Settings { get; }
    public StatsStore Stats { get; }
    public ThemeService Themes { get; }
    public AudioService Audio { get; }
    public TrayService Tray { get; }
    public NotificationService Notifications { get; }
    public LocalizationService Loc { get; }
    public AutoSaveService AutoSave { get; }
    public MainViewModel Main { get; }

    public void Dispose()
    {
        AutoSave.Flush();
        Main.Todos.StopAllTracking();
        AutoSave.Dispose();
        Tray.Dispose();
        Audio.Dispose();
        Themes.Dispose();
    }
}
