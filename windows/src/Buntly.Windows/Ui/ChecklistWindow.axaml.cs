using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Buntly.Speech;
using Buntly.Windows.Platform;

namespace Buntly.Windows.Ui;

/// <summary>
/// The first-launch page: microphone, dictation key, speech-model download.
/// Three rows, each with a tick when that part is done.
/// </summary>
internal sealed class ChecklistWindow : Window
{
    private readonly DictationSession _session;
    private readonly TextBlock _mic;
    private readonly TextBlock _key;
    private readonly TextBlock _model;
    private readonly ProgressBar _progress;
    private readonly TextBlock _status;
    private readonly Button _start;

    public ChecklistWindow(DictationSession session)
    {
        _session = session;
        Title = "Buntly";
        Width = 520;
        Height = 620;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Classes.Add("buntly");

        _mic = Body();
        _key = Body();
        _model = Body();
        _status = Body();
        _status.TextWrapping = TextWrapping.Wrap;
        _progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 8, IsIndeterminate = false };
        _start = new Button { Content = "Start using Buntly", Classes = { "accent" }, HorizontalAlignment = HorizontalAlignment.Left };
        _start.Click += (_, _) =>
        {
            _session.Settings.ChecklistComplete = true;
            _session.SaveSettings();
            Close();
        };

        var micButton = new Button { Content = "Open microphone settings" };
        micButton.Click += (_, _) => MicrophoneConsent.OpenSettings();
        var keyButton = new Button { Content = "Choose a different key" };
        keyButton.Click += (_, _) => _session.BeginHotkeyCapture();
        var download = new Button { Content = "Download the speech model" };
        download.Click += (_, _) => _session.BeginSetupDownload();

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(28),
                Spacing = 14,
                Children =
                {
                    Heading("Buntly"),
                    Body("Hold a key, talk, let go. The words land wherever your cursor already was. Audio stays on this PC."),
                    Heading("1. Microphone"),
                    _mic,
                    micButton,
                    Heading("2. Dictation key"),
                    _key,
                    Body("The Fn key on most Windows laptops never reaches any app, so Buntly does not use it."),
                    keyButton,
                    Heading("3. Speech model"),
                    _model,
                    _progress,
                    download,
                    _status,
                    _start,
                },
            },
        };

        _session.Changed += Refresh;
        Opened += (_, _) => Refresh();
        Closed += (_, _) => _session.Changed -= Refresh;
    }

    private void Refresh()
    {
        var micDenied = MicrophoneConsent.IsDenied();
        _mic.Text = micDenied
            ? "Microphone access for desktop apps is off. Turn it on, then come back."
            : "Microphone access looks allowed.";

        _key.Text = _session.CaptureNextHotkey
            ? "Press the key you want. Esc keeps the current one. Fn usually does nothing here."
            : $"Right now the key is {_session.HotkeyLabel}. Hold it to talk. A hold longer than two seconds locks on. Tap again to finish. Double-tap also locks on.";

        var present = WhisperModel.IsPresent(_session.Paths.ModelFile);
        _model.Text = present
            ? "Whisper is on this PC. Later dictation does not need the internet."
            : "One download from Hugging Face, about 1.6 GB. Internet is needed only for this.";
        _progress.IsVisible = _session.Downloading || (_session.DownloadFraction > 0 && !present);
        _progress.Value = _session.DownloadFraction;
        _status.Text = _session.DownloadError ?? _session.Status;
        _start.IsEnabled = present && _session.ModelReady;
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeight.SemiBold,
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.9,
    };

    private static TextBlock Body() => Body("");
}
