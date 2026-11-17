using System.Security.Cryptography;
using Buntly.Core;

namespace Buntly.Speech;

/// <summary>
/// Downloads and loads the ggml large-v3-turbo weights. The file stays in
/// Local AppData. A partial download is written beside it and only renamed
/// once the copy finishes, so a killed download is not treated as a model.
/// </summary>
public static class WhisperModel
{
    /// <summary>Anything smaller than this is a partial download, not the turbo weights.</summary>
    public const long MinimumBytes = 100_000_000;

    /// <summary>
    /// The classic turbo file is about 1.6 GB. Stop well above that so a
    /// bad response cannot fill the disk. The URL itself is pinned.
    /// </summary>
    public const long MaximumBytes = 2_200_000_000;

    public static bool IsPresent(string path) =>
        File.Exists(path) && new FileInfo(path).Length >= MinimumBytes;

    public static async Task DownloadAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException(
                $"The model path '{path}' has no folder. AppPaths.ModelFile is the path Buntly should pass.");
        }

        Directory.CreateDirectory(directory);
        var partial = path + ".partial";

        using var http = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Buntly/1.0");

        using var response = await http.GetAsync(
            WhisperModelNames.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not download the speech model from Hugging Face ({(int)response.StatusCode}). " +
                $"URL: {WhisperModelNames.DownloadUrl}. Check the internet connection and try the checklist again.");
        }

        // Hugging Face redirects the pinned URL to its file host. Follow that,
        // but refuse anything that is not HTTPS. The weights are not a secret;
        // the point is to not write a file that arrived over a plain connection.
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "The speech-model download left HTTPS. Buntly did not save the file. " +
                $"Started from {WhisperModelNames.DownloadUrl}.");
        }

        var total = response.Content.Headers.ContentLength;
        if (total is long announced && announced != WhisperModelNames.ExpectedBytes)
        {
            throw new InvalidOperationException(
                $"The speech-model server announced {announced} bytes. The pinned turbo file is {WhisperModelNames.ExpectedBytes} bytes. " +
                "Buntly did not save the file.");
        }

        string actualHash;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[256 * 1024];
                long written = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > MaximumBytes)
                    {
                        throw new InvalidOperationException(
                            $"The speech-model download passed {MaximumBytes} bytes. Buntly stopped and removed the partial file. " +
                            $"The pinned turbo file is {WhisperModelNames.ExpectedBytes} bytes.");
                    }
                    hash.AppendData(buffer.AsSpan(0, read));
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    if (total is > 0)
                        progress?.Report(Math.Clamp(written / (double)total, 0, 1));
                }
            }
            actualHash = Convert.ToHexString(hash.GetHashAndReset());
        }
        catch
        {
            // A failed or cancelled copy must not sit on disk as a multi-gigabyte
            // fragment. The next attempt starts over.
            TryDelete(partial);
            throw;
        }

        // Hugging Face sends this URL to a CDN whose host name changes.
        // The hash is the check that the bytes are the pinned file. A host
        // list would break the day that CDN name changes.
        if (!actualHash.Equals(WhisperModelNames.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(partial);
            throw new InvalidOperationException(
                "The speech-model download did not match the pinned Whisper turbo file, so Buntly removed it. " +
                "Try the checklist again. If it keeps failing, the file at the pinned URL has changed and the hash in AppPaths.cs has to be updated on purpose.");
        }

        if (!File.Exists(partial) || new FileInfo(partial).Length < MinimumBytes)
        {
            TryDelete(partial);
            throw new InvalidOperationException(
                $"The download finished but the file is too small to be Whisper turbo. It was saved as {partial} and has been removed. Try the checklist again.");
        }

        File.Move(partial, path, overwrite: true);
        progress?.Report(1);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // The next attempt overwrites the partial file.
        }
    }
}
