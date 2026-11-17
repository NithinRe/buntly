using System.Text;
using Whisper.net;

namespace Buntly.Speech;

/// <summary>
/// One loaded Whisper turbo model. Creating it is the slow warmup. Calling
/// <see cref="TranscribeAsync"/> on later takes is the fast path. Not safe
/// to call from two threads at once. The session holds a single lock.
/// </summary>
public sealed class WhisperTranscriber : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private bool _disposed;

    public bool IsLoaded => _processor is not null;

    public void Load(string modelPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"The speech model is not on disk at {modelPath}. Open Buntly and let the checklist finish the Hugging Face download.",
                modelPath);
        }

        _processor?.Dispose();
        _factory?.Dispose();
        _factory = WhisperFactory.FromPath(modelPath);
        _processor = _factory.CreateBuilder()
            .WithLanguage("en")
            .Build();
    }

    public async Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_processor is null)
        {
            throw new InvalidOperationException(
                "Whisper is not loaded. WhisperTranscriber.Load has to run after the model download, before the first dictation.");
        }

        var text = new StringBuilder();
        await foreach (var segment in _processor.ProcessAsync(samples, cancellationToken))
            text.Append(segment.Text);
        return text.ToString().Trim();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _processor?.Dispose();
        _factory?.Dispose();
        _processor = null;
        _factory = null;
    }
}
