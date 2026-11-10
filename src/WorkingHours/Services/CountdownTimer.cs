using System.Windows.Threading;

namespace WorkingHours.Services;

public enum TimerStatus
{
    Stopped,
    Running,
    Paused
}

/// <summary>
/// Monotonic, deadline-based countdown. Uses the wall clock for accuracy instead
/// of counting timer ticks, so the timer stays correct after sleep or load spikes.
/// </summary>
public sealed class CountdownTimer
{
    private readonly DispatcherTimer _dispatcherTimer;
    private TimeSpan _duration;
    private TimeSpan _remaining;
    private DateTime _deadlineUtc;

    public CountdownTimer()
    {
        _dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _dispatcherTimer.Tick += (_, _) => OnTick();
    }

    public event EventHandler? Tick;
    public event EventHandler? Completed;

    public TimerStatus Status { get; private set; } = TimerStatus.Stopped;
    public TimeSpan Duration => _duration;
    public TimeSpan Remaining => _remaining;
    public bool IsRunning => Status == TimerStatus.Running;
    public bool IsPaused => Status == TimerStatus.Paused;

    public double Progress
    {
        get
        {
            if (_duration <= TimeSpan.Zero)
                return 0;
            return Math.Clamp(1.0 - _remaining.TotalSeconds / _duration.TotalSeconds, 0, 1);
        }
    }

    public void Load(TimeSpan duration, bool startImmediately)
    {
        _dispatcherTimer.Stop();
        _duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        _remaining = _duration;
        Status = TimerStatus.Stopped;

        if (startImmediately && _duration > TimeSpan.Zero)
            Start();
        else
            Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        if (Status == TimerStatus.Running || _duration <= TimeSpan.Zero)
            return;

        if (_remaining <= TimeSpan.Zero)
            _remaining = _duration;

        _deadlineUtc = DateTime.UtcNow + _remaining;
        Status = TimerStatus.Running;
        _dispatcherTimer.Start();
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (Status != TimerStatus.Running)
            return;

        _remaining = Max(_deadlineUtc - DateTime.UtcNow, TimeSpan.Zero);
        Status = TimerStatus.Paused;
        _dispatcherTimer.Stop();
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (Status == TimerStatus.Running)
            Pause();
        else
            Start();
    }

    /// <summary>Returns to the loaded duration without changing it.</summary>
    public void Stop()
    {
        _dispatcherTimer.Stop();
        _remaining = _duration;
        Status = TimerStatus.Stopped;
        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick()
    {
        var remaining = _deadlineUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _dispatcherTimer.Stop();
            _remaining = TimeSpan.Zero;
            Status = TimerStatus.Stopped;
            Tick?.Invoke(this, EventArgs.Empty);
            Completed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _remaining = remaining;
        Tick?.Invoke(this, EventArgs.Empty);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
