namespace WorkingHours.Core.Models;

/// <summary>A single task in the slide-out task list.</summary>
public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Accumulated tracked time, in seconds.</summary>
    public int TimeSpentSeconds { get; set; }

    /// <summary>When the task stopwatch is running; null when paused.</summary>
    public DateTime? TimerStartedAt { get; set; }

    /// <summary>When the task was marked as done.</summary>
    public DateTime? CompletedAt { get; set; }
}
