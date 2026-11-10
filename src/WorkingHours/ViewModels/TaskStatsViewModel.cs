using System.Windows.Input;
using WorkingHours.Core.Models;
using WorkingHours.Core.Services;
using WorkingHours.Infrastructure;
using WorkingHours.Services;

namespace WorkingHours.ViewModels;

public enum TaskStatsFilter
{
    All,
    Pending,
    Completed
}

public sealed class TaskStatsRow
{
    public required string Text { get; init; }
    public required bool IsDone { get; init; }
    public required int Seconds { get; init; }
    public required string TimeText { get; init; }
    public required string CompletionText { get; init; }
    public required double BarPercent { get; init; }
}

public sealed class TaskStatsViewModel : ObservableObject, IDisposable
{
    private readonly TodoViewModel _todos;
    private readonly LocalizationService _loc;

    private TaskStatsFilter _filter = TaskStatsFilter.All;
    private IReadOnlyList<TaskStatsRow> _rows = Array.Empty<TaskStatsRow>();
    private string _trackedTimeText = string.Empty;
    private string _completedText = "0";
    private string _pendingText = "0";
    private string _averageCompletedText = "—";
    private string _mostTimeText = "—";
    private bool _hasRows;

    public TaskStatsViewModel(TodoViewModel todos, LocalizationService loc)
    {
        _todos = todos;
        _loc = loc;
        _loc.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    public IReadOnlyList<TaskStatsRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public string TrackedTimeText
    {
        get => _trackedTimeText;
        private set => SetProperty(ref _trackedTimeText, value);
    }

    public string CompletedText
    {
        get => _completedText;
        private set => SetProperty(ref _completedText, value);
    }

    public string PendingText
    {
        get => _pendingText;
        private set => SetProperty(ref _pendingText, value);
    }

    public string AverageCompletedText
    {
        get => _averageCompletedText;
        private set => SetProperty(ref _averageCompletedText, value);
    }

    public string MostTimeText
    {
        get => _mostTimeText;
        private set => SetProperty(ref _mostTimeText, value);
    }

    public bool HasRows
    {
        get => _hasRows;
        private set => SetProperty(ref _hasRows, value);
    }

    public TaskStatsFilter Filter
    {
        get => _filter;
        set
        {
            if (!SetProperty(ref _filter, value))
                return;

            OnPropertyChanged(nameof(IsAll));
            OnPropertyChanged(nameof(IsPending));
            OnPropertyChanged(nameof(IsCompleted));
            Refresh();
        }
    }

    public bool IsAll
    {
        get => Filter == TaskStatsFilter.All;
        set { if (value) Filter = TaskStatsFilter.All; }
    }

    public bool IsPending
    {
        get => Filter == TaskStatsFilter.Pending;
        set { if (value) Filter = TaskStatsFilter.Pending; }
    }

    public bool IsCompleted
    {
        get => Filter == TaskStatsFilter.Completed;
        set { if (value) Filter = TaskStatsFilter.Completed; }
    }

    public void Refresh()
    {
        var data = _todos.Items.Select(item => new
        {
            Item = item,
            Seconds = Math.Max(0, item.ElapsedSeconds)
        }).ToList();

        var snapshots = data.Select(d => new TodoItem
        {
            Text = d.Item.Text,
            IsDone = d.Item.IsDone,
            TimeSpentSeconds = d.Seconds,
            CreatedAt = d.Item.Item.CreatedAt,
            CompletedAt = d.Item.Item.CompletedAt
        }).ToList();

        var summary = TaskStatsCalculator.Calculate(snapshots);

        TrackedTimeText = FormatSummary(summary.TotalTrackedSeconds);
        CompletedText = summary.CompletedTasks.ToString(_loc.Culture);
        PendingText = summary.PendingTasks.ToString(_loc.Culture);
        AverageCompletedText = summary.CompletedTasks > 0
            ? FormatSummary(summary.AverageCompletedSeconds)
            : "—";
        MostTimeText = summary.MostTimeTask is null
            ? "—"
            : $"{summary.MostTimeTask.Text} · {FormatSummary(summary.MostTimeTask.TimeSpentSeconds)}";

        var filtered = Filter switch
        {
            TaskStatsFilter.Pending => data.Where(d => !d.Item.IsDone).ToList(),
            TaskStatsFilter.Completed => data.Where(d => d.Item.IsDone).ToList(),
            _ => data
        };

        var maxSeconds = filtered.Count > 0 ? filtered.Max(d => d.Seconds) : 0;
        Rows = filtered
            .OrderByDescending(d => d.Seconds)
            .ThenBy(d => d.Item.Text, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => new TaskStatsRow
            {
                Text = d.Item.Text,
                IsDone = d.Item.IsDone,
                Seconds = d.Seconds,
                TimeText = FormatClock(d.Seconds),
                BarPercent = maxSeconds > 0 ? d.Seconds * 100.0 / maxSeconds : 0,
                CompletionText = d.Item.IsDone
                    ? d.Item.Item.CompletedAt is { } completed
                        ? $"{_loc["CompletedOn"]} {completed.ToString("d MMM", _loc.Culture)}"
                        : _loc["CompletedTasks"]
                    : string.Empty
            })
            .ToList();

        HasRows = Rows.Count > 0;
    }

    private string FormatSummary(long seconds)
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

    private static string FormatClock(int seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    public void Dispose() => _loc.LanguageChanged -= OnLanguageChanged;
}
