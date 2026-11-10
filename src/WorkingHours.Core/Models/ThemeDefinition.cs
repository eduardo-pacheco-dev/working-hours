using System.Text.Json.Serialization;

namespace WorkingHours.Core.Models;

/// <summary>
/// A theme in the same JSON shape used by Pomotroid:
/// <c>{ "name": "...", "colors": { "--color-background": "#..." } }</c>
/// </summary>
public sealed class ThemeDefinition
{
    public string Name { get; set; } = "Untitled";

    public Dictionary<string, string> Colors { get; set; } = new();

    [JsonIgnore]
    public bool IsCustom { get; set; }

    [JsonIgnore]
    public string? FilePath { get; set; }

    [JsonIgnore]
    public bool IsLight { get; set; }

    public string? GetColor(string key) =>
        Colors.TryGetValue(key, out var value) ? value : null;

    public ThemeDefinition Clone() => new()
    {
        Name = Name,
        Colors = new Dictionary<string, string>(Colors),
        IsCustom = IsCustom,
        FilePath = FilePath,
        IsLight = IsLight
    };
}
