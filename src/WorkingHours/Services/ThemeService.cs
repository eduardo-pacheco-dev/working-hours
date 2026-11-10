using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using WorkingHours.Core.Models;
using WorkingHours.Core.Services;

namespace WorkingHours.Services;

/// <summary>
/// Loads the bundled Pomotroid themes plus user themes from
/// <c>%APPDATA%\WorkingHours\themes</c>, watches that folder for changes and
/// applies the selected palette to the WPF resource dictionary.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private static readonly (string ThemeKey, string ResourceKey)[] ColorMap =
    {
        ("--color-focus-round", "FocusBrush"),
        ("--color-short-round", "ShortBreakBrush"),
        ("--color-long-round", "LongBreakBrush"),
        ("--color-background", "BackgroundBrush"),
        ("--color-background-light", "BackgroundLightBrush"),
        ("--color-background-lightest", "BackgroundLightestBrush"),
        ("--color-foreground", "ForegroundBrush"),
        ("--color-foreground-darker", "ForegroundDarkerBrush"),
        ("--color-foreground-darkest", "ForegroundDarkestBrush"),
        ("--color-accent", "AccentBrush")
    };

    private readonly AppPaths _paths;
    private readonly List<ThemeDefinition> _builtIns = new();
    private readonly List<ThemeDefinition> _custom = new();
    private FileSystemWatcher? _watcher;
    private System.Windows.Threading.DispatcherTimer? _reloadTimer;
    private AppSettings? _lastSettings;

    public ThemeService(AppPaths paths)
    {
        _paths = paths;
    }

    public event EventHandler? ThemesChanged;
    public event EventHandler? ThemeApplied;

    public ThemeDefinition? CurrentTheme { get; private set; }

    public IReadOnlyList<ThemeDefinition> AllThemes =>
        _builtIns
            .Where(b => !_custom.Any(c => string.Equals(c.Name, b.Name, StringComparison.OrdinalIgnoreCase)))
            .Concat(_custom)
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public void Initialize()
    {
        LoadThemes();
        StartWatcher();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void LoadThemes()
    {
        _builtIns.Clear();
        _custom.Clear();

        var builtInDirectory = Path.Combine(AppContext.BaseDirectory, "Themes");
        if (Directory.Exists(builtInDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(builtInDirectory, "*.json"))
                TryLoadTheme(file, false);
        }

        if (Directory.Exists(_paths.ThemesDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(_paths.ThemesDirectory, "*.json"))
                TryLoadTheme(file, true);
        }

        if (_builtIns.Count == 0 &&
            !_custom.Any(c => string.Equals(c.Name, "Pomotroid", StringComparison.OrdinalIgnoreCase)))
        {
            _builtIns.Add(CreateFallbackTheme());
        }

        ThemesChanged?.Invoke(this, EventArgs.Empty);

        if (_lastSettings is not null)
            Apply(_lastSettings);
    }

    public void Apply(AppSettings settings)
    {
        _lastSettings = settings.Clone();

        var useLight = settings.ThemeMode.Equals("light", StringComparison.OrdinalIgnoreCase) ||
                       (settings.ThemeMode.Equals("auto", StringComparison.OrdinalIgnoreCase) && IsSystemLightTheme());

        var selectedName = useLight ? settings.ThemeLight : settings.ThemeDark;
        var theme = AllThemes.FirstOrDefault(t =>
                        string.Equals(t.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                    ?? AllThemes.FirstOrDefault(t => t.IsLight == useLight)
                    ?? AllThemes.FirstOrDefault();

        if (theme is null)
            return;

        Apply(theme);
    }

    public void Apply(ThemeDefinition theme)
    {
        var resources = Application.Current.Resources;
        var fallback = CreateFallbackTheme();

        foreach (var (themeKey, resourceKey) in ColorMap)
        {
            var hex = theme.GetColor(themeKey) ?? fallback.GetColor(themeKey)!;
            if (TryParseColor(hex, out var color))
                resources[resourceKey] = FrozenBrush(color);
        }

        if (TryParseColor(theme.GetColor("--color-accent") ?? "#05EC8C", out var accent))
        {
            resources["OnAccentBrush"] = FrozenBrush(Luminance(accent) > 0.6
                ? Color.FromRgb(0x0B, 0x12, 0x20)
                : Colors.White);
        }

        CurrentTheme = theme;
        ThemeApplied?.Invoke(this, EventArgs.Empty);
    }

    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i ? i != 0 : true;
        }
        catch
        {
            return true;
        }
    }

    private void TryLoadTheme(string file, bool isCustom)
    {
        try
        {
            var json = File.ReadAllText(file);
            var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonDefaults.Options);
            if (theme is null || string.IsNullOrWhiteSpace(theme.Name) || theme.Colors.Count == 0)
                return;

            theme.IsCustom = isCustom;
            theme.FilePath = file;
            theme.IsLight = GuessIsLight(theme);

            if (isCustom)
                _custom.Add(theme);
            else
                _builtIns.Add(theme);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A malformed theme must never prevent the app from starting.
        }
    }

    private static bool GuessIsLight(ThemeDefinition theme)
    {
        var background = theme.GetColor("--color-background");
        if (background is not null && TryParseColor(background, out var color))
            return Luminance(color) > 0.55;
        return theme.Name.Contains("light", StringComparison.OrdinalIgnoreCase) ||
               theme.Name.Contains("latte", StringComparison.OrdinalIgnoreCase) ||
               theme.Name.Contains("dawn", StringComparison.OrdinalIgnoreCase);
    }

    private void StartWatcher()
    {
        _paths.EnsureCreated();
        try
        {
            _reloadTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            _reloadTimer.Tick += (_, _) =>
            {
                _reloadTimer!.Stop();
                LoadThemes();
            };

            _watcher = new FileSystemWatcher(_paths.ThemesDirectory, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _watcher.Created += (_, _) => QueueReload();
            _watcher.Changed += (_, _) => QueueReload();
            _watcher.Deleted += (_, _) => QueueReload();
            _watcher.Renamed += (_, _) => QueueReload();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Theme hot-reload is optional.
        }
    }

    private void QueueReload()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || _reloadTimer is null)
            return;

        dispatcher.BeginInvoke(() =>
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_lastSettings is not null)
            {
                Apply(_lastSettings);
            }
            else if (CurrentTheme is not null)
            {
                ThemeApplied?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private static ThemeDefinition CreateFallbackTheme() => new()
    {
        Name = "Pomotroid",
        IsLight = false,
        Colors = new Dictionary<string, string>
        {
            ["--color-long-round"] = "#0BBDDB",
            ["--color-short-round"] = "#05EC8C",
            ["--color-focus-round"] = "#FF4E4D",
            ["--color-background"] = "#2F384B",
            ["--color-background-light"] = "#3D4457",
            ["--color-background-lightest"] = "#9CA5B5",
            ["--color-foreground"] = "#F6F2EB",
            ["--color-foreground-darker"] = "#C0C9DA",
            ["--color-foreground-darkest"] = "#DBE1EF",
            ["--color-accent"] = "#05EC8C"
        }
    };

    private static bool TryParseColor(string hex, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch
        {
            color = Colors.Transparent;
            return false;
        }
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _watcher?.Dispose();
        _reloadTimer?.Stop();
    }
}
