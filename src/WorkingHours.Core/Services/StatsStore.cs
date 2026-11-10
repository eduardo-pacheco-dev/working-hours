using System.Text.Json;
using WorkingHours.Core.Models;

namespace WorkingHours.Core.Services;

/// <summary>Persists completed focus sessions as a small JSON log.</summary>
public sealed class StatsStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public StatsStore(AppPaths paths) : this(paths.SessionsFile) { }

    public StatsStore(string sessionsPath)
    {
        _path = sessionsPath;
    }

    public IReadOnlyList<SessionRecord> GetAll()
    {
        lock (_gate)
        {
            return ReadLocked();
        }
    }

    public void Add(SessionRecord record)
    {
        if (record.DurationSeconds <= 0)
            return;

        lock (_gate)
        {
            var sessions = ReadLocked().ToList();
            sessions.Add(record);
            WriteLocked(sessions);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            WriteLocked(new List<SessionRecord>());
        }
    }

    private List<SessionRecord> ReadLocked()
    {
        try
        {
            if (!File.Exists(_path))
                return new List<SessionRecord>();

            var json = File.ReadAllText(_path);
            var log = JsonSerializer.Deserialize<SessionLog>(json, JsonDefaults.Options);
            return log?.Sessions ?? new List<SessionRecord>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new List<SessionRecord>();
        }
    }

    private void WriteLocked(List<SessionRecord> sessions)
    {
        var log = new SessionLog { Sessions = sessions };
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(log, JsonDefaults.Options));
    }

    private sealed class SessionLog
    {
        public int Version { get; set; } = 1;
        public List<SessionRecord> Sessions { get; set; } = new();
    }
}
