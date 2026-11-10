using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WorkingHours.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class TaskStatsWindow : Window
{
    private readonly TaskStatsViewModel _vm;

    public TaskStatsWindow(TodoViewModel todos, LocalizationService loc, ThemeService themes)
    {
        InitializeComponent();
        _vm = new TaskStatsViewModel(todos, loc);
        DataContext = _vm;

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            WindowEffects.ApplyRoundedCorners(handle);
            WindowEffects.SetDarkTitleBar(handle, themes.CurrentTheme?.IsLight != true);
        };
        Closed += (_, _) => _vm.Dispose();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
