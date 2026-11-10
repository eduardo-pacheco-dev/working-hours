using System.Text.Json.Serialization;

namespace WorkingHours.Core.Models;

/// <summary>
/// Every user preference. Persisted as JSON in
/// <c>%APPDATA%\WorkingHours\settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    // Behaviour
    public bool AlwaysOnTop { get; set; }
    public bool BreakAlwaysOnTop { get; set; }
    public bool AutoStartWork { get; set; } = true;
    public bool AutoStartBreak { get; set; } = true;

    // Window / tray
    public bool TrayIconEnabled { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool MinimizeToTrayOnClose { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }

    // Cycle
    public int LongBreakInterval { get; set; } = 4;
    public bool ShortBreaksEnabled { get; set; } = true;
    public bool LongBreaksEnabled { get; set; } = true;
    public bool DialCountdown { get; set; } = true;

    // Appearance
    /// <summary>"auto", "light" or "dark".</summary>
    public string ThemeMode { get; set; } = "auto";
    public string ThemeLight { get; set; } = "Pomotroid Light";
    public string ThemeDark { get; set; } = "Pomotroid";

    // Sound
    public bool TickSoundsDuringWork { get; set; }
    public bool TickSoundsDuringBreak { get; set; }
    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; }

    // Durations (seconds)
    public int TimeWorkSeconds { get; set; } = 25 * 60;
    public int TimeShortBreakSeconds { get; set; } = 5 * 60;
    public int TimeLongBreakSeconds { get; set; } = 15 * 60;

    // System integration
    public bool GlobalShortcutsEnabled { get; set; }
    /// <summary>"auto", "en" or "pt-BR".</summary>
    public string Language { get; set; } = "auto";

    // Editing helpers used by the settings window (not persisted separately).
    [JsonIgnore]
    public int WorkMinutes
    {
        get => TimeWorkSeconds / 60;
        set => TimeWorkSeconds = Math.Clamp(value, 1, 240) * 60;
    }

    [JsonIgnore]
    public int ShortBreakMinutes
    {
        get => TimeShortBreakSeconds / 60;
        set => TimeShortBreakSeconds = Math.Clamp(value, 1, 120) * 60;
    }

    [JsonIgnore]
    public int LongBreakMinutes
    {
        get => TimeLongBreakSeconds / 60;
        set => TimeLongBreakSeconds = Math.Clamp(value, 1, 180) * 60;
    }

    [JsonIgnore]
    public int VolumePercent
    {
        get => (int)Math.Round(Volume * 100);
        set => Volume = Math.Clamp(value, 0, 100) / 100.0;
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>Clamps values that come from disk or user input into safe ranges.</summary>
    public void Sanitize()
    {
        TimeWorkSeconds = Math.Clamp(TimeWorkSeconds, 1, 24 * 60 * 60);
        TimeShortBreakSeconds = Math.Clamp(TimeShortBreakSeconds, 1, 24 * 60 * 60);
        TimeLongBreakSeconds = Math.Clamp(TimeLongBreakSeconds, 1, 24 * 60 * 60);
        LongBreakInterval = Math.Clamp(LongBreakInterval, 1, 24);
        Volume = Math.Clamp(Volume, 0, 1);

        ThemeMode = Normalize(ThemeMode, "auto", "light", "dark");
        Language = Normalize(Language, "auto", "en", "pt-BR");
        if (string.IsNullOrWhiteSpace(ThemeLight)) ThemeLight = "Pomotroid Light";
        if (string.IsNullOrWhiteSpace(ThemeDark)) ThemeDark = "Pomotroid";
    }

    private static string Normalize(string value, params string[] allowed) =>
        allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase)) ?? allowed[0];
}
