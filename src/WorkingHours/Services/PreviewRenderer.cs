using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WorkingHours.Core.Models;
using WorkingHours.Core.Services;
using WorkingHours.ViewModels;
using WorkingHours.Views;

namespace WorkingHours.Services;

/// <summary>
/// Development utility: renders the application windows to PNG files without
/// showing them on the desktop. Run with <c>--render-previews &lt;folder&gt;</c>.
/// </summary>
public static class PreviewRenderer
{
    public static void RenderAll(AppServices services, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview.log"),
            $"started {DateTime.Now:O}{Environment.NewLine}");
        SeedStatistics(services.Stats);

        var settings = new SettingsWindow(services);
        Render(settings, Path.Combine(outputDirectory, "settings.png"), closeAfter: false);
        settings.ScrollToEndForPreview();
        Render(settings, Path.Combine(outputDirectory, "settings-bottom.png"), alreadyShown: true);
        settings.Close();

        foreach (var (range, file) in new[]
                 {
                     (StatsRange.Daily, "stats-daily.png"),
                     (StatsRange.Weekly, "stats-weekly.png"),
                     (StatsRange.AllTime, "stats-alltime.png")
                 })
        {
            var stats = new StatsWindow(services.Stats, services.Loc, services.Themes, range)
            {
                Width = 720,
                Height = 700
            };
            Render(stats, Path.Combine(outputDirectory, file));
        }

        var mini = new MiniTimerWindow(services, placeOnLoad: false);
        Render(mini, Path.Combine(outputDirectory, "mini.png"));

        var main = new MainWindow(services) { Width = 440, Height = 680 };
        Render(main, Path.Combine(outputDirectory, "main.png"), closeAfter: false);

        var compact = new MainWindow(services) { Width = 330, Height = 460 };
        Render(compact, Path.Combine(outputDirectory, "main-compact.png"), closeAfter: false);

        services.Settings.Current.ThemeMode = "dark";
        services.Themes.Apply(services.Settings.Current);
        var darkMain = new MainWindow(services) { Width = 440, Height = 680 };
        Render(darkMain, Path.Combine(outputDirectory, "main-dark.png"), closeAfter: false);

        SeedTodos(services);
        var tasks = new MainWindow(services) { Width = 720, Height = 680 };
        tasks.OpenTodoSidebarForPreview();
        Render(tasks, Path.Combine(outputDirectory, "main-tasks.png"), closeAfter: false);

        tasks.ShowTodoDragPreviewForPreview();
        Render(tasks, Path.Combine(outputDirectory, "main-tasks-drag.png"),
            alreadyShown: true, closeAfter: false);

        File.AppendAllText(Path.Combine(outputDirectory, "preview.log"),
            $"finished {DateTime.Now:O}{Environment.NewLine}");
        // The main window is intentionally left open; the application shuts down next.
    }

    private static void Render(Window window, string path, bool alreadyShown = false, bool closeAfter = true)
    {
        if (!alreadyShown)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.Show();
        }

        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        window.UpdateLayout();

        if (window.Content is not FrameworkElement content || content.ActualWidth < 1)
            return;

        var dpi = VisualTreeHelper.GetDpi(content);
        var width = (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY);
        var bitmap = new RenderTargetBitmap(width, height,
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(content);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);

        if (!alreadyShown && closeAfter)
            window.Close();
    }

    private static void SeedTodos(AppServices services)
    {
        var todos = services.Main.Todos;
        if (todos.HasItems)
            return;

        todos.AddTask("Revisar proposta do cliente");
        todos.AddTask("Enviar relatório semanal");
        todos.AddTask("Preparar apresentação de sexta");
        todos.AddTask("Responder e-mails pendentes");

        if (todos.Items.Count > 0)
            SetPreviewTime(todos.Items[0], 12 * 60 + 34);
        if (todos.Items.Count > 1)
            SetPreviewTime(todos.Items[1], 48 * 60);
        if (todos.Items.Count > 2)
        {
            SetPreviewTime(todos.Items[2], 65 * 60 + 20);
            todos.Items[2].IsDone = true;
        }
    }

    private static void SetPreviewTime(TodoItemViewModel item, int seconds)
    {
        item.Item.TimeSpentSeconds = seconds;
        item.Item.TimerStartedAt = null;
        item.RefreshTime();
    }

    private static void SeedStatistics(StatsStore stats)
    {
        if (stats.GetAll().Count > 0)
            return;

        var random = new Random(42);
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var offset = 0; offset < 364; offset++)
        {
            var date = today.AddDays(-offset);
            var weekday = date.DayOfWeek;
            var baseSessions = weekday is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1 : 3;
            var count = Math.Max(0, baseSessions + random.Next(-1, 3));

            for (var i = 0; i < count; i++)
            {
                var hour = 8 + random.Next(0, 10);
                stats.Add(new SessionRecord
                {
                    StartedAt = date.ToDateTime(new TimeOnly(hour, random.Next(0, 60))),
                    DurationSeconds = 25 * 60
                });
            }
        }

        stats.Add(new SessionRecord
        {
            StartedAt = DateTime.Today.AddHours(9),
            DurationSeconds = 25 * 60
        });
    }
}
