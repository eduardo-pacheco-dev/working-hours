using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WorkingHours.Core.Services;
using WorkingHours.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public enum StatsWindowTab
{
    Focus,
    Tasks
}

public partial class StatsWindow : Window
{
    private readonly StatsViewModel _focusVm;
    private readonly TaskStatsViewModel _taskVm;
    private readonly ThemeService _themes;

    public StatsWindow(
        StatsStore stats,
        TodoViewModel todos,
        LocalizationService loc,
        ThemeService themes,
        StatsRange? initialRange = null,
        StatsWindowTab initialTab = StatsWindowTab.Focus)
    {
        InitializeComponent();
        _themes = themes;
        _focusVm = new StatsViewModel(stats, loc);
        if (initialRange is { } range)
            _focusVm.Range = range;

        _taskVm = new TaskStatsViewModel(todos, loc);

        DataContext = _focusVm;
        FocusContent.DataContext = _focusVm;
        TasksContent.DataContext = _taskVm;

        SelectTab(initialTab);

        _themes.ThemeApplied += OnThemeApplied;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            WindowEffects.ApplyRoundedCorners(handle);
            WindowEffects.SetDarkTitleBar(handle, themes.CurrentTheme?.IsLight != true);
        };
        Closed += OnClosed;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void SelectTab(StatsWindowTab tab)
    {
        var focus = tab == StatsWindowTab.Focus;
        FocusTab.IsChecked = focus;
        TasksTab.IsChecked = !focus;
        FocusContent.Visibility = focus ? Visibility.Visible : Visibility.Collapsed;
        TasksContent.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FocusTab_Click(object sender, RoutedEventArgs e) => SelectTab(StatsWindowTab.Focus);

    private void TasksTab_Click(object sender, RoutedEventArgs e) => SelectTab(StatsWindowTab.Tasks);

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        Chart.InvalidateVisual();
        Heatmap.InvalidateVisual();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _themes.ThemeApplied -= OnThemeApplied;
        _focusVm.Dispose();
        _taskVm.Dispose();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
