using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using WorkingHours.Core.Models;
using WorkingHours.Core.Services;
using WorkingHours.Infrastructure;
using WorkingHours.Services;

namespace WorkingHours.ViewModels;

public sealed class TodoItemViewModel : ObservableObject
{
    private readonly TodoStore _store;
    private readonly TodoViewModel _owner;

    internal TodoItemViewModel(TodoItem item, TodoStore store, TodoViewModel owner)
    {
        Item = item;
        _store = store;
        _owner = owner;
    }

    public TodoItem Item { get; }

    public string Text => Item.Text;

    public bool IsDone
    {
        get => Item.IsDone;
        set
        {
            if (Item.IsDone == value)
                return;

            if (value)
            {
                _owner.StopTracking(this);
                Item.IsDone = true;
                Item.CompletedAt = DateTime.Now;
            }
            else
            {
                Item.IsDone = false;
                Item.CompletedAt = null;
            }

            _store.Update(Item);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTracking));
            OnPropertyChanged(nameof(TrackingTooltip));
            _owner.RefreshCounts();
        }
    }

    public bool IsTracking => Item.TimerStartedAt is not null;

    public int ElapsedSeconds
    {
        get
        {
            var total = Item.TimeSpentSeconds;
            if (Item.TimerStartedAt is { } started)
                total += (int)Math.Max(0, (DateTime.Now - started).TotalSeconds);
            return total;
        }
    }

    public bool HasTime => IsTracking || ElapsedSeconds > 0;

    public string TimeText => FormatTime(ElapsedSeconds);

    public string TrackingTooltip =>
        IsTracking ? _owner.PauseTrackingText : _owner.StartTrackingText;

    public void RefreshTime()
    {
        OnPropertyChanged(nameof(ElapsedSeconds));
        OnPropertyChanged(nameof(HasTime));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(IsTracking));
        OnPropertyChanged(nameof(TrackingTooltip));
    }

    internal void StartTimer()
    {
        Item.TimerStartedAt = DateTime.Now;
        RefreshTime();
    }

    /// <summary>Accumulates the current interval and pauses the stopwatch.</summary>
    internal void StopTimer()
    {
        if (Item.TimerStartedAt is not { } started)
        {
            RefreshTime();
            return;
        }

        var elapsed = (int)Math.Max(0, (DateTime.Now - started).TotalSeconds);
        Item.TimeSpentSeconds += elapsed;
        Item.TimerStartedAt = null;
        RefreshTime();
    }

    private static string FormatTime(int seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }
}

/// <summary>Backs the slide-out task sidebar and keeps it in sync with disk.</summary>
public sealed class TodoViewModel : ObservableObject
{
    private readonly TodoStore _store;
    private readonly LocalizationService _loc;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private TodoItemViewModel? _trackedItem;
    private string _newTodoText = string.Empty;

    public TodoViewModel(TodoStore store, LocalizationService loc)
    {
        _store = store;
        _loc = loc;

        foreach (var item in store.GetAll())
        {
            // A stopwatch is only authoritative while the app is running.
            if (item.TimerStartedAt is not null)
            {
                item.TimerStartedAt = null;
                store.Update(item);
            }
            Items.Add(new TodoItemViewModel(item, store, this));
        }

        AddCommand = new RelayCommand(AddTask, () => !string.IsNullOrWhiteSpace(NewTodoText));
        DeleteCommand = new RelayCommand(parameter =>
        {
            if (parameter is TodoItemViewModel item)
                DeleteTask(item);
        });
        ToggleTrackingCommand = new RelayCommand(parameter =>
        {
            if (parameter is TodoItemViewModel item)
                ToggleTracking(item);
        });
        ClearCompletedCommand = new RelayCommand(ClearCompleted, () => HasCompleted);

        _clock.Tick += (_, _) => _trackedItem?.RefreshTime();
        _loc.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<TodoItemViewModel> Items { get; } = new();

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ToggleTrackingCommand { get; }
    public ICommand ClearCompletedCommand { get; }

    public string NewTodoText
    {
        get => _newTodoText;
        set
        {
            if (!SetProperty(ref _newTodoText, value))
                return;

            (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool HasItems => Items.Count > 0;
    public bool HasRemaining => RemainingCount > 0;
    public bool HasCompleted => Items.Any(i => i.IsDone);
    public int RemainingCount => Items.Count(i => !i.IsDone);

    public string RemainingText
    {
        get
        {
            var count = RemainingCount;
            if (count <= 0)
                return _loc["AllDone"];
            return count == 1 ? _loc["RemainingOne"] : _loc.Format("RemainingMany", count);
        }
    }

    internal string StartTrackingText => _loc["StartTimer"];
    internal string PauseTrackingText => _loc["PauseTimer"];

    public void AddTask(string text)
    {
        var item = _store.Add(text);
        if (item is null)
            return;

        Items.Add(new TodoItemViewModel(item, _store, this));
        NewTodoText = string.Empty;
        RefreshCounts();
    }

    private void AddTask()
    {
        AddTask(NewTodoText);
    }

    private void ToggleTracking(TodoItemViewModel item)
    {
        if (item.IsDone)
            return;

        if (ReferenceEquals(_trackedItem, item))
        {
            StopTracking(item);
            return;
        }

        StopTracking(_trackedItem);
        item.StartTimer();
        _store.Update(item.Item);
        _trackedItem = item;
        _clock.Start();
    }

    /// <summary>
    /// Starts the first pending task when the main timer starts, unless the user
    /// is already tracking a task manually.
    /// </summary>
    public void StartFirstPendingTracking()
    {
        if (_trackedItem is not null)
            return;

        var first = Items.FirstOrDefault(i => !i.IsDone);
        if (first is not null)
            ToggleTracking(first);
    }

    /// <summary>Pauses the task that is currently tracking, if any.</summary>
    public void StopActiveTracking() => StopTracking(_trackedItem);

    /// <summary>Stops the given task stopwatch and persists the accumulated time.</summary>
    public void StopTracking(TodoItemViewModel? item)
    {
        if (item is null)
            return;

        var wasTracking = item.IsTracking;
        item.StopTimer();

        if (wasTracking)
            _store.Update(item.Item);

        if (ReferenceEquals(_trackedItem, item))
        {
            _trackedItem = null;
            _clock.Stop();
        }
    }

    /// <summary>
    /// Folds the current interval into the task and keeps the stopwatch running.
    /// Used by the periodic auto-save so a crash loses at most one interval.
    /// </summary>
    public void CheckpointTracking()
    {
        if (_trackedItem?.Item.TimerStartedAt is not { } started)
            return;

        var elapsed = (int)Math.Max(0, (DateTime.Now - started).TotalSeconds);
        if (elapsed <= 0)
            return;

        _trackedItem.Item.TimeSpentSeconds += elapsed;
        _trackedItem.Item.TimerStartedAt = DateTime.Now;
        _store.Update(_trackedItem.Item);
        _trackedItem.RefreshTime();
    }

    /// <summary>Called on application exit so the running interval is not lost.</summary>
    public void StopAllTracking()
    {
        StopTracking(_trackedItem);
        _clock.Stop();
    }

    private void DeleteTask(TodoItemViewModel item)
    {
        if (!item.IsDone)
            StopTracking(item);

        _store.Delete(item.Item.Id);
        Items.Remove(item);
        RefreshCounts();
    }

    private void ClearCompleted()
    {
        _store.ClearCompleted();
        foreach (var item in Items.Where(i => i.IsDone).ToList())
            Items.Remove(item);
        RefreshCounts();
    }

    /// <summary>Moves a task in the list and persists the new manual order.</summary>
    public void MoveTask(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Items.Count)
            return;

        newIndex = Math.Clamp(newIndex, 0, Items.Count - 1);
        if (oldIndex == newIndex)
            return;

        Items.Move(oldIndex, newIndex);
        _store.Reorder(Items.Select(i => i.Item.Id).ToList());
    }

    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasRemaining));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(RemainingText));

        (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearCompletedCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RemainingText));
        foreach (var item in Items)
            item.RefreshTime();
    }
}
