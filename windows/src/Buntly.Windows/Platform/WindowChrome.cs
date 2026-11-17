using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Buntly.Windows.Platform;

/// <summary>
/// Keeps the pill from taking keyboard focus, and lets clicks pass through
/// it except when there is something to click (the hands-free ✕, or Copy).
/// </summary>
internal static class WindowChrome
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExTransparent = 0x00000020;

    public static void ApplyNoActivate(Window window)
    {
        var handle = HandleOf(window);
        if (handle == IntPtr.Zero)
            return;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow | WsExTopmost;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
    }

    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var handle = HandleOf(window);
        if (handle == IntPtr.Zero)
            return;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        if (clickThrough)
            style |= WsExTransparent;
        else
            style &= ~WsExTransparent;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
    }

    public static IntPtr HandleOf(Window window) =>
        window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
