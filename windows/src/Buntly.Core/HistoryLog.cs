using System.Text.Json;

namespace Buntly.Core;

public sealed record HistoryEntry(
    string Id,
    DateTimeOffset At,
    string Text,
    double Seconds,
    string? App,
    string Model,
    string Outcome);

/// <summary>
/// Append-only transcript log. Audio is never written here. One JSON object
/// per line, split by month, same shape as the Mac history file. A failure
/// to write is returned as a message. It must not throw into the paste path.
/// </summary>
public sealed class HistoryLog
{
    private readonly string _directory;
    private readonly object _gate = new();

    public HistoryLog(string directory)
    {
        _directory = directory;
    }

    public string? Append(HistoryEntry entry)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_directory);
                TryMarkNotIndexed(_directory);
                var path = FileFor(entry.At);
                var line = JsonSerializer.Serialize(entry, JsonOptions());
                File.AppendAllText(path, line + Environment.NewLine);
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not write history in {_directory}. {ex.Message} Dictation still pastes. Check that the folder is writable.";
        }
    }

    public IReadOnlyList<HistoryEntry> ReadAll()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_directory))
                return [];

            var entries = new List<HistoryEntry>();
            foreach (var file in Directory.GetFiles(_directory, "*.jsonl").OrderBy(path => path))
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions());
                        if (entry is not null)
                            entries.Add(entry);
                    }
                    catch (JsonException)
                    {
                        // One bad line must not hide the rest of the month.
                    }
                }
            }
            entries.Sort((a, b) => b.At.CompareTo(a.At));
            return entries;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_directory))
                return;
            foreach (var file in Directory.GetFiles(_directory, "*.jsonl"))
                File.Delete(file);
        }
    }

    /// <summary>
    /// Drops lines older than <paramref name="retentionDays"/>. Zero keeps
    /// everything. Rewrites a month file only when a line was actually removed.
    /// </summary>
    public void Prune(int retentionDays, DateTimeOffset now)
    {
        if (retentionDays <= 0)
            return;

        var cutoff = now.AddDays(-retentionDays);
        lock (_gate)
        {
            if (!Directory.Exists(_directory))
                return;

            foreach (var file in Directory.GetFiles(_directory, "*.jsonl"))
            {
                var kept = new List<string>();
                var removed = false;
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    HistoryEntry? entry;
                    try
                    {
                        entry = JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions());
                    }
                    catch (JsonException)
                    {
                        kept.Add(line);
                        continue;
                    }

                    if (entry is null || entry.At < cutoff)
                    {
                        removed = true;
                        continue;
                    }
                    kept.Add(line);
                }

                if (!removed)
                    continue;
                if (kept.Count == 0)
                    File.Delete(file);
                else
                    File.WriteAllLines(file, kept);
            }
        }
    }

    private string FileFor(DateTimeOffset at) =>
        Path.Combine(_directory, at.UtcDateTime.ToString("yyyy-MM") + ".jsonl");

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static void TryMarkNotIndexed(string directory)
    {
        try
        {
            var attributes = File.GetAttributes(directory);
            attributes |= FileAttributes.NotContentIndexed;
            File.SetAttributes(directory, attributes);
        }
        catch (Exception)
        {
            // Mac and some Windows setups reject this attribute. The folder
            // is still usable. Search may index it. Not fatal.
        }
    }
}
