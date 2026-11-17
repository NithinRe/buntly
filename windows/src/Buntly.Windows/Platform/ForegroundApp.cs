using System.Runtime.InteropServices;

namespace Buntly.Windows.Platform;

/// <summary>
/// The program the person was typing in when the take started, and whether
/// that program is running as Administrator. An ordinary app is not allowed
/// to type into an elevated one, so paste has to stop and leave the clipboard.
/// </summary>
internal static class ForegroundApp
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;

    public static string? Name()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return null;
        _ = GetWindowThreadProcessId(window, out var processId);
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
            return null;
        try
        {
            var buffer = new char[1024];
            var size = buffer.Length;
            if (!QueryFullProcessImageName(process, 0, buffer, ref size))
                return null;
            return Path.GetFileNameWithoutExtension(new string(buffer, 0, size));
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static bool IsElevatedOverUs()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return false;
        _ = GetWindowThreadProcessId(window, out var processId);
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
            return false;
        try
        {
            return IsElevated(process) && !IsElevated(GetCurrentProcess());
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static bool IsElevated(IntPtr process)
    {
        if (!OpenProcessToken(process, TokenQuery, out var token))
            return false;
        try
        {
            var elevation = new TokenElevationInfo();
            var size = Marshal.SizeOf<TokenElevationInfo>();
            if (!GetTokenInformation(token, TokenElevation, ref elevation, size, out _))
                return false;
            return elevation.TokenIsElevated != 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevationInfo
    {
        public int TokenIsElevated;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr token,
        int informationClass,
        ref TokenElevationInfo elevation,
        int length,
        out int returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process,
        int flags,
        char[] buffer,
        ref int size);
}
