using System.Diagnostics;
using Buntly.Core;
using Buntly.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Buntly.SpeechCheck;

/// <summary>
/// Records a few seconds from the default microphone, runs Whisper turbo,
/// and prints how long the transcription took. Run this on the Windows
/// laptop before sending anyone the installer. There is no window on purpose.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var seconds = 10;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--seconds" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed) && parsed is > 0 and <= 60)
                seconds = parsed;
        }

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Buntly.SpeechCheck records from a Windows microphone. Run the published exe on the laptop.");
            return 1;
        }

        var paths = AppPaths.ForCurrentUser();
        Console.WriteLine($"Model file: {paths.ModelFile}");

        if (!WhisperModel.IsPresent(paths.ModelFile))
        {
            Console.WriteLine("Downloading ggml-large-v3-turbo from Hugging Face. This happens once.");
            var download = Stopwatch.StartNew();
            var progress = new Progress<double>(fraction =>
                Console.Write($"\r  {fraction * 100:0}%"));
            try
            {
                await WhisperModel.DownloadAsync(paths.ModelFile, progress, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            Console.WriteLine();
            Console.WriteLine($"Download finished in {download.Elapsed.TotalSeconds:0.0}s.");
        }
        else
        {
            Console.WriteLine("Model already on disk.");
        }

        Console.WriteLine($"Recording {seconds}s. Speak a normal sentence.");
        float[] samples;
        int capturedRate;
        try
        {
            (samples, capturedRate) = Record(seconds);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Could not open the microphone. {ex.Message} Allow desktop apps in Windows Settings → Privacy → Microphone, then run this again.");
            return 1;
        }

        var mono16k = PcmConvert.ResampleLinear(samples, capturedRate, DictationGuards.SampleRate);
        Console.WriteLine($"Captured {mono16k.Length / (double)DictationGuards.SampleRate:0.00}s of audio at 16 kHz.");

        using var whisper = new WhisperTranscriber();
        var load = Stopwatch.StartNew();
        whisper.Load(paths.ModelFile);
        Console.WriteLine($"Model load: {load.Elapsed.TotalSeconds:0.00}s.");

        var transcribe = Stopwatch.StartNew();
        var text = await whisper.TranscribeAsync(mono16k, CancellationToken.None);
        transcribe.Stop();

        Console.WriteLine($"Transcribe: {transcribe.Elapsed.TotalSeconds:0.00}s.");
        Console.WriteLine("Text:");
        Console.WriteLine(text);

        if (transcribe.Elapsed.TotalSeconds > 5 && seconds >= 8)
        {
            Console.WriteLine();
            Console.WriteLine("That is slow for talking over. Do not send the installer yet.");
            Console.WriteLine("Rebuild with Whisper.net.Runtime.Vulkan instead of Whisper.net.Runtime and run this again.");
            Console.WriteLine("The swap is described in windows/README.md.");
            return 2;
        }

        return 0;
    }

    private static (float[] Samples, int SampleRate) Record(int seconds)
    {
        using var capture = new WasapiCapture();
        var format = capture.WaveFormat;
        var chunks = new List<byte[]>();
        capture.DataAvailable += (_, eventArgs) =>
        {
            var piece = new byte[eventArgs.BytesRecorded];
            Buffer.BlockCopy(eventArgs.Buffer, 0, piece, 0, eventArgs.BytesRecorded);
            chunks.Add(piece);
        };
        capture.StartRecording();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        capture.StopRecording();

        var length = chunks.Sum(chunk => chunk.Length);
        var bytes = new byte[length];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, bytes, offset, chunk.Length);
            offset += chunk.Length;
        }

        var mono = PcmConvert.ToMonoFloat(
            bytes,
            format.Channels,
            format.BitsPerSample,
            format.Encoding == WaveFormatEncoding.IeeeFloat);
        return (mono, format.SampleRate);
    }
}
