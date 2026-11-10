using System.Text.Json;
using WorkingHours.Core.Models;

namespace WorkingHours.Core.Services;

/// <summary>Loads and persists <see cref="AppSettings"/> as a JSON document.</summary>
public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(AppPaths paths) : this(paths.SettingsFile) { }

    public SettingsStore(string settingsPath)
    {
        _path = settingsPath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)
                           ?? new AppSettings();
            settings.Sanitize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Sanitize();
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonDefaults.Options));
    }
}

internal static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
