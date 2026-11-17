using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Buntly.Windows.Platform;

/// <summary>
/// "Start when I sign in" is a per-user Run key. No admin password, and it
/// points at the exe the person actually launched so a later install in a
/// new folder does not keep starting the old one.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Buntly";

    [SupportedOSPlatform("windows")]
    public static void Apply(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
            return;

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
            return;
        key.SetValue(ValueName, $"\"{path}\"");
    }
}
