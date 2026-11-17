using System.Runtime.InteropServices;

namespace Buntly.Windows.Platform;

/// <summary>
/// Asks Windows to confirm the person at the keyboard. The prompt is
/// Windows Hello when the laptop has it, otherwise the PIN or password.
/// That is the stand-in for the Mac app's Touch ID gate. The credential
/// blob Windows returns is freed immediately and never written down.
/// Buntly only needs to know that the prompt succeeded.
/// </summary>
internal static class WindowsHello
{
    private const int CredUiEnumerateCurrentUser = 0x200;
    private const int ErrorCancelled = 1223;

    public static bool Confirm(IntPtr owner, out string? failure)
    {
        failure = null;
        if (!OperatingSystem.IsWindows())
        {
            failure = "Windows Hello only runs on Windows. History stays locked.";
            return false;
        }

        var info = new CredUiInfo
        {
            cbSize = Marshal.SizeOf<CredUiInfo>(),
            hwndParent = owner,
            pszCaptionText = "Buntly",
            pszMessageText = "Confirm it's you to open dictation history.",
        };
        uint authPackage = 0;
        var save = false;
        var result = CredUIPromptForWindowsCredentials(
            ref info,
            0,
            ref authPackage,
            IntPtr.Zero,
            0,
            out var outBuffer,
            out var outSize,
            ref save,
            CredUiEnumerateCurrentUser);

        WipeAndFree(outBuffer, outSize);

        if (result == 0)
            return true;
        if (result == ErrorCancelled)
        {
            failure = "History stays locked.";
            return false;
        }

        failure = $"Windows could not show the confirmation prompt (error {result}). History stays locked.";
        return false;
    }

    /// <summary>
    /// The buffer holds the packed Windows credential. Zero it before free so
    /// a later memory dump is less likely to still contain it. Buntly never
    /// reads the bytes.
    /// </summary>
    private static void WipeAndFree(IntPtr buffer, uint size)
    {
        if (buffer == IntPtr.Zero)
            return;
        try
        {
            if (size > 0 && size <= 65_536)
            {
                var zeros = new byte[size];
                Marshal.Copy(zeros, 0, buffer, zeros.Length);
            }
        }
        finally
        {
            CoTaskMemFree(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CredUiInfo
    {
        public int cbSize;
        public IntPtr hwndParent;
        public string pszMessageText;
        public string pszCaptionText;
        public IntPtr hbmBanner;
    }

    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredUIPromptForWindowsCredentialsW")]
    private static extern int CredUIPromptForWindowsCredentials(
        ref CredUiInfo creditInfo,
        int authError,
        ref uint authPackage,
        IntPtr inAuthBuffer,
        uint inAuthBufferSize,
        out IntPtr outAuthBuffer,
        out uint outAuthBufferSize,
        ref bool save,
        int flags);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pointer);
}
