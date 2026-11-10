using WorkingHours.Core.Models;
using WorkingHours.Core.Timer;

namespace WorkingHours.Services;

public sealed class RoundChangedEventArgs : EventArgs
{
    public RoundChangedEventArgs(RoundType round, RoundType? previous, int durationSeconds, bool autoStarted)
    {
        Round = round;
        Previous = previous;
        DurationSeconds = durationSeconds;
        AutoStarted = autoStarted;
    }

    public RoundType Round { get; }
    public RoundType? Previous { get; }
    public int DurationSeconds { get; }
    public bool AutoStarted { get; }
}

public sealed class WorkCompletedEventArgs : EventArgs
{
    public WorkCompletedEventArgs(DateTime startedAt, int durationSeconds)
    {
        StartedAt = startedAt;
        DurationSeconds = durationSeconds;
    }

    public DateTime StartedAt { get; }
    public int DurationSeconds { get; }
}

/// <summary>Owns the sequence and the countdown, and keeps them in sync with settings.</summary>
public sealed class PomodoroEngine
{
    private readonly CountdownTimer _timer = new();
    private readonly SequenceState _sequence;
    private AppSettings _settings;
    private DateTime? _workStartedAt;

    public PomodoroEngine(AppSettings settings)
    {
        _settings = settings;
        _sequence = new SequenceState(settings.LongBreakInterval);

        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
        _timer.Completed += (_, _) => OnRoundCompleted();
    }

    public event EventHandler? Tick;
    public event EventHandler? StateChanged;
    public event EventHandler<RoundChangedEventArgs>? RoundChanged;
    public event EventHandler<WorkCompletedEventArgs>? WorkCompleted;

    public RoundType CurrentRound => _sequence.CurrentRound;
    public RoundType? PreviousRound => _sequence.PreviousRound;
    public int WorkRoundNumber => _sequence.WorkRoundNumber;
    public int WorkRoundsTotal => _sequence.WorkRoundsTotal;
    public bool IsRunning => _timer.IsRunning;
    public bool IsPaused => _timer.IsPaused;
    public bool IsIdle => _timer.Status == TimerStatus.Stopped;
    public TimeSpan Remaining => _timer.Remaining;
    public TimeSpan Duration => _timer.Duration;
    public double Progress => _timer.Progress;

    public void Initialize()
    {
        _sequence.UpdateTotal(_settings.LongBreakInterval);
        _timer.Load(TimeSpan.FromSeconds(_sequence.CurrentDurationSeconds(_settings)), false);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _sequence.UpdateTotal(settings.LongBreakInterval);

        if (_timer.Status == TimerStatus.Stopped)
            _timer.Load(TimeSpan.FromSeconds(_sequence.CurrentDurationSeconds(settings)), false);

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        if (CurrentRound == RoundType.Work)
            _workStartedAt ??= DateTime.Now;

        _timer.Start();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        _timer.Pause();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (_timer.IsRunning)
            Pause();
        else
            Start();
    }

    /// <summary>Restarts the current round from the beginning and starts it.</summary>
    public void RestartRound()
    {
        _workStartedAt = CurrentRound == RoundType.Work ? DateTime.Now : null;
        _timer.Load(TimeSpan.FromSeconds(_sequence.CurrentDurationSeconds(_settings)), true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Back to the first focus round, stopped.</summary>
    public void Reset()
    {
        _sequence.Reset();
        _workStartedAt = null;
        _timer.Load(TimeSpan.FromSeconds(_sequence.CurrentDurationSeconds(_settings)), false);
        StateChanged?.Invoke(this, EventArgs.Empty);
        RoundChanged?.Invoke(this, new RoundChangedEventArgs(CurrentRound, null,
            _sequence.CurrentDurationSeconds(_settings), false));
    }

    /// <summary>Advances without recording a session.</summary>
    public void Skip()
    {
        _workStartedAt = null;
        Advance();
    }

    private void OnRoundCompleted()
    {
        if (CurrentRound == RoundType.Work)
        {
            var startedAt = _workStartedAt ?? DateTime.Now.AddSeconds(-_sequence.CurrentDurationSeconds(_settings));
            WorkCompleted?.Invoke(this, new WorkCompletedEventArgs(startedAt, _sequence.CurrentDurationSeconds(_settings)));
        }

        _workStartedAt = null;
        Advance();
    }

    private void Advance()
    {
        var previous = CurrentRound;
        var (round, duration) = _sequence.Advance(_settings);
        var autoStart = round == RoundType.Work ? _settings.AutoStartWork : _settings.AutoStartBreak;

        _timer.Load(TimeSpan.FromSeconds(duration), autoStart);
        if (autoStart && round == RoundType.Work)
            _workStartedAt = DateTime.Now;

        StateChanged?.Invoke(this, EventArgs.Empty);
        RoundChanged?.Invoke(this, new RoundChangedEventArgs(round, previous, duration, autoStart));
    }
}
