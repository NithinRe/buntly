using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Buntly.Windows.Platform;

/// <summary>
/// A low-level keyboard hook, which is the Windows way to see a key go down
/// and up in every app. A normal "register hotkey" call only reports a
/// press, so it cannot do hold-to-talk. The callback only posts the key to
/// the UI thread. Doing the microphone work inside the callback is how
/// Windows silently removes the hook.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint LlkhfUp = 0x80;
    private const uint LlkhfInjected = 0x10;

    private readonly HookProc _proc;
    private IntPtr _hook;
    private Action<int, bool>? _onKey;

    public int Hotkey { get; set; }
    public bool Armed { get; set; }
    public bool CaptureNext { get; set; }

    public KeyboardHook()
    {
        // The delegate must stay rooted for the life of the hook. If the
        // garbage collector collects it, the next key press crashes the process.
        _proc = Callback;
    }

    public string? Install(Action<int, bool> onKey)
    {
        _onKey = onKey;
        if (!OperatingSystem.IsWindows())
            return "The dictation key only works on Windows.";

        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, IntPtr.Zero, 0);
        if (_hook == IntPtr.Zero)
        {
            return "Windows did not install Buntly's keyboard hook (SetWindowsHookEx failed). " +
                   "Security software sometimes blocks this. Buntly needs it to notice the dictation key while another app is in front.";
        }
        return null;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _onKey = null;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || _onKey is null)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var info = Marshal.PtrToStructure<KbdLlHook>(lParam);
        // keybd_event, which is how Buntly pastes, sets this flag. Swallowing
        // those keys would eat Ctrl+V when the dictation key is Ctrl or V.
        if ((info.flags & LlkhfInjected) != 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var isUp = (info.flags & LlkhfUp) != 0;
        var vk = (int)info.vkCode;
        var onKey = _onKey;

        var isHotkey = vk == Hotkey;
        var isEscapeWhileArmed = vk == 0x1B && Armed;
        var swallow = CaptureNext || isHotkey || isEscapeWhileArmed;
        if (swallow)
            Dispatcher.UIThread.Post(() => onKey(vk, !isUp));

        if (swallow)
            return (IntPtr)1;
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHook
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
}
