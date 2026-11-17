using System.Runtime.InteropServices;

namespace Buntly.Windows;

internal static class NativeMessage
{
    public static void Show(string text)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine(text);
            return;
        }

        try
        {
            MessageBox(IntPtr.Zero, text, "Buntly", 0x40);
        }
        catch (DllNotFoundException)
        {
            Console.Error.WriteLine(text);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}
