using Buntly.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Buntly.Windows.Platform;

/// <summary>
/// Opens the default microphone for one take and closes it when the take
/// ends. A fresh capture every time, rather than reusing one that has gone
/// quiet after a device change.
/// </summary>
internal sealed class MicCapture : IDisposable
{
    private WasapiCapture? _capture;
    private MemoryStream? _bytes;
    private readonly object _gate = new();

    public bool IsRecording => _capture is not null;
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public int BitsPerSample { get; private set; }
    public bool IeeeFloat { get; private set; }

    public string? Start(Action<float> onLevel)
    {
        if (!OperatingSystem.IsWindows())
            return "The microphone capture only runs on Windows.";

        try
        {
            Stop();
            var bytes = new MemoryStream();
            var capture = new WasapiCapture();
            var format = capture.WaveFormat;
            SampleRate = format.SampleRate;
            Channels = format.Channels;
            BitsPerSample = format.BitsPerSample;
            IeeeFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;

            capture.DataAvailable += (_, args) =>
            {
                if (args.BytesRecorded <= 0)
                    return;
                var chunk = new byte[args.BytesRecorded];
                Buffer.BlockCopy(args.Buffer, 0, chunk, 0, args.BytesRecorded);
                lock (_gate)
                    bytes.Write(chunk, 0, chunk.Length);

                var mono = PcmConvert.ToMonoFloat(chunk, Channels, BitsPerSample, IeeeFloat);
                onLevel(DictationGuards.Rms(mono));
            };

            capture.StartRecording();
            _bytes = bytes;
            _capture = capture;
            return null;
        }
        catch (Exception ex)
        {
            Stop();
            return "Buntly could not open the microphone. " + ex.Message +
                   " Allow desktop apps in Windows Settings → Privacy → Microphone, then try again.";
        }
    }

    /// <summary>Mono samples at the device rate. Empty if nothing was captured.</summary>
    public float[] Stop()
    {
        byte[] raw;
        lock (_gate)
        {
            try
            {
                _capture?.StopRecording();
            }
            catch (Exception)
            {
                // The device can disappear mid-take. The bytes we already have are the take.
            }
            _capture?.Dispose();
            _capture = null;
            raw = _bytes?.ToArray() ?? [];
            _bytes?.Dispose();
            _bytes = null;
        }

        if (raw.Length == 0 || SampleRate <= 0)
            return [];
        return PcmConvert.ToMonoFloat(raw, Channels, BitsPerSample, IeeeFloat);
    }

    public void Dispose() => Stop();
}
