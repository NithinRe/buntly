namespace Buntly.Core;

/// <summary>
/// Turns whatever the microphone handed us into the 16 kHz mono float
/// buffer Whisper reads. Linear resample is good enough for speech and
/// has no native dependency, so the length math can be tested on the Mac.
/// </summary>
public static class PcmConvert
{
    public static float[] ToMonoFloat(ReadOnlySpan<byte> bytes, int channels, int bitsPerSample, bool ieeeFloat)
    {
        if (channels <= 0 || bitsPerSample < 8 || bitsPerSample % 8 != 0)
            return [];

        var bytesPerSample = bitsPerSample / 8;
        var bytesPerFrame = channels * bytesPerSample;
        if (bytesPerFrame <= 0 || bytes.Length < bytesPerFrame)
            return [];

        var frames = bytes.Length / bytesPerFrame;
        var mono = new float[frames];
        for (var frame = 0; frame < frames; frame++)
        {
            float sum = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = frame * bytesPerFrame + channel * bytesPerSample;
                sum += ReadSample(bytes, offset, bitsPerSample, ieeeFloat);
            }
            mono[frame] = sum / channels;
        }
        return mono;
    }

    public static float[] ResampleLinear(ReadOnlySpan<float> input, int fromRate, int toRate)
    {
        if (input.Length == 0 || fromRate <= 0 || toRate <= 0)
            return [];
        if (fromRate == toRate)
            return input.ToArray();

        var outLength = (int)((long)input.Length * toRate / fromRate);
        if (outLength <= 0)
            return [];

        var output = new float[outLength];
        var step = (double)fromRate / toRate;
        for (var i = 0; i < outLength; i++)
        {
            var position = i * step;
            var left = (int)position;
            if (left >= input.Length)
                left = input.Length - 1;
            var right = Math.Min(left + 1, input.Length - 1);
            var fraction = (float)(position - left);
            output[i] = input[left] * (1 - fraction) + input[right] * fraction;
        }
        return output;
    }

    private static float ReadSample(ReadOnlySpan<byte> bytes, int offset, int bitsPerSample, bool ieeeFloat)
    {
        if (ieeeFloat && bitsPerSample == 32 && offset + 4 <= bytes.Length)
            return BitConverter.ToSingle(bytes.Slice(offset, 4));

        if (bitsPerSample == 16 && offset + 2 <= bytes.Length)
            return BitConverter.ToInt16(bytes.Slice(offset, 2)) / 32768f;

        if (bitsPerSample == 32 && offset + 4 <= bytes.Length)
            return BitConverter.ToInt32(bytes.Slice(offset, 4)) / 2147483648f;

        return 0;
    }
}
