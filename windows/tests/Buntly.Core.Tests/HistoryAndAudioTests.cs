namespace Buntly.Core.Tests;

public class HistoryAndAudioTests
{
    [Fact]
    public void History_round_trips_and_prune_drops_old_lines()
    {
        var root = Path.Combine(Path.GetTempPath(), "buntly-tests-" + Guid.NewGuid().ToString("N"));
        var log = new HistoryLog(Path.Combine(root, "history.noindex"));
        var old = new HistoryEntry("old", DateTimeOffset.UtcNow.AddDays(-40), "old words", 1, "Notepad", "ggml-large-v3-turbo", "pasted");
        var recent = new HistoryEntry("new", DateTimeOffset.UtcNow, "new words", 2, "Notepad", "ggml-large-v3-turbo", "pasted");

        Assert.Null(log.Append(old));
        Assert.Null(log.Append(recent));
        Assert.Equal(2, log.ReadAll().Count);

        log.Prune(retentionDays: 30, DateTimeOffset.UtcNow);
        var left = log.ReadAll();
        Assert.Single(left);
        Assert.Equal("new words", left[0].Text);

        log.Clear();
        Assert.Empty(log.ReadAll());
    }

    [Fact]
    public void Settings_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "buntly-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "settings.json");
        var settings = new AppSettings
        {
            HotkeyVirtualKey = 0xA5,
            HistoryRetentionDays = 7,
            UserCorrections = [new UserCorrection("loo tech", "LIHTC")],
            ChecklistComplete = true,
        };
        settings.Save(path);

        var loaded = AppSettings.Load(path);
        Assert.Equal(0xA5, loaded.HotkeyVirtualKey);
        Assert.Equal(7, loaded.HistoryRetentionDays);
        Assert.True(loaded.ChecklistComplete);
        Assert.Equal("LIHTC", Assert.Single(loaded.UserCorrections).Correct);
    }

    [Fact]
    public void Stereo_pcm16_becomes_mono_and_48k_becomes_16k()
    {
        // One frame, left = 0.5, right = -0.5, so mono is 0.
        var frame = new byte[4];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 2), (short)16384);
        BitConverter.TryWriteBytes(frame.AsSpan(2, 2), (short)-16384);
        var mono = PcmConvert.ToMonoFloat(frame, channels: 2, bitsPerSample: 16, ieeeFloat: false);
        Assert.Single(mono);
        Assert.Equal(0f, mono[0], precision: 3);

        var input = new float[48];
        Array.Fill(input, 0.25f);
        var output = PcmConvert.ResampleLinear(input, fromRate: 48_000, toRate: 16_000);
        Assert.Equal(16, output.Length);
        Assert.Equal(0.25f, output[0], precision: 3);
    }
}
