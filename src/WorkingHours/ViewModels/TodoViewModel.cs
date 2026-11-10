using System.Collections.ObjectModel;
using System.Windows.Input;
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

            Item.IsDone = value;
            _store.Update(Item);
            OnPropertyChanged();
            _owner.RefreshCounts();
        }
    }
}

/// <summary>Backs the slide-out task sidebar and keeps it in sync with disk.</summary>
public sealed class TodoViewModel : ObservableObject
{
    private readonly TodoStore _store;
    private readonly LocalizationService _loc;
    private string _newTodoText = string.Empty;

    public TodoViewModel(TodoStore store, LocalizationService loc)
    {
        _store = store;
        _loc = loc;

        foreach (var item in store.GetAll())
            Items.Add(new TodoItemViewModel(item, store, this));

        AddCommand = new RelayCommand(AddTask, () => !string.IsNullOrWhiteSpace(NewTodoText));
        DeleteCommand = new RelayCommand(parameter =>
        {
            if (parameter is TodoItemViewModel item)
                DeleteTask(item);
        });
        ClearCompletedCommand = new RelayCommand(ClearCompleted, () => HasCompleted);

        _loc.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<TodoItemViewModel> Items { get; } = new();

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
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

    private void DeleteTask(TodoItemViewModel item)
    {
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

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(RemainingText));
}
