namespace Buntly.Core;

/// <summary>
/// Where Buntly keeps its files on this PC. Local AppData, never Documents,
/// so OneDrive does not upload the model or the transcripts. The history
/// folder name ends in <c>.noindex</c> for the same reason the Mac folder
/// does: search should not index dictation fragments.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Buntly storage folder is missing. AppPaths was given an empty path.", nameof(root));
        Root = root;
    }

    public string Root { get; }

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string ModelFile => Path.Combine(Root, "models", WhisperModelNames.FileName);

    public string HistoryDirectory => Path.Combine(Root, "history.noindex");

    public string LogFile => Path.Combine(Root, "buntly.log");

    public static AppPaths ForCurrentUser()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            throw new InvalidOperationException(
                "Windows did not report a Local AppData folder, so Buntly has nowhere to store the speech model. " +
                "That value comes from Environment.SpecialFolder.LocalApplicationData.");
        }
        return new AppPaths(Path.Combine(local, "Buntly"));
    }
}

/// <summary>The Windows ggml copy of the same Whisper turbo generation the Mac app runs.</summary>
public static class WhisperModelNames
{
    public const string FileName = "ggml-large-v3-turbo.bin";

    /// <summary>Written on every history row so a later comparison knows which file produced it.</summary>
    public const string HistoryModelId = "ggml-large-v3-turbo";

    /// <summary>
    /// Pinned revision, not the moving main branch. This is the classic
    /// (not quantized) turbo file Whisper.net 1.9 downloads from Hugging Face.
    /// </summary>
    public const string DownloadUrl =
        "https://huggingface.co/sandrohanea/whisper.net/resolve/v5/classic/ggml-large-v3-turbo.bin";

    /// <summary>
    /// SHA-256 of that exact file. It is the Git LFS id Hugging Face published
    /// for revision v5, path classic/ggml-large-v3-turbo.bin. The download is
    /// kept only when the bytes match. A changed file at that URL must be
    /// pinned again on purpose.
    /// </summary>
    public const string Sha256 = "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69";

    /// <summary>Byte size of the same pinned file. About 1.51 GB.</summary>
    public const long ExpectedBytes = 1_624_555_275;
}
