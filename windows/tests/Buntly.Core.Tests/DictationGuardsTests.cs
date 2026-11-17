namespace Buntly.Core.Tests;

public class DictationGuardsTests
{
    private static float[] Silent(double seconds) =>
        new float[(int)(seconds * DictationGuards.SampleRate)];

    private static float[] Speech(double seconds, float amplitude = 0.1f)
    {
        var samples = new float[(int)(seconds * DictationGuards.SampleRate)];
        Array.Fill(samples, amplitude);
        return samples;
    }

    [Fact]
    public void Rejects_clip_shorter_than_minimum()
    {
        var reason = DictationGuards.RejectRecording(Speech(0.1), DictationGuards.SampleRate);
        Assert.Equal(DiscardReason.TooShort, reason);
    }

    [Fact]
    public void Rejects_silent_clip_even_when_long_enough()
    {
        var reason = DictationGuards.RejectRecording(Silent(1), DictationGuards.SampleRate);
        Assert.Equal(DiscardReason.TooQuiet, reason);
    }

    [Fact]
    public void Accepts_normal_speech()
    {
        Assert.Null(DictationGuards.RejectRecording(Speech(1), DictationGuards.SampleRate));
    }

    [Fact]
    public void Rejects_known_hallucination_phrases()
    {
        Assert.Equal(DiscardReason.Hallucination, DictationGuards.RejectTranscript("Thank you."));
        Assert.Equal(DiscardReason.Hallucination, DictationGuards.RejectTranscript("thanks for watching!"));
        Assert.Equal(DiscardReason.EmptyTranscript, DictationGuards.RejectTranscript(""));
    }

    [Fact]
    public void Accepts_a_real_transcript()
    {
        Assert.Null(DictationGuards.RejectTranscript("The HAP contract expires in March."));
    }

    [Fact]
    public void Rms_of_a_constant_signal_is_the_amplitude()
    {
        var samples = new float[1000];
        Array.Fill(samples, 0.1f);
        Assert.Equal(0.1f, DictationGuards.Rms(samples), precision: 4);
    }

    [Fact]
    public void Brief_speech_survives_a_minute_of_trailing_silence()
    {
        var samples = Speech(0.5, 0.01f).Concat(Silent(60)).ToArray();
        Assert.True(DictationGuards.Rms(samples) < DictationGuards.SilenceRmsThreshold);
        Assert.Null(DictationGuards.RejectRecording(samples, DictationGuards.SampleRate));
    }

    [Fact]
    public void Brief_speech_survives_a_minute_of_leading_silence()
    {
        var samples = Silent(60).Concat(Speech(0.5, 0.01f)).ToArray();
        Assert.Null(DictationGuards.RejectRecording(samples, DictationGuards.SampleRate));
    }

    [Fact]
    public void Peak_window_finds_the_loudest_moment()
    {
        var samples = Silent(5).Concat(Speech(0.6, 0.1f)).Concat(Silent(5)).ToArray();
        var peak = DictationGuards.PeakWindowRms(samples, DictationGuards.SampleRate);
        Assert.Equal(0.1f, peak, precision: 3);
        Assert.True(DictationGuards.Rms(samples) < peak);
    }
}
