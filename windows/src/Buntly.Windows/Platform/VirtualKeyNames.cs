namespace Buntly.Windows.Platform;

internal static class VirtualKeyNames
{
    public static string Label(int virtualKey) => virtualKey switch
    {
        0xA3 => "Right Ctrl",
        0xA2 => "Left Ctrl",
        0xA5 => "Right Alt",
        0xA4 => "Left Alt",
        0xA1 => "Right Shift",
        0xA0 => "Left Shift",
        0x5B => "Left Windows",
        0x5C => "Right Windows",
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x6F),
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        _ => "Key " + virtualKey,
    };
}
