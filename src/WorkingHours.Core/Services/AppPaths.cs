namespace WorkingHours.Core.Services;

/// <summary>Well-known per-user locations for the application data.</summary>
public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkingHours");
    }

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string SessionsFile => Path.Combine(Root, "sessions.json");
    public string TodosFile => Path.Combine(Root, "todos.json");
    public string ThemesDirectory => Path.Combine(Root, "themes");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ThemesDirectory);
    }
}
