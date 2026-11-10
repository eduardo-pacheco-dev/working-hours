using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WorkingHours.Core.Services;
using WorkingHours.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class StatsWindow : Window
{
    private readonly StatsViewModel _vm;
    private readonly ThemeService _themes;

    public StatsWindow(StatsStore stats, LocalizationService loc, ThemeService themes, StatsRange? initialRange = null)
    {
        InitializeComponent();
        _themes = themes;
        _vm = new StatsViewModel(stats, loc);
        if (initialRange is { } range)
            _vm.Range = range;
        DataContext = _vm;

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

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        Chart.InvalidateVisual();
        Heatmap.InvalidateVisual();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _themes.ThemeApplied -= OnThemeApplied;
        _vm.Dispose();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
