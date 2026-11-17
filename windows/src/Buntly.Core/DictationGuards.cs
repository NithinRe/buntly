namespace Buntly.Core;

/// <summary>
/// Why a recording was thrown away before paste. Same three filters as the
/// Mac app: too short, too quiet, or a line Whisper invents from silence.
/// </summary>
public enum DiscardReason
{
    TooShort,
    TooQuiet,
    Hallucination,
    EmptyTranscript,
}

/// <summary>
/// The pure filters between "the microphone returned audio" and "paste this".
/// Thresholds match <c>DictationGuards.swift</c> so a Windows take is judged
/// the same way as a Mac take. No microphone and no model in here, which is
/// why the tests can run on the Mac.
/// </summary>
public static class DictationGuards
{
    /// <summary>Whisper wants 16 kHz mono. The Mac recorder uses the same rate.</summary>
    public const int SampleRate = 16_000;

    /// <summary>Shorter than this is an accidental tap. Matches <c>minSeconds</c>.</summary>
    public const double MinSeconds = 0.3;

    /// <summary>
    /// Loudest half-second below this is the room, not speech.
    /// Matches <c>silenceRMSThreshold</c> (0.0015).
    /// </summary>
    public const float SilenceRmsThreshold = 0.0015f;

    /// <summary>Window used to find speech buried in a long quiet clip.</summary>
    public const double LoudWindowSeconds = 0.5;

    /// <summary>
    /// Phrases Whisper invents when handed near-silence. Compared after trim,
    /// case-insensitive, and only when the whole transcript is just that phrase.
    /// </summary>
    public static readonly string[] HallucinationPhrases =
    [
        "thank you.",
        "thanks for watching!",
        "you",
        "bye.",
        ".",
        "subtitles by the amara.org community",
    ];

    public static float Rms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
            return 0;

        double sum = 0;
        foreach (var sample in samples)
            sum += sample * sample;
        return (float)Math.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// Loudest half-second, not the average of the whole clip. A short
    /// "1, 2, 3" at the start of a minute-long hands-free take would fall
    /// under the silence gate if we averaged the trailing quiet in with it.
    /// </summary>
    public static float PeakWindowRms(ReadOnlySpan<float> samples, double sampleRate)
    {
        if (samples.Length == 0 || sampleRate <= 0)
            return 0;

        var windowSize = Math.Max(1, (int)(LoudWindowSeconds * sampleRate));
        if (samples.Length <= windowSize)
            return Rms(samples);

        float loudest = 0;
        for (var start = 0; start < samples.Length; start += windowSize)
        {
            var end = Math.Min(start + windowSize, samples.Length);
            var slice = Rms(samples[start..end]);
            if (slice > loudest)
                loudest = slice;
        }
        return loudest;
    }

    public static double DurationSeconds(int sampleCount, double sampleRate)
    {
        if (sampleRate <= 0)
            return 0;
        return sampleCount / sampleRate;
    }

    /// <summary>Null means the clip is worth sending to Whisper.</summary>
    public static DiscardReason? RejectRecording(ReadOnlySpan<float> samples, double sampleRate)
    {
        var seconds = DurationSeconds(samples.Length, sampleRate);
        if (seconds < MinSeconds)
            return DiscardReason.TooShort;

        var loudness = PeakWindowRms(samples, sampleRate);
        if (loudness < SilenceRmsThreshold)
            return DiscardReason.TooQuiet;

        return null;
    }

    /// <summary>Null means the transcript is real speech.</summary>
    public static DiscardReason? RejectTranscript(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return DiscardReason.EmptyTranscript;

        foreach (var phrase in HallucinationPhrases)
        {
            if (string.Equals(trimmed, phrase, StringComparison.OrdinalIgnoreCase))
                return DiscardReason.Hallucination;
        }
        return null;
    }
}
