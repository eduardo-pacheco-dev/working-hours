using WorkingHours.Core.Models;
using WorkingHours.Core.Services;

namespace WorkingHours.Services;

/// <summary>Owns the live settings object and notifies the app when they change.</summary>
public sealed class SettingsManager
{
    private readonly SettingsStore _store;

    public SettingsManager(SettingsStore store)
    {
        _store = store;
        Current = store.Load();
        Current.Sanitize();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public void Replace(AppSettings settings)
    {
        settings.Sanitize();
        Current = settings;
        _store.Save(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Persists in-place changes (window bounds, mute, pin) without re-applying everything.</summary>
    public void SaveWithoutNotify() => _store.Save(Current);
}
