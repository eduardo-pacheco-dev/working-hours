using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WorkingHours.Services;

/// <summary>System-wide hotkeys registered through RegisterHotKey.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int ToggleId = 0xB001;
    private const int ResetId = 0xB002;
    private const int SkipId = 0xB003;
    private const int RestartId = 0xB004;

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    public event EventHandler? ToggleRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? SkipRequested;
    public event EventHandler? RestartRequested;

    public void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    public void SetEnabled(bool enabled)
    {
        if (_hwnd == IntPtr.Zero)
            return;

        Unregister();
        if (!enabled)
            return;

        var modifiers = ModControl | ModNoRepeat;
        _registered =
            RegisterHotKey(_hwnd, ToggleId, modifiers, 0x70) &&   // Ctrl+F1
            RegisterHotKey(_hwnd, ResetId, modifiers, 0x71) &&    // Ctrl+F2
            RegisterHotKey(_hwnd, SkipId, modifiers, 0x72) &&     // Ctrl+F3
            RegisterHotKey(_hwnd, RestartId, modifiers, 0x73);    // Ctrl+F4
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
            return IntPtr.Zero;

        switch (wParam.ToInt32())
        {
            case ToggleId:
                ToggleRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
            case ResetId:
                ResetRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
            case SkipId:
                SkipRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
            case RestartId:
                RestartRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void Unregister()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        UnregisterHotKey(_hwnd, ToggleId);
        UnregisterHotKey(_hwnd, ResetId);
        UnregisterHotKey(_hwnd, SkipId);
        UnregisterHotKey(_hwnd, RestartId);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
