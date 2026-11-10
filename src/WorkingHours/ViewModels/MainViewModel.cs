using System.Windows.Input;
using WorkingHours.Core.Models;
using WorkingHours.Core.Services;
using WorkingHours.Infrastructure;
using WorkingHours.Services;

namespace WorkingHours.ViewModels;

public sealed record RoundDot(int Number, bool IsActive, bool IsComplete);

public sealed class MainViewModel : ObservableObject
{
    private readonly SettingsManager _settingsManager;
    private readonly StatsStore _stats;
    private readonly AudioService _audio;
    private readonly TrayService _tray;
    private readonly NotificationService _notifications;
    private readonly LocalizationService _loc;
    private readonly PomodoroEngine _engine;

    private AppSettings _settings;
    private string _timeText = "25:00";
    private string _statusText = string.Empty;
    private string _roundLabel = string.Empty;
    private string _roundDetail = string.Empty;
    private string _windowTitle = "Working Hours";
    private string _playPauseTooltip = string.Empty;
    private string _muteTooltip = string.Empty;
    private double _progress;
    private bool _isRunning;
    private bool _isPaused;
    private bool _isPinned;
    private bool _isMuted;
    private bool _isCompact;
    private RoundType _round = RoundType.Work;
    private IReadOnlyList<RoundDot> _roundDots = Array.Empty<RoundDot>();
    private int _lastDisplayedSecond = -1;

    public MainViewModel(
        SettingsManager settingsManager,
        StatsStore stats,
        AudioService audio,
        TrayService tray,
        NotificationService notifications,
        LocalizationService loc)
    {
        _settingsManager = settingsManager;
        _settings = settingsManager.Current;
        _stats = stats;
        _audio = audio;
        _tray = tray;
        _notifications = notifications;
        _loc = loc;

        _engine = new PomodoroEngine(_settings);
        _engine.Tick += (_, _) => OnTick();
        _engine.StateChanged += (_, _) => UpdateTimerState();
        _engine.RoundChanged += OnRoundChanged;
        _engine.WorkCompleted += OnWorkCompleted;

        ToggleCommand = new RelayCommand(() => _engine.Toggle());
        ResetCommand = new RelayCommand(() => _engine.Reset());
        SkipCommand = new RelayCommand(() => _engine.Skip());
        RestartCommand = new RelayCommand(() => _engine.RestartRound());
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);

        _tray.ToggleRequested += (_, _) => _engine.Toggle();
        _tray.ResetRequested += (_, _) => _engine.Reset();
        _tray.SkipRequested += (_, _) => _engine.Skip();
        _loc.LanguageChanged += OnLanguageChanged;

        _engine.Initialize();
        ApplySettings(_settings);
        UpdateRoundProperties();
        UpdateTimerState();
        UpdateTray(force: true);
    }

    public event EventHandler? AttentionRequested;

    public ICommand ToggleCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand TogglePinCommand { get; }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string RoundLabel
    {
        get => _roundLabel;
        private set => SetProperty(ref _roundLabel, value);
    }

    public string RoundDetail
    {
        get => _roundDetail;
        private set => SetProperty(ref _roundDetail, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    public string PlayPauseTooltip
    {
        get => _playPauseTooltip;
        private set => SetProperty(ref _playPauseTooltip, value);
    }

    public string MuteTooltip
    {
        get => _muteTooltip;
        private set => SetProperty(ref _muteTooltip, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public bool IsCompact
    {
        get => _isCompact;
        set => SetProperty(ref _isCompact, value);
    }

    public RoundType Round
    {
        get => _round;
        private set => SetProperty(ref _round, value);
    }

    public IReadOnlyList<RoundDot> RoundDots
    {
        get => _roundDots;
        private set => SetProperty(ref _roundDots, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (!SetProperty(ref _isPinned, value))
                return;

            _settings.AlwaysOnTop = value;
            _settingsManager.SaveWithoutNotify();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set => SetProperty(ref _isMuted, value);
    }

    public bool DialCountdown => _settings.DialCountdown;

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _engine.ApplySettings(settings);

        _audio.Volume = settings.Volume;
        _audio.IsMuted = settings.IsMuted;
        _notifications.Enabled = settings.NotificationsEnabled;

        _isPinned = settings.AlwaysOnTop;
        _isMuted = settings.IsMuted;
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(DialCountdown));

        UpdateRoundProperties();
        UpdateTimerState();
        UpdateTray(force: true);
    }

    private void OnTick()
    {
        var remaining = _engine.Remaining;
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));

        TimeText = $"{seconds / 60:00}:{seconds % 60:00}";
        Progress = _engine.Progress;
        IsRunning = _engine.IsRunning;
        IsPaused = _engine.IsPaused;
        WindowTitle = _isRunning || _isPaused
            ? $"{TimeText} · {RoundLabel} — {_loc["AppName"]}"
            : $"{_loc["AppName"]} — {RoundLabel}";

        if (seconds == _lastDisplayedSecond)
            return;

        _lastDisplayedSecond = seconds;
        UpdateTray();

        if (_isRunning && seconds > 0)
        {
            var tickEnabled = _round == RoundType.Work
                ? _settings.TickSoundsDuringWork
                : _settings.TickSoundsDuringBreak;
            if (tickEnabled)
                _audio.PlayTick();
        }
    }

    private void UpdateTimerState()
    {
        IsRunning = _engine.IsRunning;
        IsPaused = _engine.IsPaused;
        Round = _engine.CurrentRound;

        var remaining = _engine.Remaining;
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        TimeText = $"{seconds / 60:00}:{seconds % 60:00}";
        Progress = _engine.Progress;

        StatusText = _isPaused ? _loc["Paused"] : _isRunning ? _loc["InProgress"] : _loc["Ready"];
        PlayPauseTooltip = _isRunning ? _loc["Pause"] : _isPaused ? _loc["Resume"] : _loc["Start"];
        WindowTitle = _isRunning || _isPaused
            ? $"{TimeText} · {RoundLabel} — {_loc["AppName"]}"
            : $"{_loc["AppName"]} — {RoundLabel}";

        UpdateTray();
    }

    private void OnRoundChanged(object? sender, RoundChangedEventArgs e)
    {
        UpdateRoundProperties();
        UpdateTimerState();
        UpdateTray(force: true);

        // A reset also raises this event (with no previous round), which is not a completion.
        if (e.Previous is not { } previous)
            return;

        string title;
        string body;

        switch (previous)
        {
            case RoundType.Work:
                _audio.PlayWorkComplete();
                title = _loc["FocusCompleteTitle"];
                body = _loc["FocusCompleteBody"];
                break;
            case RoundType.ShortBreak:
                _audio.PlayBreakComplete();
                title = _loc["ShortBreakCompleteTitle"];
                body = _loc["ShortBreakCompleteBody"];
                break;
            default:
                _audio.PlayBreakComplete();
                title = _loc["LongBreakCompleteTitle"];
                body = _loc["LongBreakCompleteBody"];
                break;
        }

        _notifications.Show(title, body);
        AttentionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkCompleted(object? sender, WorkCompletedEventArgs e)
    {
        _stats.Add(new SessionRecord
        {
            StartedAt = e.StartedAt,
            DurationSeconds = e.DurationSeconds
        });
    }

    private void UpdateRoundProperties()
    {
        Round = _engine.CurrentRound;
        RoundLabel = Round switch
        {
            RoundType.ShortBreak => _loc["ShortBreak"],
            RoundType.LongBreak => _loc["LongBreak"],
            _ => _loc["Focus"]
        };
        RoundDetail = _loc.Format("RoundOf", _engine.WorkRoundNumber, _engine.WorkRoundsTotal);

        var total = Math.Max(1, _engine.WorkRoundsTotal);
        var current = _engine.WorkRoundNumber;
        RoundDots = Enumerable.Range(1, total)
            .Select(n => new RoundDot(n, n == current, n < current))
            .ToList();

        MuteTooltip = _isMuted ? _loc["Unmute"] : _loc["Mute"];
    }

    private void UpdateTray(bool force = false)
    {
        if (!force && !_settings.TrayIconEnabled && !_tray.IsVisible)
            return;

        var tooltip = $"{_loc["AppName"]} · {TimeText} · {RoundLabel}";
        var toggleText = _isRunning ? _loc["Pause"] : _isPaused ? _loc["Resume"] : _loc["Start"];

        _tray.Update(
            _settings.TrayIconEnabled,
            tooltip,
            _engine.CurrentRound,
            _engine.Progress,
            _isRunning,
            _isPaused,
            toggleText,
            _loc["Reset"],
            _loc["Skip"],
            _loc["ShowWindow"],
            _loc["Exit"]);
    }

    private void ToggleMute()
    {
        _settings.IsMuted = !_settings.IsMuted;
        IsMuted = _settings.IsMuted;
        _audio.IsMuted = _settings.IsMuted;
        _settingsManager.SaveWithoutNotify();
        MuteTooltip = _isMuted ? _loc["Unmute"] : _loc["Mute"];
        UpdateTray(force: true);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateRoundProperties();
        UpdateTimerState();
        UpdateTray(force: true);
    }
}
