using System.Text.Json;
using WorkingHours.Core.Models;

namespace WorkingHours.Core.Services;

/// <summary>Persists the task list as a small JSON log next to the other app data.</summary>
public sealed class TodoStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public TodoStore(AppPaths paths) : this(paths.TodosFile) { }

    public TodoStore(string todosPath)
    {
        _path = todosPath;
    }

    public IReadOnlyList<TodoItem> GetAll()
    {
        lock (_gate)
        {
            return ReadLocked().ToList();
        }
    }

    /// <summary>Persists a manual order; any task missing from the list stays at the end.</summary>
    public void Reorder(IReadOnlyList<Guid> orderedIds)
    {
        lock (_gate)
        {
            var items = ReadLocked();
            var remaining = items.ToDictionary(t => t.Id);
            var reordered = new List<TodoItem>(items.Count);

            foreach (var id in orderedIds)
            {
                if (remaining.Remove(id, out var item))
                    reordered.Add(item);
            }

            reordered.AddRange(items.Where(t => remaining.ContainsKey(t.Id)));
            WriteLocked(reordered);
        }
    }

    /// <summary>Adds a task. Returns <c>null</c> when the text is blank.</summary>
    public TodoItem? Add(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;

        lock (_gate)
        {
            var items = ReadLocked();
            var item = new TodoItem
            {
                Id = Guid.NewGuid(),
                Text = trimmed,
                CreatedAt = DateTime.Now
            };
            items.Add(item);
            WriteLocked(items);
            return item;
        }
    }

    public void Update(TodoItem item)
    {
        lock (_gate)
        {
            var items = ReadLocked();
            var index = items.FindIndex(t => t.Id == item.Id);
            if (index < 0)
                return;

            items[index].Text = item.Text;
            items[index].IsDone = item.IsDone;
            items[index].TimeSpentSeconds = item.TimeSpentSeconds;
            items[index].TimerStartedAt = item.TimerStartedAt;
            items[index].CompletedAt = item.CompletedAt;
            WriteLocked(items);
        }
    }

    public void Delete(Guid id)
    {
        lock (_gate)
        {
            var items = ReadLocked();
            if (items.RemoveAll(t => t.Id == id) > 0)
                WriteLocked(items);
        }
    }

    public void ClearCompleted()
    {
        lock (_gate)
        {
            var items = ReadLocked();
            if (items.RemoveAll(t => t.IsDone) > 0)
                WriteLocked(items);
        }
    }

    private List<TodoItem> ReadLocked()
    {
        try
        {
            if (!File.Exists(_path))
                return new List<TodoItem>();

            var json = File.ReadAllText(_path);
            var log = JsonSerializer.Deserialize<TodoLog>(json, JsonDefaults.Options);
            return log?.Items ?? new List<TodoItem>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new List<TodoItem>();
        }
    }

    private void WriteLocked(List<TodoItem> items)
    {
        var log = new TodoLog { Items = items };
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(log, JsonDefaults.Options));
    }

    private sealed class TodoLog
    {
        public int Version { get; set; } = 1;
        public List<TodoItem> Items { get; set; } = new();
    }
}
