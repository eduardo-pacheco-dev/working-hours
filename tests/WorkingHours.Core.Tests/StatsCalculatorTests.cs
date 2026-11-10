using WorkingHours.Core.Models;
using WorkingHours.Core.Services;

namespace WorkingHours.Core.Tests;

public class StatsCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static SessionRecord Session(DateOnly date, int hour, int minutes = 25) => new()
    {
        StartedAt = date.ToDateTime(new TimeOnly(hour, 30)),
        DurationSeconds = minutes * 60
    };

    private static List<SessionRecord> Sample() => new()
    {
        Session(Today, 9),
        Session(Today, 9),
        Session(Today, 14, 50),
        Session(Today.AddDays(-1), 10),
        Session(Today.AddDays(-2), 8),
        Session(Today.AddDays(-5), 16),
        Session(Today.AddDays(-30), 11)
    };

    [Fact]
    public void TotalFocus_AndSessionCount()
    {
        var sessions = Sample();
        Assert.Equal((25 + 25 + 50 + 25 + 25 + 25 + 25) * 60, StatsCalculator.TotalFocusSeconds(sessions));
        Assert.Equal(7, StatsCalculator.TotalSessionCount(sessions));
    }

    [Fact]
    public void HourlyFocus_BucketsByStartHour()
    {
        var hourly = StatsCalculator.HourlyFocusSeconds(Sample(), Today);
        Assert.Equal(50 * 60, hourly[9]);
        Assert.Equal(50 * 60, hourly[14]);
        Assert.Equal(0, hourly[10]);
    }

    [Fact]
    public void DailyFocus_UsesInclusiveStartAndDayCount()
    {
        var buckets = StatsCalculator.DailyFocusSeconds(Sample(), Today.AddDays(-2), 3);
        Assert.Equal(3, buckets.Length);
        Assert.Equal(25 * 60, buckets[0]); // -2 days
        Assert.Equal(25 * 60, buckets[1]); // -1 day
        Assert.Equal(100 * 60, buckets[2]);
    }

    [Fact]
    public void CurrentStreak_CountsConsecutiveDaysEndingToday()
    {
        Assert.Equal(3, StatsCalculator.CurrentStreak(Sample(), Today));
    }

    [Fact]
    public void CurrentStreak_StaysAliveWhenOnlyYesterdayHasData()
    {
        var sessions = new List<SessionRecord> { Session(Today.AddDays(-1), 9), Session(Today.AddDays(-2), 9) };
        Assert.Equal(2, StatsCalculator.CurrentStreak(sessions, Today));
    }

    [Fact]
    public void LongestStreak_FindsBestRun()
    {
        var sessions = new List<SessionRecord>
        {
            Session(Today, 9), Session(Today.AddDays(-1), 9), Session(Today.AddDays(-2), 9),
            Session(Today.AddDays(-10), 9), Session(Today.AddDays(-11), 9)
        };
        Assert.Equal(3, StatsCalculator.LongestStreak(sessions));
    }

    [Fact]
    public void BestDay_ReturnsHighestTotal()
    {
        var (date, seconds) = StatsCalculator.BestDay(Sample());
        Assert.Equal(Today, date);
        Assert.Equal(100 * 60, seconds);
    }

    [Fact]
    public void EmptyData_IsHandled()
    {
        var empty = Array.Empty<SessionRecord>();
        Assert.Equal(0, StatsCalculator.TotalFocusSeconds(empty));
        Assert.Equal(0, StatsCalculator.CurrentStreak(empty, Today));
        Assert.Equal(0, StatsCalculator.LongestStreak(empty));
        Assert.Equal(0, StatsCalculator.BestDay(empty).Seconds);
    }
}
