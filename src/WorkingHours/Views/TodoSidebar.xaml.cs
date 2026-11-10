using System.Windows.Controls;
using System.Windows.Input;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class TodoSidebar : UserControl
{
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
}
