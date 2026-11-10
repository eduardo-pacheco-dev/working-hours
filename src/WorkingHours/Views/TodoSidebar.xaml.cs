using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class TodoSidebar : UserControl
{
    private Point _dragStart;
    private TodoItemViewModel? _dragItem;

    public TodoSidebar()
    {
        InitializeComponent();
    }

    public event EventHandler? CloseRequested;

    public void FocusNewTask() => NewTaskBox.Focus();

    private void Close_Click(object sender, System.Windows.RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void NewTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            DataContext is TodoViewModel vm &&
            vm.AddCommand.CanExecute(null))
        {
            vm.AddCommand.Execute(null);
            NewTaskBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void TasksList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(TasksList);
        _dragItem = FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is null
            ? GetItem(e.OriginalSource)
            : null;
    }

    private void TasksList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
            return;

        var position = e.GetPosition(TasksList);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(TasksList, _dragItem, DragDropEffects.Move);
        _dragItem = null;
    }

    private void TasksList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _dragItem is not null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void TasksList_Drop(object sender, DragEventArgs e)
    {
        if (_dragItem is null || DataContext is not TodoViewModel vm)
            return;

        var oldIndex = vm.Items.IndexOf(_dragItem);
        if (oldIndex < 0)
        {
            _dragItem = null;
            return;
        }

        var target = GetItem(e.OriginalSource);
        int insertIndex;

        if (target is null)
        {
            insertIndex = vm.Items.Count;
        }
        else
        {
            var targetIndex = vm.Items.IndexOf(target);
            var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            var dropAfter = container is not null &&
                            e.GetPosition(container).Y > container.ActualHeight / 2;
            insertIndex = targetIndex + (dropAfter ? 1 : 0);
        }

        if (insertIndex > oldIndex)
            insertIndex--;

        vm.MoveTask(oldIndex, insertIndex);
        _dragItem = null;
        e.Handled = true;
    }

    private static TodoItemViewModel? GetItem(object? source) =>
        FindAncestor<ListBoxItem>(source as DependencyObject)?.DataContext as TodoItemViewModel;

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
