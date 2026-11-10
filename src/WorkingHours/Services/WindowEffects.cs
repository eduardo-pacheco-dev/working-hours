using System.Runtime.InteropServices;

namespace WorkingHours.Services;

/// <summary>Small Win32 helpers for native window behaviour.</summary>
public static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int CornerRound = 2;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;

    public static void ApplyRoundedCorners(IntPtr hwnd)
    {
        try
        {
            var preference = CornerRound;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10: square corners are fine.
        }
    }

    public static void SetDarkTitleBar(IntPtr hwnd, bool dark)
    {
        try
        {
            var value = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch
        {
            // Older Windows builds ignore this attribute.
        }
    }

    /// <summary>Keeps the mini overlay out of Alt+Tab while still showing it on screen.</summary>
    public static void HideFromAltTab(IntPtr hwnd)
    {
        try
        {
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            style = (style | WsExToolWindow) & ~WsExAppWindow;
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
        }
        catch
        {
            // Worst case the overlay also shows in Alt+Tab.
        }
    }

    public static void FlashTaskbar(IntPtr hwnd)
    {
        var info = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Flags = 0x00000002 | 0x0000000C, // FLASHW_TRAY | FLASHW_TIMERNOFG
            Count = 2,
            Timeout = 0
        };
        FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
