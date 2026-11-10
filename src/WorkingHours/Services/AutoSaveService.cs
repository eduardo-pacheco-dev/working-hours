using System.Windows.Threading;

namespace WorkingHours.Services;

/// <summary>
/// Periodically flushes state that would otherwise only be written on exit
/// (window geometry and the running task stopwatch). Each participant registers
/// its own save action so this stays independent of the UI.
/// </summary>
public sealed class AutoSaveService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly List<(string Key, Action Save)> _actions = new();

    public AutoSaveService(TimeSpan? interval = null)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval ?? TimeSpan.FromSeconds(15)
        };
        _timer.Tick += (_, _) => Flush();
    }

    public DateTime? LastSavedAt { get; private set; }

    public event EventHandler? Saved;

    public void Register(string key, Action save)
    {
        Unregister(key);
        _actions.Add((key, save));
    }

    public void Unregister(string key)
    {
        _actions.RemoveAll(a => a.Key == key);
    }

    public void Start() => _timer.Start();

    public void Flush()
    {
        foreach (var (key, save) in _actions.ToList())
        {
            try
            {
                save();
            }
            catch (Exception ex)
            {
                Diagnostics.Log(ex, $"AutoSave:{key}");
            }
        }

        LastSavedAt = DateTime.Now;
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
