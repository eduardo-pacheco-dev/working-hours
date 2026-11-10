using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WorkingHours.Core.Services;
using WorkingHours.Services;
using WorkingHours.ViewModels;
using WorkingHours.Views;

namespace WorkingHours;

public partial class App : Application
{
    private const string MutexName = @"Local\WorkingHours.SingleInstance";
    private const string ShowEventName = @"Local\WorkingHours.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private CancellationTokenSource? _showListenerCancellation;
    private AppServices? _services;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Diagnostics.Log(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Diagnostics.Log(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };

        try
        {
            var previewDirectory = GetPreviewDirectory(e.Args);
            var previewMode = previewDirectory is not null;

            var paths = previewMode
                ? new AppPaths(Path.Combine(Path.GetTempPath(), "WorkingHoursPreview"))
                : new AppPaths();
            if (previewMode)
            {
                try { Directory.Delete(paths.Root, recursive: true); } catch { }
            }
            paths.EnsureCreated();

            var settings = new SettingsManager(new SettingsStore(paths));

            var loc = new LocalizationService();
            loc.Apply(settings.Current.Language);

            var themes = new ThemeService(paths);
            themes.Initialize();
            themes.Apply(settings.Current);

            var stats = new StatsStore(paths);
            var audio = new AudioService
            {
                Volume = settings.Current.Volume,
                IsMuted = settings.Current.IsMuted
            };
            var tray = new TrayService();
            var notifications = new NotificationService(tray)
            {
                Enabled = settings.Current.NotificationsEnabled
            };

            var todos = new TodoStore(paths);
            var main = new MainViewModel(settings, stats, audio, tray, notifications, todos, loc);

            _services = new AppServices(paths, settings, stats, themes, audio, tray,
                notifications, loc, main);

            if (previewMode)
            {
                PreviewRenderer.RenderAll(_services, previewDirectory!);
                Shutdown();
                return;
            }

            _window = new MainWindow(_services);
            MainWindow = _window;
            _window.Show();

            settings.Changed += OnSettingsChanged;
            tray.ShowRequested += (_, _) => _window?.ShowFromTray();
            tray.ExitRequested += (_, _) => ExitApplication();

            StartShowListener();
        }
        catch (Exception ex)
        {
            Diagnostics.Log(ex, "Startup");
            MessageBox.Show(
                $"Working Hours could not start.\n\n{ex.Message}",
                "Working Hours",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_services is null)
            return;

        var current = _services.Settings.Current;
        _services.Loc.Apply(current.Language);
        _services.Themes.Apply(current);
        _services.Main.ApplySettings(current);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Diagnostics.Log(e.Exception, "DispatcherUnhandledException");
        MessageBox.Show(
            $"Something went wrong.\n\n{e.Exception.Message}",
            "Working Hours",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void StartShowListener()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showListenerCancellation = new CancellationTokenSource();
        var token = _showListenerCancellation.Token;
        var showEvent = _showEvent;

        Task.Run(() =>
        {
            var handles = new[] { showEvent, token.WaitHandle };
            while (!token.IsCancellationRequested)
            {
                if (WaitHandle.WaitAny(handles) != 0)
                    break;

                Dispatcher.BeginInvoke(new Action(() => _window?.ShowFromTray()));
            }
        }, token);
    }

    private static string? GetPreviewDirectory(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--render-previews")
                return i + 1 < args.Length ? args[i + 1] : Path.Combine(Path.GetTempPath(), "WorkingHoursPreview");
        }
        return null;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var handle))
            {
                handle.Set();
                handle.Dispose();
            }
        }
        catch
        {
            // The other instance may be shutting down.
        }
    }

    private void ExitApplication()
    {
        if (_window is not null)
        {
            _window.CloseForExit();
            return;
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showListenerCancellation?.Cancel();
        _showListenerCancellation?.Dispose();
        _showEvent?.Dispose();
        _services?.Dispose();

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // Not the owning thread; the OS releases it on process exit anyway.
        }
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
