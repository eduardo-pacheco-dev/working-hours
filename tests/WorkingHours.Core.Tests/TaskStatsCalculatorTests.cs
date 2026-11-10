using WorkingHours.Core.Models;
using WorkingHours.Core.Services;

namespace WorkingHours.Core.Tests;

public class TaskStatsCalculatorTests
{
    private static TodoItem Task(string text, bool done, int seconds, int createdHour = 9) => new()
    {
        Text = text,
        IsDone = done,
        TimeSpentSeconds = seconds,
        CreatedAt = DateTime.Today.AddHours(createdHour)
    };

    [Fact]
    public void Calculate_CountsAndTotals()
    {
        var items = new[]
        {
            Task("A", true, 3600),
            Task("B", false, 1800),
            Task("C", true, 1200),
            Task("D", false, 0)
        };

        var summary = TaskStatsCalculator.Calculate(items);

        Assert.Equal(4, summary.TotalTasks);
        Assert.Equal(2, summary.CompletedTasks);
        Assert.Equal(2, summary.PendingTasks);
        Assert.Equal(6600, summary.TotalTrackedSeconds);
        Assert.Equal(2400, summary.AverageCompletedSeconds);
        Assert.Equal("A", summary.MostTimeTask?.Text);
    }

    [Fact]
    public void Calculate_NoCompletedTasks_HasZeroAverage()
    {
        var summary = TaskStatsCalculator.Calculate(new[]
        {
            Task("A", false, 60),
            Task("B", false, 120)
        });

        Assert.Equal(0, summary.AverageCompletedSeconds);
        Assert.Equal("B", summary.MostTimeTask?.Text);
    }

    [Fact]
    public void Calculate_NoTrackedTime_ReturnsNoHighlight()
    {
        var summary = TaskStatsCalculator.Calculate(new[]
        {
            Task("A", false, 0),
            Task("B", true, 0)
        });

        Assert.Equal(0, summary.TotalTrackedSeconds);
        Assert.Null(summary.MostTimeTask);
    }

    [Fact]
    public void Calculate_EmptyList()
    {
        var summary = TaskStatsCalculator.Calculate(Array.Empty<TodoItem>());

        Assert.Equal(0, summary.TotalTasks);
        Assert.Equal(0, summary.CompletedTasks);
        Assert.Equal(0, summary.PendingTasks);
        Assert.Null(summary.MostTimeTask);
    }
}
