using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WorkingHours.Services;
using WorkingHours.ViewModels;

namespace WorkingHours.Views;

/// <summary>
/// Small always-on-top clock that floats over other windows. It shares the
/// main view model, so the timer stays perfectly in sync with the full window.
/// </summary>
public partial class MiniTimerWindow : Window
{
    private readonly AppServices _services;
    private readonly MainViewModel _vm;
    private readonly bool _placeOnLoad;

    public MiniTimerWindow(AppServices services, bool placeOnLoad = true)
    {
        InitializeComponent();
        _services = services;
        _vm = services.Main;
        _placeOnLoad = placeOnLoad;
        DataContext = _vm;

        _services.Themes.ThemeApplied += OnThemeApplied;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public event EventHandler? RestoreRequested;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowEffects.HideFromAltTab(handle);
        WindowEffects.ApplyRoundedCorners(handle);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_placeOnLoad)
            PlaceWindow();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _services.Themes.ThemeApplied -= OnThemeApplied;

        var settings = _services.Settings.Current;
        settings.MiniWindowX = (int)Math.Round(Left);
        settings.MiniWindowY = (int)Math.Round(Top);
        _services.Settings.SaveWithoutNotify();
    }

    private void OnThemeApplied(object? sender, EventArgs e) => Dial.InvalidateVisual();

    private void PlaceWindow()
    {
        var workArea = SystemParameters.WorkArea;
        var settings = _services.Settings.Current;

        if (settings.MiniWindowX is { } x && settings.MiniWindowY is { } y)
        {
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            if (x + 60 > virtualLeft && x < virtualRight - 60 &&
                y + 60 > virtualTop && y < virtualBottom - 60)
            {
                Left = Math.Min(x, virtualRight - Width - 4);
                Top = Math.Min(y, virtualBottom - Height - 4);
                return;
            }
        }

        Left = workArea.Right - Width - 22;
        Top = workArea.Bottom - Height - 22;
    }

    private void RootCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (e.ClickCount == 2)
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
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
            case Key.Escape:
                RestoreRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void Expand_Click(object sender, RoutedEventArgs e) =>
        RestoreRequested?.Invoke(this, EventArgs.Empty);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
