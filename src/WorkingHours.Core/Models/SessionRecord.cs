using System.Text.Json.Serialization;

namespace WorkingHours.Core.Models;

/// <summary>A completed focus (work) round, used by the statistics views.</summary>
public sealed class SessionRecord
{
    /// <summary>Local start time of the round.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Planned duration of the round, in seconds.</summary>
    public int DurationSeconds { get; set; }

    [JsonIgnore]
    public DateOnly LocalDate => DateOnly.FromDateTime(StartedAt);
}
