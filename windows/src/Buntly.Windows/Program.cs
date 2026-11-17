using Avalonia;

namespace Buntly.Windows;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // A second copy would install a second keyboard hook and they would
        // fight over the microphone. The mutex name is local to this login.
        // WaitOne, not the constructor's initiallyOwned flag: if the previous
        // process died holding the mutex, that constructor throws before the
        // variable is assigned, and a later ReleaseMutex would crash on null.
        using var mutex = new Mutex(initiallyOwned: false, "Local\\Buntly.Windows");
        bool owned;
        try
        {
            owned = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // WaitOne still granted ownership. The previous Buntly died.
            owned = true;
        }

        if (!owned)
        {
            NativeMessage.Show("Buntly is already running. Look for the rings icon by the clock.");
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
