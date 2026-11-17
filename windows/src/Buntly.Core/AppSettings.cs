using System.Text.Json;

namespace Buntly.Core;

/// <summary>
/// The settings the person can change. Stored as one JSON file under Local
/// AppData. Defaults match the Mac app: history on, kept until they say
/// otherwise, pill at the bottom. The hotkey default is Right Ctrl
/// (virtual-key 0xA3) because the Fn key on a Windows laptop usually never
/// reaches software.
/// </summary>
public sealed class AppSettings
{
    public const int DefaultHotkey = 0xA3;

    public List<UserCorrection> UserCorrections { get; set; } = [];

    public bool HistoryEnabled { get; set; } = true;

    /// <summary>0 means keep history until they clear it.</summary>
    public int HistoryRetentionDays { get; set; }

    public bool OverlayAtTop { get; set; }

    public double OverlayMargin { get; set; } = 25;

    public int HotkeyVirtualKey { get; set; } = DefaultHotkey;

    public bool StartWithWindows { get; set; }

    public bool ChecklistComplete { get; set; }

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions()) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A broken settings file should not stop dictation. Start from
            // defaults. The next save replaces the broken file.
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this, JsonOptions());
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
