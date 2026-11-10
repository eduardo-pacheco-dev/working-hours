using WorkingHours.Core.Models;
using WorkingHours.Core.Timer;

namespace WorkingHours.Core.Tests;

public class SequenceStateTests
{
    private static AppSettings Settings(
        int rounds = 4,
        bool shortBreaks = true,
        bool longBreaks = true) => new()
    {
        TimeWorkSeconds = 1500,
        TimeShortBreakSeconds = 300,
        TimeLongBreakSeconds = 900,
        LongBreakInterval = rounds,
        ShortBreaksEnabled = shortBreaks,
        LongBreaksEnabled = longBreaks
    };

    private static List<RoundType> Types(int rounds, int cycles, AppSettings? settings = null)
    {
        var s = settings ?? Settings(rounds);
        var sequence = new SequenceState(rounds);
        var types = new List<RoundType> { sequence.CurrentRound };

        for (var i = 0; i < cycles * rounds * 2; i++)
            types.Add(sequence.Advance(s).Round);

        return types;
    }

    [Fact]
    public void FourRounds_CycleMatchesDefaultPomodoro()
    {
        var settings = Settings();
        var sequence = new SequenceState(4);

        var expected = new[]
        {
            (RoundType.ShortBreak, 300),
            (RoundType.Work, 1500),
            (RoundType.ShortBreak, 300),
            (RoundType.Work, 1500),
            (RoundType.ShortBreak, 300),
            (RoundType.Work, 1500),
            (RoundType.LongBreak, 900),
            (RoundType.Work, 1500),
            (RoundType.ShortBreak, 300)
        };

        foreach (var (round, duration) in expected)
        {
            var next = sequence.Advance(settings);
            Assert.Equal(round, next.Round);
            Assert.Equal(duration, next.DurationSeconds);
        }
    }

    [Fact]
    public void SingleRound_CycleAlternatesWorkAndLongBreak()
    {
        var types = Types(1, 3);
        Assert.Equal(new[]
        {
            RoundType.Work, RoundType.LongBreak, RoundType.Work, RoundType.LongBreak,
            RoundType.Work, RoundType.LongBreak, RoundType.Work
        }, types);
    }

    [Fact]
    public void WorkRoundCounter_ResetsAfterLongBreak()
    {
        var settings = Settings();
        var sequence = new SequenceState(2);

        sequence.Advance(settings);                 // short break
        sequence.Advance(settings);                 // work 2
        Assert.Equal(2, sequence.WorkRoundNumber);

        sequence.Advance(settings);                 // long break
        Assert.Equal(2, sequence.WorkRoundNumber);

        sequence.Advance(settings);                 // work 1
        Assert.Equal(1, sequence.WorkRoundNumber);
    }

    [Fact]
    public void ShortBreaksDisabled_ChainsWorkRoundsButKeepsLongBreak()
    {
        var settings = Settings(rounds: 4, shortBreaks: false);
        var sequence = new SequenceState(4);

        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(2, sequence.WorkRoundNumber);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(RoundType.LongBreak, sequence.Advance(settings).Round);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(1, sequence.WorkRoundNumber);
    }

    [Fact]
    public void LongBreaksDisabled_SubstitutesShortBreak()
    {
        var settings = Settings(rounds: 2, longBreaks: false);
        var sequence = new SequenceState(2);

        Assert.Equal(RoundType.ShortBreak, sequence.Advance(settings).Round);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(RoundType.ShortBreak, sequence.Advance(settings).Round);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(1, sequence.WorkRoundNumber);
    }

    [Fact]
    public void BothBreaksDisabled_LoopsWorkRounds()
    {
        var settings = Settings(rounds: 3, shortBreaks: false, longBreaks: false);
        var sequence = new SequenceState(3);

        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(2, sequence.WorkRoundNumber);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(3, sequence.WorkRoundNumber);
        Assert.Equal(RoundType.Work, sequence.Advance(settings).Round);
        Assert.Equal(1, sequence.WorkRoundNumber);
    }

    [Fact]
    public void Reset_ReturnsToFirstWorkRound()
    {
        var settings = Settings();
        var sequence = new SequenceState(4);

        sequence.Advance(settings);
        sequence.Advance(settings);
        sequence.Reset();

        Assert.Equal(RoundType.Work, sequence.CurrentRound);
        Assert.Equal(1, sequence.WorkRoundNumber);
        Assert.Equal(1500, sequence.CurrentDurationSeconds(settings));
    }
}
