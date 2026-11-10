using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using WorkingHours.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly MainViewModel _vm;
    private readonly HotkeyService _hotkeys = new();
    private bool _forceClose;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _vm = services.Main;
        DataContext = _vm;

        RestoreWindowBounds();
        TaskbarItemInfo = new TaskbarItemInfo { ProgressState = TaskbarItemProgressState.None };

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.AttentionRequested += OnAttentionRequested;
        _services.Themes.ThemeApplied += OnThemeApplied;
        _services.Settings.Changed += OnSettingsChanged;

        _hotkeys.ToggleRequested += (_, _) => _vm.ToggleCommand.Execute(null);
        _hotkeys.ResetRequested += (_, _) => _vm.ResetCommand.Execute(null);
        _hotkeys.SkipRequested += (_, _) => _vm.SkipCommand.Execute(null);
        _hotkeys.RestartRequested += (_, _) => _vm.RestartCommand.Execute(null);

        SourceInitialized += OnSourceInitialized;
        StateChanged += OnWindowStateChanged;
        SizeChanged += OnWindowSizeChanged;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = _vm.IsPinned;
    }

    public void CloseForExit()
    {
        _forceClose = true;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowEffects.ApplyRoundedCorners(handle);
        WindowEffects.SetDarkTitleBar(handle, _services.Themes.CurrentTheme?.IsLight != true);

        _hotkeys.Attach(handle);
        _hotkeys.SetEnabled(_services.Settings.Current.GlobalShortcutsEnabled);
    }

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        Dial.InvalidateVisual();
        if (new WindowInteropHelper(this).Handle is var handle && handle != IntPtr.Zero)
            WindowEffects.SetDarkTitleBar(handle, _services.Themes.CurrentTheme?.IsLight != true);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _hotkeys.SetEnabled(_services.Settings.Current.GlobalShortcutsEnabled);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Progress):
            case nameof(MainViewModel.IsRunning):
            case nameof(MainViewModel.IsPaused):
                UpdateTaskbarProgress();
                break;
        }
    }

    private void UpdateTaskbarProgress()
    {
        if (TaskbarItemInfo is null)
            return;

        TaskbarItemInfo.ProgressValue = _vm.Progress;

        if (_vm.IsPaused)
        {
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
        }
        else if (_vm.IsRunning)
        {
            TaskbarItemInfo.ProgressState = _vm.Round == Core.Models.RoundType.Work
                ? TaskbarItemProgressState.Normal
                : TaskbarItemProgressState.Indeterminate;
        }
        else
        {
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
        }
    }

    private void OnAttentionRequested(object? sender, EventArgs e)
    {
        if (IsActive)
            return;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            WindowEffects.FlashTaskbar(handle);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
            return;

        var settings = _services.Settings.Current;
        if (settings.MinimizeToTray && settings.TrayIconEnabled)
            Hide();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _vm.IsCompact = ActualWidth < 370 || ActualHeight < 520;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowBounds();
        _services.Settings.SaveWithoutNotify();

        var settings = _services.Settings.Current;
        if (!_forceClose && settings.MinimizeToTrayOnClose && settings.TrayIconEnabled)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _forceClose = true;
        Dispatcher.BeginInvoke(new Action(() => Application.Current.Shutdown()));
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm.AttentionRequested -= OnAttentionRequested;
        _services.Themes.ThemeApplied -= OnThemeApplied;
        _services.Settings.Changed -= OnSettingsChanged;
        _hotkeys.Dispose();
    }

    private void RestoreWindowBounds()
    {
        var settings = _services.Settings.Current;
        if (settings.WindowWidth is > 280)
            Width = settings.WindowWidth.Value;
        if (settings.WindowHeight is > 380)
            Height = settings.WindowHeight.Value;

        if (settings.WindowX is { } x && settings.WindowY is { } y)
        {
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            if (x + 80 > virtualLeft && x < virtualRight - 80 &&
                y + 60 > virtualTop && y < virtualBottom - 60)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = x;
                Top = y;
            }
        }
    }

    private void SaveWindowBounds()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
            return;

        var settings = _services.Settings.Current;
        settings.WindowX = (int)Math.Round(bounds.Left);
        settings.WindowY = (int)Math.Round(bounds.Top);
        settings.WindowWidth = (int)Math.Round(bounds.Width);
        settings.WindowHeight = (int)Math.Round(bounds.Height);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed || WindowState == WindowState.Maximized)
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button can be released between the event and the drag.
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers != ModifierKeys.None)
            return;

        switch (e.Key)
        {
            case Key.Space:
                _vm.ToggleCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R:
                _vm.ResetCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.S:
                _vm.SkipCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.M:
                _vm.ToggleMuteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F11:
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                e.Handled = true;
                break;
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_services) { Owner = this };
        window.ShowDialog();
    }

    private void Stats_Click(object sender, RoutedEventArgs e)
    {
        var window = new StatsWindow(_services.Stats, _services.Loc, _services.Themes) { Owner = this };
        window.ShowDialog();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

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
