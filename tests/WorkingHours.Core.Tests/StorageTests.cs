using WorkingHours.Core.Models;
using WorkingHours.Core.Services;

namespace WorkingHours.Core.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WorkingHoursTests-" + Guid.NewGuid().ToString("N"));

    public StorageTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var store = new SettingsStore(Path.Combine(_root, "settings.json"));
        var settings = new AppSettings
        {
            TimeWorkSeconds = 1800,
            TimeShortBreakSeconds = 240,
            LongBreakInterval = 5,
            ThemeMode = "dark",
            ThemeDark = "Dracula",
            Volume = 0.42,
            AlwaysOnTop = true
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(1800, loaded.TimeWorkSeconds);
        Assert.Equal(240, loaded.TimeShortBreakSeconds);
        Assert.Equal(5, loaded.LongBreakInterval);
        Assert.Equal("dark", loaded.ThemeMode);
        Assert.Equal("Dracula", loaded.ThemeDark);
        Assert.Equal(0.42, loaded.Volume, 3);
        Assert.True(loaded.AlwaysOnTop);
    }

    [Fact]
    public void Settings_AreSanitized()
    {
        var store = new SettingsStore(Path.Combine(_root, "settings.json"));
        var settings = new AppSettings
        {
            TimeWorkSeconds = -5,
            TimeShortBreakSeconds = 0,
            LongBreakInterval = 999,
            Volume = 4.5,
            ThemeMode = "neon",
            Language = "klingon"
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(1, loaded.TimeWorkSeconds);
        Assert.Equal(1, loaded.TimeShortBreakSeconds);
        Assert.Equal(24, loaded.LongBreakInterval);
        Assert.Equal(1, loaded.Volume);
        Assert.Equal("auto", loaded.ThemeMode);
        Assert.Equal("auto", loaded.Language);
    }

    [Fact]
    public void MissingSettingsFile_ReturnsDefaults()
    {
        var store = new SettingsStore(Path.Combine(_root, "missing.json"));
        var loaded = store.Load();

        Assert.Equal(25 * 60, loaded.TimeWorkSeconds);
        Assert.Equal(4, loaded.LongBreakInterval);
    }

    [Fact]
    public void Stats_AddAndPersistAcrossInstances()
    {
        var path = Path.Combine(_root, "sessions.json");
        var store = new StatsStore(path);

        store.Add(new SessionRecord { StartedAt = DateTime.Today.AddHours(9), DurationSeconds = 1500 });
        store.Add(new SessionRecord { StartedAt = DateTime.Today.AddHours(10), DurationSeconds = 1500 });
        store.Add(new SessionRecord { StartedAt = DateTime.Today, DurationSeconds = 0 }); // ignored

        var reloaded = new StatsStore(path).GetAll();
        Assert.Equal(2, reloaded.Count);
        Assert.Equal(3000, reloaded.Sum(s => s.DurationSeconds));
    }

    [Fact]
    public void Stats_Clear()
    {
        var path = Path.Combine(_root, "sessions.json");
        var store = new StatsStore(path);
        store.Add(new SessionRecord { StartedAt = DateTime.Today, DurationSeconds = 1500 });

        store.Clear();

        Assert.Empty(store.GetAll());
    }
}
