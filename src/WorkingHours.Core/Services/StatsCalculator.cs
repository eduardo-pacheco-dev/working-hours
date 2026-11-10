using WorkingHours.Core.Models;

namespace WorkingHours.Core.Services;

/// <summary>Pure aggregation helpers shared by the statistics UI and the tests.</summary>
public static class StatsCalculator
{
    public static long TotalFocusSeconds(IEnumerable<SessionRecord> sessions) =>
        sessions.Sum(s => (long)Math.Max(0, s.DurationSeconds));

    public static int TotalSessionCount(IEnumerable<SessionRecord> sessions) =>
        sessions.Count();

    public static long FocusSecondsOn(IEnumerable<SessionRecord> sessions, DateOnly date) =>
        sessions.Where(s => s.LocalDate == date).Sum(s => (long)Math.Max(0, s.DurationSeconds));

    public static int SessionCountOn(IEnumerable<SessionRecord> sessions, DateOnly date) =>
        sessions.Count(s => s.LocalDate == date);

    /// <summary>Focus seconds for each of the 24 hours of a day.</summary>
    public static long[] HourlyFocusSeconds(IEnumerable<SessionRecord> sessions, DateOnly date)
    {
        var buckets = new long[24];
        foreach (var session in sessions.Where(s => s.LocalDate == date))
        {
            var hour = Math.Clamp(session.StartedAt.Hour, 0, 23);
            buckets[hour] += Math.Max(0, session.DurationSeconds);
        }
        return buckets;
    }

    /// <summary>Focus seconds for <paramref name="days"/> consecutive days starting at <paramref name="start"/>.</summary>
    public static long[] DailyFocusSeconds(IEnumerable<SessionRecord> sessions, DateOnly start, int days)
    {
        var buckets = new long[Math.Max(0, days)];
        foreach (var session in sessions)
        {
            var index = session.LocalDate.DayNumber - start.DayNumber;
            if (index >= 0 && index < buckets.Length)
                buckets[index] += Math.Max(0, session.DurationSeconds);
        }
        return buckets;
    }

    /// <summary>
    /// Consecutive days with at least one session, ending today (or yesterday when
    /// today has no sessions yet, so the streak is still considered alive).
    /// </summary>
    public static int CurrentStreak(IEnumerable<SessionRecord> sessions, DateOnly today)
    {
        var days = sessions
            .Select(s => s.LocalDate)
            .Where(d => d <= today)
            .Distinct()
            .ToHashSet();

        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    public static int LongestStreak(IEnumerable<SessionRecord> sessions)
    {
        var days = sessions.Select(s => s.LocalDate).Distinct().Order().ToList();
        var best = 0;
        var current = 0;
        DateOnly? previous = null;

        foreach (var day in days)
        {
            current = previous is not null && day == previous.Value.AddDays(1) ? current + 1 : 1;
            best = Math.Max(best, current);
            previous = day;
        }
        return best;
    }

    public static (DateOnly Date, long Seconds) BestDay(IEnumerable<SessionRecord> sessions)
    {
        var best = sessions
            .GroupBy(s => s.LocalDate)
            .Select(g => (Date: g.Key, Seconds: g.Sum(s => (long)Math.Max(0, s.DurationSeconds))))
            .OrderByDescending(x => x.Seconds)
            .ThenBy(x => x.Date)
            .FirstOrDefault();
        return best;
    }
}
