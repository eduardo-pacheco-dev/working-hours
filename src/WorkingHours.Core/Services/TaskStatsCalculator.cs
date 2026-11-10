using WorkingHours.Core.Models;

namespace WorkingHours.Core.Services;

public sealed record TaskStatsSummary(
    int TotalTasks,
    int CompletedTasks,
    int PendingTasks,
    long TotalTrackedSeconds,
    long AverageCompletedSeconds,
    TodoItem? MostTimeTask);

/// <summary>Pure aggregations used by the task statistics view.</summary>
public static class TaskStatsCalculator
{
    public static TaskStatsSummary Calculate(IReadOnlyCollection<TodoItem> items)
    {
        var completed = items.Count(i => i.IsDone);
        var totalSeconds = items.Sum(i => (long)Math.Max(0, i.TimeSpentSeconds));
        var completedSeconds = items
            .Where(i => i.IsDone)
            .Sum(i => (long)Math.Max(0, i.TimeSpentSeconds));

        var mostTime = items
            .Where(i => i.TimeSpentSeconds > 0)
            .OrderByDescending(i => i.TimeSpentSeconds)
            .ThenBy(i => i.CreatedAt)
            .FirstOrDefault();

        return new TaskStatsSummary(
            items.Count,
            completed,
            items.Count - completed,
            totalSeconds,
            completed > 0 ? completedSeconds / completed : 0,
            mostTime);
    }
}
