using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using WorkingHours.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly MainViewModel _vm;
    private const double TodoSidebarWidth = 280;

    private readonly HotkeyService _hotkeys = new();
    private MiniTimerWindow? _miniWindow;
    private bool _suppressMiniClose;
    private bool _todoOpen;
    private double _todoGrow;
    private double _todoShift;
    private bool _todoAnimating;
    private bool _forceClose;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _vm = services.Main;
        DataContext = _vm;
        TodoSidebarView.DataContext = _vm.Todos;
        _services.AutoSave.Register("window-bounds", PersistWindowBounds);

        RestoreWindowBounds();
        TaskbarItemInfo = new TaskbarItemInfo { ProgressState = TaskbarItemProgressState.None };

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.AttentionRequested += OnAttentionRequested;
        _services.Themes.ThemeApplied += OnThemeApplied;
        _services.Settings.Changed += OnSettingsChanged;
        _services.Tray.MiniRequested += OnMiniRequested;

        _hotkeys.ToggleRequested += (_, _) => _vm.ToggleCommand.Execute(null);
        _hotkeys.ResetRequested += (_, _) => _vm.ResetCommand.Execute(null);
        _hotkeys.SkipRequested += (_, _) => _vm.SkipCommand.Execute(null);
        _hotkeys.RestartRequested += (_, _) => _vm.RestartCommand.Execute(null);

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => WindowStartupLocation = WindowStartupLocation.Manual;
        StateChanged += OnWindowStateChanged;
        SizeChanged += OnWindowSizeChanged;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void ShowFromTray() => ShowMainWindow();

    public void CloseForExit()
    {
        _forceClose = true;
        Close();
    }

    private void ShowMainWindow()
    {
        CloseMini(suppressRestore: true);

        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = _vm.IsPinned;
    }

    private void ToggleMiniMode()
    {
        if (_miniWindow is not null)
        {
            RestoreFromMini();
            return;
        }

        var mini = new MiniTimerWindow(_services);
        mini.RestoreRequested += (_, _) => RestoreFromMini();
        mini.Closed += OnMiniClosed;

        _miniWindow = mini;
        _services.AutoSave.Flush();
        Hide();
        mini.Show();
    }

    private void RestoreFromMini()
    {
        CloseMini(suppressRestore: true);
        ShowMainWindow();
    }

    private void OnMiniClosed(object? sender, EventArgs e)
    {
        if (_miniWindow is not null)
            _miniWindow = null;

        if (!_suppressMiniClose && !_forceClose)
            ShowMainWindow();
    }

    private void CloseMini(bool suppressRestore)
    {
        var mini = _miniWindow;
        if (mini is null)
            return;

        _miniWindow = null;
        var previous = _suppressMiniClose;
        _suppressMiniClose = suppressRestore;
        mini.Close();
        _suppressMiniClose = previous;
    }

    private void OnMiniRequested(object? sender, EventArgs e) => ToggleMiniMode();

    private void Todo_Click(object sender, RoutedEventArgs e) => ToggleTodoSidebar();

    private void TodoSidebar_CloseRequested(object? sender, EventArgs e) => CloseTodoSidebar();

    private void ToggleTodoSidebar()
    {
        if (_todoAnimating)
            return;

        if (_todoOpen)
            CloseTodoSidebar();
        else
            OpenTodoSidebar();
    }

    private void OpenTodoSidebar(bool animate = true)
    {
        if (_todoOpen)
            return;

        _todoOpen = true;
        TodoSidebarView.DataContext = _vm.Todos;
        TodoPanel.Visibility = Visibility.Visible;

        var canResize = WindowState == WindowState.Normal;
        var grow = canResize ? TodoSidebarWidth : 0;
        var shift = canResize
            ? Math.Clamp(Left - SystemParameters.VirtualScreenLeft, 0, grow)
            : 0;
        _todoGrow = grow;
        _todoShift = shift;

        if (!animate)
        {
            TodoPanel.Width = TodoSidebarWidth;
            if (canResize)
            {
                Left -= shift;
                Width += grow;
            }
            TodoSidebarView.FocusNewTask();
            return;
        }

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (canResize)
        {
            _todoAnimating = true;
            var targetLeft = Left - shift;
            var targetWidth = Width + grow;
            var leftAnimation = new DoubleAnimation(Left, targetLeft, duration)
            {
                EasingFunction = ease
            };
            leftAnimation.Completed += (_, _) =>
            {
                BeginAnimation(LeftProperty, null);
                BeginAnimation(WidthProperty, null);
                Left = targetLeft;
                Width = targetWidth;
                _todoAnimating = false;
            };

            BeginAnimation(LeftProperty, leftAnimation);
            BeginAnimation(WidthProperty, new DoubleAnimation(Width, targetWidth, duration)
            {
                EasingFunction = ease
            });
        }

        var panelAnimation = new DoubleAnimation(TodoPanel.ActualWidth, TodoSidebarWidth, duration)
        {
            EasingFunction = ease
        };
        panelAnimation.Completed += (_, _) =>
        {
            if (!_todoOpen)
                return;
            TodoPanel.BeginAnimation(WidthProperty, null);
            TodoPanel.Width = TodoSidebarWidth;
        };
        TodoPanel.BeginAnimation(WidthProperty, panelAnimation);

        TodoSidebarView.FocusNewTask();
    }

    private void CloseTodoSidebar(bool animate = true)
    {
        if (!_todoOpen)
            return;

        _todoOpen = false;
        var grow = _todoGrow;
        var shift = _todoShift;
        _todoGrow = 0;
        _todoShift = 0;

        if (!animate)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(WidthProperty, null);
            _todoAnimating = false;
            if (grow > 0 && WindowState == WindowState.Normal)
            {
                Left += shift;
                Width = Math.Max(MinWidth, Width - grow);
            }

            TodoPanel.BeginAnimation(WidthProperty, null);
            TodoPanel.Width = 0;
            TodoPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(170);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        if (grow > 0 && WindowState == WindowState.Normal)
        {
            _todoAnimating = true;
            var targetLeft = Left + shift;
            var targetWidth = Math.Max(MinWidth, Width - grow);
            var leftAnimation = new DoubleAnimation(Left, targetLeft, duration)
            {
                EasingFunction = ease
            };
            leftAnimation.Completed += (_, _) =>
            {
                BeginAnimation(LeftProperty, null);
                BeginAnimation(WidthProperty, null);
                Left = targetLeft;
                Width = targetWidth;
                _todoAnimating = false;
            };

            BeginAnimation(LeftProperty, leftAnimation);
            BeginAnimation(WidthProperty, new DoubleAnimation(Width, targetWidth, duration)
            {
                EasingFunction = ease
            });
        }

        var panelAnimation = new DoubleAnimation(
            TodoPanel.ActualWidth > 0 ? TodoPanel.ActualWidth : TodoSidebarWidth,
            0, duration)
        {
            EasingFunction = ease
        };
        panelAnimation.Completed += (_, _) =>
        {
            if (_todoOpen)
                return;
            TodoPanel.BeginAnimation(WidthProperty, null);
            TodoPanel.Width = 0;
            TodoPanel.Visibility = Visibility.Collapsed;
        };
        TodoPanel.BeginAnimation(WidthProperty, panelAnimation);
    }

    /// <summary>Development helper used by the preview renderer.</summary>
    public void OpenTodoSidebarForPreview()
    {
        _todoOpen = true;
        TodoSidebarView.DataContext = _vm.Todos;
        TodoPanel.Visibility = Visibility.Visible;
        TodoPanel.Width = TodoSidebarWidth;
    }

    /// <summary>Development helper: renders the drag feedback for the preview.</summary>
    public void ShowTodoDragPreviewForPreview() => TodoSidebarView.ShowDragPreview();

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
        {
            _services.AutoSave.Flush();
            Hide();
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _vm.IsCompact = ActualWidth < 370 || ActualHeight < 520;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        CloseMini(suppressRestore: true);
        var sidebarWasOpen = _todoOpen;
        CloseTodoSidebar(animate: false);
        SaveWindowBounds(subtractSidebar: sidebarWasOpen && WindowState != WindowState.Normal);
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
        _services.Tray.MiniRequested -= OnMiniRequested;
        _services.AutoSave.Unregister("window-bounds");
        CloseMini(suppressRestore: true);
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

    /// <summary>Periodic auto-save hook: persists the current normal window bounds.</summary>
    public void PersistWindowBounds()
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
            return;

        SaveWindowBounds();
        _services.Settings.SaveWithoutNotify();
    }

    private void SaveWindowBounds(bool subtractSidebar = false)
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
            return;

        var width = subtractSidebar && bounds.Width > TodoSidebarWidth
            ? bounds.Width - TodoSidebarWidth
            : bounds.Width;

        var settings = _services.Settings.Current;
        settings.WindowX = (int)Math.Round(bounds.Left);
        settings.WindowY = (int)Math.Round(bounds.Top);
        settings.WindowWidth = (int)Math.Round(width);
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
        if (e.KeyboardDevice.Modifiers != ModifierKeys.None ||
            Keyboard.FocusedElement is TextBoxBase)
            return;

        switch (e.Key)
        {
            case Key.T:
                ToggleTodoSidebar();
                e.Handled = true;
                break;
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
            case Key.F9:
                ToggleMiniMode();
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

    private void Mini_Click(object sender, RoutedEventArgs e) => ToggleMiniMode();

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
