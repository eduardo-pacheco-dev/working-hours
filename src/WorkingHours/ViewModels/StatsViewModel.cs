using System.Globalization;
using System.Windows.Input;
using WorkingHours.Core.Models;
using WorkingHours.Core.Services;
using WorkingHours.Infrastructure;
using WorkingHours.Services;

namespace WorkingHours.ViewModels;

public enum StatsRange
{
    Daily,
    Weekly,
    AllTime
}

public sealed class StatsViewModel : ObservableObject, IDisposable
{
    private readonly StatsStore _store;
    private readonly LocalizationService _loc;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    private DateOnly _dailyDate;
    private DateOnly _weekStart;
    private StatsRange _range = StatsRange.Daily;

    private string _periodLabel = string.Empty;
    private string _focusTimeText = "0m";
    private string _sessionsText = "0";
    private string _streakText = string.Empty;
    private string _bestDayText = string.Empty;
    private IReadOnlyList<double> _chartValues = Array.Empty<double>();
    private IReadOnlyList<string> _chartLabels = Array.Empty<string>();
    private IReadOnlyDictionary<DateOnly, double> _heatmapValues = new Dictionary<DateOnly, double>();
    private bool _hasChartData;

    public StatsViewModel(StatsStore store, LocalizationService loc)
    {
        _store = store;
        _loc = loc;
        _dailyDate = _today;
        _weekStart = StartOfWeek(_today);

        PreviousCommand = new RelayCommand(GoPrevious);
        NextCommand = new RelayCommand(GoNext, CanGoNext);

        _loc.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    public StatsRange Range
    {
        get => _range;
        set
        {
            if (!SetProperty(ref _range, value))
                return;

            OnPropertyChanged(nameof(IsDaily));
            OnPropertyChanged(nameof(IsWeekly));
            OnPropertyChanged(nameof(IsAllTime));
            Refresh();
        }
    }

    public bool IsDaily
    {
        get => _range == StatsRange.Daily;
        set { if (value) Range = StatsRange.Daily; }
    }

    public bool IsWeekly
    {
        get => _range == StatsRange.Weekly;
        set { if (value) Range = StatsRange.Weekly; }
    }

    public bool IsAllTime
    {
        get => _range == StatsRange.AllTime;
        set { if (value) Range = StatsRange.AllTime; }
    }

    public string PeriodLabel
    {
        get => _periodLabel;
        private set => SetProperty(ref _periodLabel, value);
    }

    public string FocusTimeText
    {
        get => _focusTimeText;
        private set => SetProperty(ref _focusTimeText, value);
    }

    public string SessionsText
    {
        get => _sessionsText;
        private set => SetProperty(ref _sessionsText, value);
    }

    public string StreakText
    {
        get => _streakText;
        private set => SetProperty(ref _streakText, value);
    }

    public string BestDayText
    {
        get => _bestDayText;
        private set => SetProperty(ref _bestDayText, value);
    }

    public IReadOnlyList<double> ChartValues
    {
        get => _chartValues;
        private set => SetProperty(ref _chartValues, value);
    }

    public IReadOnlyList<string> ChartLabels
    {
        get => _chartLabels;
        private set => SetProperty(ref _chartLabels, value);
    }

    public IReadOnlyDictionary<DateOnly, double> HeatmapValues
    {
        get => _heatmapValues;
        private set => SetProperty(ref _heatmapValues, value);
    }

    public bool HasChartData
    {
        get => _hasChartData;
        private set => SetProperty(ref _hasChartData, value);
    }

    public void Refresh()
    {
        var sessions = _store.GetAll();
        var culture = _loc.Culture;

        var currentStreak = StatsCalculator.CurrentStreak(sessions, _today);
        StreakText = $"{currentStreak} {_loc["Days"]}";

        var best = StatsCalculator.BestDay(sessions);
        BestDayText = best.Seconds <= 0
            ? _loc["NoData"]
            : $"{best.Date.ToString("d MMM", culture)} · {FormatDuration(best.Seconds)}";

        switch (_range)
        {
            case StatsRange.Daily:
                BuildDaily(sessions, culture);
                break;
            case StatsRange.Weekly:
                BuildWeekly(sessions, culture);
                break;
            default:
                BuildAllTime(sessions, culture);
                break;
        }

        (PreviousCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void BuildDaily(IReadOnlyList<SessionRecord> sessions, CultureInfo culture)
    {
        PeriodLabel = _dailyDate == _today
            ? _loc["Today"]
            : _dailyDate.ToString("ddd, d MMM", culture);

        var buckets = StatsCalculator.HourlyFocusSeconds(sessions, _dailyDate);
        var values = buckets.Select(b => b / 60.0).ToArray();
        var labels = Enumerable.Range(0, 24)
            .Select(h => h % 3 == 0 ? h.ToString("00", CultureInfo.InvariantCulture) : string.Empty)
            .ToArray();

        ChartValues = values;
        ChartLabels = labels;
        HasChartData = values.Any(v => v > 0);
        FocusTimeText = FormatDuration(buckets.Sum());
        SessionsText = StatsCalculator.SessionCountOn(sessions, _dailyDate).ToString(culture);
    }

    private void BuildWeekly(IReadOnlyList<SessionRecord> sessions, CultureInfo culture)
    {
        var end = _weekStart.AddDays(6);
        PeriodLabel = $"{_weekStart.ToString("d MMM", culture)} – {end.ToString("d MMM", culture)}";

        var buckets = StatsCalculator.DailyFocusSeconds(sessions, _weekStart, 7);
        var values = buckets.Select(b => b / 60.0).ToArray();
        ChartValues = values;

        ChartLabels = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = _weekStart.AddDays(offset).ToDateTime(TimeOnly.MinValue);
                var name = culture.DateTimeFormat.AbbreviatedDayNames[(int)day.DayOfWeek].TrimEnd('.');
                return name.Length > 3 ? name[..3] : name;
            })
            .ToArray();

        HasChartData = values.Any(v => v > 0);
        FocusTimeText = FormatDuration(buckets.Sum());
        SessionsText = sessions.Count(s => s.LocalDate >= _weekStart && s.LocalDate <= end)
            .ToString(culture);
    }

    private void BuildAllTime(IReadOnlyList<SessionRecord> sessions, CultureInfo culture)
    {
        PeriodLabel = _loc["AllTime"];

        var heatmap = sessions
            .GroupBy(s => s.LocalDate)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationSeconds) / 60.0);

        HeatmapValues = heatmap;
        HasChartData = sessions.Count > 0;
        FocusTimeText = FormatDuration(StatsCalculator.TotalFocusSeconds(sessions));
        SessionsText = sessions.Count.ToString(culture);
    }

    private void GoPrevious()
    {
        if (_range == StatsRange.Daily)
            _dailyDate = _dailyDate.AddDays(-1);
        else if (_range == StatsRange.Weekly)
            _weekStart = _weekStart.AddDays(-7);
        else
            return;

        Refresh();
    }

    private void GoNext()
    {
        if (_range == StatsRange.Daily)
            _dailyDate = _dailyDate.AddDays(1);
        else if (_range == StatsRange.Weekly)
            _weekStart = _weekStart.AddDays(7);
        else
            return;

        Refresh();
    }

    private bool CanGoNext() => _range switch
    {
        StatsRange.Daily => _dailyDate < _today,
        StatsRange.Weekly => _weekStart < StartOfWeek(_today),
        _ => false
    };

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var delta = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-delta);
    }

    private string FormatDuration(long seconds)
    {
        var minutes = (int)Math.Round(seconds / 60.0);
        if (minutes <= 0)
            return $"0{_loc["MinutesShort"]}";

        var hours = minutes / 60;
        var remaining = minutes % 60;
        if (hours <= 0)
            return $"{remaining}{_loc["MinutesShort"]}";
        return remaining <= 0
            ? $"{hours}{_loc["HoursShort"]}"
            : $"{hours}{_loc["HoursShort"]} {remaining:00}{_loc["MinutesShort"]}";
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose() => _loc.LanguageChanged -= OnLanguageChanged;
}
