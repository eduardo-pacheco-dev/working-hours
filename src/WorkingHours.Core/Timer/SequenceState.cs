using WorkingHours.Core.Models;

namespace WorkingHours.Core.Timer;

/// <summary>
/// Pomodoro round sequencing:
/// work → short break → work → … → long break → work (cycle).
/// </summary>
public sealed class SequenceState
{
    public RoundType CurrentRound { get; private set; } = RoundType.Work;
    public RoundType? PreviousRound { get; private set; }

    /// <summary>Current work round (1-based).</summary>
    public int WorkRoundNumber { get; private set; } = 1;

    /// <summary>Number of work rounds before a long break.</summary>
    public int WorkRoundsTotal { get; private set; }

    /// <summary>Monotonic count of work rounds since the last reset.</summary>
    public int SessionWorkCount { get; private set; } = 1;

    public SequenceState(int workRoundsTotal)
    {
        WorkRoundsTotal = Math.Max(1, workRoundsTotal);
    }

    public void UpdateTotal(int workRoundsTotal) =>
        WorkRoundsTotal = Math.Max(1, workRoundsTotal);

    public int DurationFor(RoundType round, AppSettings settings) => round switch
    {
        RoundType.Work => settings.TimeWorkSeconds,
        RoundType.ShortBreak => settings.TimeShortBreakSeconds,
        RoundType.LongBreak => settings.TimeLongBreakSeconds,
        _ => settings.TimeWorkSeconds
    };

    public int CurrentDurationSeconds(AppSettings settings) =>
        DurationFor(CurrentRound, settings);

    /// <summary>Advances to the next round and returns it together with its duration.</summary>
    public (RoundType Round, int DurationSeconds) Advance(AppSettings settings)
    {
        PreviousRound = CurrentRound;

        switch (CurrentRound)
        {
            case RoundType.Work:
                if (WorkRoundNumber >= WorkRoundsTotal)
                {
                    if (settings.LongBreaksEnabled)
                    {
                        CurrentRound = RoundType.LongBreak;
                    }
                    else if (settings.ShortBreaksEnabled)
                    {
                        WorkRoundNumber = 0; // short break → work increments back to 1
                        CurrentRound = RoundType.ShortBreak;
                    }
                    else
                    {
                        WorkRoundNumber = 1;
                        CurrentRound = RoundType.Work;
                    }
                }
                else if (settings.ShortBreaksEnabled)
                {
                    CurrentRound = RoundType.ShortBreak;
                }
                else
                {
                    WorkRoundNumber++;
                    CurrentRound = RoundType.Work;
                }
                break;

            case RoundType.ShortBreak:
                WorkRoundNumber++;
                CurrentRound = RoundType.Work;
                break;

            case RoundType.LongBreak:
                WorkRoundNumber = 1;
                CurrentRound = RoundType.Work;
                break;
        }

        if (CurrentRound == RoundType.Work)
            SessionWorkCount++;

        return (CurrentRound, CurrentDurationSeconds(settings));
    }

    public void Reset()
    {
        CurrentRound = RoundType.Work;
        PreviousRound = null;
        WorkRoundNumber = 1;
        SessionWorkCount = 1;
    }
}
