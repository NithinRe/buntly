using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Buntly.Windows.Platform;

/// <summary>
/// Windows does not give a desktop app a Microphone prompt of its own the
/// way macOS does. The switch that matters is "Let desktop apps access your
/// microphone". Deny is stored in the current-user registry. Missing means
/// Windows is still allowing it.
/// </summary>
internal static class MicrophoneConsent
{
    private const string GlobalKey = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
    private const string DesktopKey = GlobalKey + @"\NonPackaged";

    public static bool IsDenied()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        return IsDeny(GlobalKey) || IsDeny(DesktopKey);
    }

    public static void OpenSettings()
    {
        if (!OperatingSystem.IsWindows())
            return;
        Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute = true });
    }

    [SupportedOSPlatform("windows")]
    private static bool IsDeny(string keyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        var value = key?.GetValue("Value") as string;
        return string.Equals(value, "Deny", StringComparison.OrdinalIgnoreCase);
    }
}
