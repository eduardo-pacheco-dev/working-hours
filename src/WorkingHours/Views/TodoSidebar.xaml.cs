using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class TodoSidebar : UserControl
{
    private Point _dragStart;
    private TodoItemViewModel? _dragItem;
    private bool _dragging;

    public TodoSidebar()
    {
        InitializeComponent();
        TasksList.LostMouseCapture += TasksList_LostMouseCapture;
    }

    public event EventHandler? CloseRequested;

    public void FocusNewTask() => NewTaskBox.Focus();

    private void Close_Click(object sender, RoutedEventArgs e) =>
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

        if (!_dragging)
        {
            if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            StartDrag(position);
        }

        UpdateDrag(position);
    }

    private void TasksList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            _dragItem = null;
            return;
        }

        FinishDrag(commit: true);
        e.Handled = true;
    }

    private void TasksList_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragging)
            FinishDrag(commit: false);
    }

    private void StartDrag(Point position)
    {
        var container = TasksList.ItemContainerGenerator.ContainerFromItem(_dragItem) as ListBoxItem;
        if (container is null)
            return;

        _dragging = true;
        SetGhostSource(container);
        var listOrigin = TasksList.TranslatePoint(new Point(0, 0), DropOverlay);
        Canvas.SetLeft(DragGhost, listOrigin.X + position.X + 14);
        Canvas.SetTop(DragGhost, listOrigin.Y + position.Y + 10);
        DragGhost.Visibility = Visibility.Visible;

        TasksList.CaptureMouse();
        UpdateDrag(position);
    }

    private void UpdateDrag(Point position)
    {
        var listOrigin = TasksList.TranslatePoint(new Point(0, 0), DropOverlay);
        Canvas.SetLeft(DragGhost, listOrigin.X + position.X + 14);
        Canvas.SetTop(DragGhost, listOrigin.Y + position.Y + 10);

        var (target, after, container) = HitTest(position);
        if (target is null || container is null)
        {
            DropLine.Visibility = Visibility.Collapsed;
        }
        else
        {
            var origin = container.TranslatePoint(new Point(0, 0), DropOverlay);
            Canvas.SetLeft(DropLine, origin.X);
            Canvas.SetTop(DropLine, origin.Y + (after ? container.ActualHeight : 0) - 1);
            DropLine.Width = Math.Max(0, container.ActualWidth);
            DropLine.Visibility = Visibility.Visible;
        }

        AutoScroll(position);
    }

    private void FinishDrag(bool commit)
    {
        if (!_dragging)
        {
            _dragItem = null;
            return;
        }

        _dragging = false;
        var position = Mouse.GetPosition(TasksList);
        var bounds = new Rect(0, 0, TasksList.ActualWidth, TasksList.ActualHeight);

        if (commit && _dragItem is not null && bounds.Contains(position) &&
            DataContext is TodoViewModel vm)
        {
            var oldIndex = vm.Items.IndexOf(_dragItem);
            var (target, after, _) = HitTest(position);

            int insertIndex;
            if (target is null)
            {
                insertIndex = vm.Items.Count;
            }
            else
            {
                insertIndex = vm.Items.IndexOf(target) + (after ? 1 : 0);
            }

            if (insertIndex > oldIndex)
                insertIndex--;

            vm.MoveTask(oldIndex, insertIndex);
        }

        DropLine.Visibility = Visibility.Collapsed;
        DragGhost.Visibility = Visibility.Collapsed;
        DragGhostVisual.Fill = null;

        if (Mouse.Captured == TasksList)
            TasksList.ReleaseMouseCapture();

        _dragItem = null;
    }

    private void SetGhostSource(ListBoxItem container)
    {
        DragGhostVisual.Width = container.ActualWidth;
        DragGhostVisual.Height = container.ActualHeight;
        DragGhostVisual.Fill = new VisualBrush(container)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
    }

    /// <summary>Development helper: shows the drag preview for the screenshot renderer.</summary>
    public void ShowDragPreview()
    {
        if (TasksList.Items.Count == 0)
            return;

        var source = TasksList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        if (source is null)
            return;

        SetGhostSource(source);
        var listOrigin = TasksList.TranslatePoint(new Point(0, 0), DropOverlay);
        var sourceOrigin = source.TranslatePoint(new Point(0, 0), DropOverlay);
        Canvas.SetLeft(DragGhost, listOrigin.X + 70);
        Canvas.SetTop(DragGhost, sourceOrigin.Y + 26);
        DragGhost.Visibility = Visibility.Visible;

        var target = TasksList.ItemContainerGenerator.ContainerFromIndex(
            Math.Min(1, TasksList.Items.Count - 1)) as ListBoxItem;
        if (target is not null)
        {
            var origin = target.TranslatePoint(new Point(0, 0), DropOverlay);
            Canvas.SetLeft(DropLine, origin.X);
            Canvas.SetTop(DropLine, origin.Y + target.ActualHeight - 1);
            DropLine.Width = target.ActualWidth;
            DropLine.Visibility = Visibility.Visible;
        }
    }

    private (TodoItemViewModel? Target, bool After, ListBoxItem? Container) HitTest(Point position)
    {
        var hit = TasksList.InputHitTest(position) as DependencyObject;
        var target = GetItem(hit);
        if (target is null)
            return (null, false, null);

        var container = FindAncestor<ListBoxItem>(hit) ??
                        TasksList.ItemContainerGenerator.ContainerFromItem(target) as ListBoxItem;
        if (container is null)
            return (target, false, null);

        var origin = container.TranslatePoint(new Point(0, 0), TasksList);
        var after = position.Y > origin.Y + container.ActualHeight / 2;
        return (target, after, container);
    }

    private void AutoScroll(Point position)
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(TasksList);
        if (scrollViewer is null)
            return;

        if (position.Y < 24)
            scrollViewer.LineUp();
        else if (position.Y > TasksList.ActualHeight - 24)
            scrollViewer.LineDown();
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

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }
        return null;
    }
}
