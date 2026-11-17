using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Buntly.Windows.Platform;

/// <summary>
/// Puts the transcript on the clipboard and sends Ctrl+V, then puts back
/// whatever text was there. Same idea as the Mac app. Call this on the UI
/// thread. The clipboard APIs require it.
/// </summary>
internal static class PasteAtCursor
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const byte VkControl = 0x11;
    private const byte VkV = 0x56;
    private const uint KeyUp = 0x0002;

    public static string? Paste(string text)
    {
        if (!OperatingSystem.IsWindows())
            return "Paste only runs on Windows. The text was not sent.";

        if (ForegroundApp.IsElevatedOverUs())
        {
            if (!TrySetText(text))
            {
                return "Buntly transcribed the audio but could not reach the clipboard. Copy it from History.";
            }
            return "Buntly transcribed the audio but cannot paste into a program running as Administrator. " +
                   "The text is on your clipboard. Press Ctrl+V to paste it.";
        }

        // If we could not read the old clipboard, do not "restore" later by
        // emptying it. A failed read and an empty clipboard look the same
        // unless we keep them apart.
        var hadPrevious = TryGetText(out var previous);
        if (!TrySetText(text))
        {
            return "Buntly transcribed the audio but could not put it on the clipboard. It is in History.";
        }

        Thread.Sleep(50);
        keybd_event(VkControl, 0, 0, UIntPtr.Zero);
        keybd_event(VkV, 0, 0, UIntPtr.Zero);
        keybd_event(VkV, 0, KeyUp, UIntPtr.Zero);
        keybd_event(VkControl, 0, KeyUp, UIntPtr.Zero);

        // The clipboard APIs want the UI thread. Give the target app a moment
        // to read, then put the previous text back if it is still ours.
        _ = RestoreClipboardLater(text, hadPrevious, previous);
        return null;
    }

    private static async Task RestoreClipboardLater(string text, bool hadPrevious, string? previous)
    {
        await Task.Delay(300);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!hadPrevious)
                return;
            if (TryGetText(out var current) && current == text)
                TrySetText(previous);
        });
    }

    public static bool TrySetText(string? text)
    {
        if (!OpenForWrite())
            return false;
        try
        {
            if (!EmptyClipboard())
                return false;
            if (text is null)
                return true;

            var bytes = (text.Length + 1) * 2;
            var handle = GlobalAlloc(GmemMoveable, (nuint)bytes);
            if (handle == IntPtr.Zero)
                return false;
            var locked = GlobalLock(handle);
            if (locked == IntPtr.Zero)
            {
                GlobalFree(handle);
                return false;
            }
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, locked, text.Length);
                Marshal.WriteInt16(locked, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
            {
                GlobalFree(handle);
                return false;
            }
            // SetClipboardData owns the handle now.
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static bool TryGetText(out string? text)
    {
        text = null;
        if (!OpenClipboard(IntPtr.Zero))
            return false;
        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
                return true;
            var locked = GlobalLock(handle);
            // A failed lock is not an empty clipboard. Reporting success with
            // no text would make the restore step wipe whatever is there.
            if (locked == IntPtr.Zero)
                return false;
            try
            {
                text = Marshal.PtrToStringUni(locked);
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool OpenForWrite()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
                return true;
            Thread.Sleep(15);
        }
        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
