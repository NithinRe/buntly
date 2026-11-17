using System.Diagnostics;
using Avalonia.Threading;
using Buntly.Core;
using Buntly.Speech;
using Buntly.Windows.Platform;

namespace Buntly.Windows;

/// <summary>
/// Hold, double-tap, transcribe, paste. The windows watch <see cref="Pill"/>
/// and <see cref="Level"/>. This class owns the microphone and the model so
/// those two things only exist once.
/// </summary>
public sealed class DictationSession : IDisposable
{
    private readonly KeyboardHook _hook = new();
    private readonly MicCapture _mic = new();
    private readonly WhisperTranscriber _whisper = new();
    private readonly GestureRecognizer _gestures = new();
    private readonly HistoryLog _history;
    private readonly object _levelGate = new();
    private DispatcherTimer? _autoLock;
    private DispatcherTimer? _discard;
    private DispatcherTimer? _returnToIdle;
    private CancellationTokenSource? _transcribe;
    private string? _foregroundApp;
    private bool _started;
    private bool _disposed;

    public DictationSession()
    {
        Paths = AppPaths.ForCurrentUser();
        Settings = AppSettings.Load(Paths.SettingsFile);
        _history = new HistoryLog(Paths.HistoryDirectory);
        _hook.Hotkey = Settings.HotkeyVirtualKey;
        Status = "Starting…";
    }

    public AppPaths Paths { get; }
    public AppSettings Settings { get; }
    public PillKind Pill { get; private set; } = PillKind.Idle;
    public string? ReviewText { get; private set; }
    public string Status { get; private set; }
    public bool ModelReady { get; private set; }
    public bool ModelLoadStarted { get; private set; }
    public bool Downloading { get; private set; }
    public double DownloadFraction { get; private set; }
    public string? DownloadError { get; private set; }
    public bool CaptureNextHotkey { get; private set; }
    public DateTimeOffset? ReviewUntil { get; private set; }

    public float Level { get; private set; }

    public string HotkeyLabel => VirtualKeyNames.Label(Settings.HotkeyVirtualKey);

    public double ReviewFraction
    {
        get
        {
            if (ReviewUntil is not DateTimeOffset until)
                return 0;
            var left = (until - DateTimeOffset.Now).TotalSeconds;
            return Math.Clamp(left / OverlayMetrics.ReviewHoldSeconds, 0, 1);
        }
    }

    public event Action? Changed;

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _history.Prune(Settings.HistoryRetentionDays, DateTimeOffset.Now);
        var hookError = _hook.Install(OnKey);
        if (hookError is not null)
        {
            Status = hookError;
            Log(hookError);
        }

        if (WhisperModel.IsPresent(Paths.ModelFile))
            _ = LoadModelAsync(downloadIfMissing: false);
        Notify();
    }

    public void BeginSetupDownload() => _ = LoadModelAsync(downloadIfMissing: true);

    public void BeginHotkeyCapture()
    {
        CaptureNextHotkey = true;
        _hook.CaptureNext = true;
        Status = "Press the key you want. Esc cancels. Fn usually never arrives.";
        Notify();
    }

    public void SaveSettings()
    {
        Settings.Save(Paths.SettingsFile);
        if (OperatingSystem.IsWindows())
            StartupRegistration.Apply(Settings.StartWithWindows);
        _hook.Hotkey = Settings.HotkeyVirtualKey;
        _history.Prune(Settings.HistoryRetentionDays, DateTimeOffset.Now);
        Notify();
    }

    public IReadOnlyList<HistoryEntry> ReadHistory() => _history.ReadAll();

    public void ClearHistory()
    {
        _history.Clear();
        Notify();
    }

    public bool ConfirmHistory(IntPtr owner, out string? failure) =>
        WindowsHello.Confirm(owner, out failure);

    public void CancelToReview()
    {
        if (Pill != PillKind.HandsFree)
            return;
        CancelTimers();
        _gestures.Reset();
        Finish(reviewOnly: true, allowShort: true);
    }

    public void CopyReview()
    {
        var text = ReviewText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetPill(PillKind.Idle, null);
            return;
        }
        PasteAtCursor.TrySetText(text);
        Record("copied", text, 0);
        SetPill(PillKind.Idle, null);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CancelTimers();
        _transcribe?.Cancel();
        _mic.Dispose();
        _hook.Dispose();
        _whisper.Dispose();
        Settings.Save(Paths.SettingsFile);
    }

    private async Task LoadModelAsync(bool downloadIfMissing)
    {
        if (ModelReady || ModelLoadStarted)
            return;
        ModelLoadStarted = true;
        DownloadError = null;
        try
        {
            if (!WhisperModel.IsPresent(Paths.ModelFile))
            {
                if (!downloadIfMissing)
                {
                    ModelLoadStarted = false;
                    Status = "The speech model is not downloaded yet.";
                    Notify();
                    return;
                }

                Downloading = true;
                Status = "Downloading the speech model…";
                Notify();
                var progress = new Progress<double>(fraction =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        DownloadFraction = fraction;
                        Status = $"Downloading the speech model… {fraction * 100:0}%";
                        Notify();
                    });
                });
                await WhisperModel.DownloadAsync(Paths.ModelFile, progress, CancellationToken.None);
                Downloading = false;
            }

            Status = "Loading the speech model…";
            Notify();
            await Task.Run(() => _whisper.Load(Paths.ModelFile));

            // The first call pays the warmup. Doing it on silence means the
            // person's first real sentence is not the slow one.
            var silence = new float[DictationGuards.SampleRate];
            await _whisper.TranscribeAsync(silence, CancellationToken.None);

            ModelReady = true;
            Status = $"Ready. Hold {HotkeyLabel}.";
            Log("Model ready.");
        }
        catch (Exception ex)
        {
            Downloading = false;
            ModelLoadStarted = false;
            DownloadError = ex.Message;
            Status = ex.Message;
            Log(ex.Message);
        }
        Notify();
    }

    private void OnKey(int virtualKey, bool isDown)
    {
        if (CaptureNextHotkey)
        {
            if (!isDown)
                return;
            CaptureNextHotkey = false;
            _hook.CaptureNext = false;
            if (virtualKey == 0x1B)
            {
                Status = $"Dictation key stays {HotkeyLabel}.";
                Notify();
                return;
            }
            Settings.HotkeyVirtualKey = virtualKey;
            _hook.Hotkey = virtualKey;
            SaveSettings();
            Status = $"Dictation key is {HotkeyLabel}.";
            Notify();
            return;
        }

        if (virtualKey == 0x1B && isDown)
        {
            CancelTake();
            return;
        }

        if (virtualKey != Settings.HotkeyVirtualKey)
            return;

        if (_gestures.Handle(isDown, MonotonicSeconds()) is GestureIntent intent)
            Apply(intent);
    }

    private void Apply(GestureIntent intent)
    {
        CancelAutoLock();
        switch (intent)
        {
            case GestureIntent.StartRecording:
                CancelDiscard();
                StartMic();
                ScheduleAutoLock();
                break;
            case GestureIntent.LockHandsFree:
                CancelDiscard();
                if (!_mic.IsRecording)
                    StartMic();
                if (!_mic.IsRecording)
                {
                    _gestures.Reset();
                    return;
                }
                SetPill(PillKind.HandsFree, null);
                Log("Hands-free on.");
                break;
            case GestureIntent.DiscardFragment:
                HoldForDoubleTap();
                break;
            case GestureIntent.StopAndTranscribe:
                CancelDiscard();
                Finish(reviewOnly: false, allowShort: false);
                break;
        }
    }

    private void StartMic()
    {
        if (!ModelReady)
        {
            SetPill(PillKind.Problem, "The speech model is still loading. Wait until the tray says Ready.");
            return;
        }

        if (_mic.IsRecording)
        {
            SetPill(_gestures.IsHandsFree ? PillKind.HandsFree : PillKind.Listening, null);
            return;
        }

        var failure = _mic.Start(OnLevel);
        if (failure is not null)
        {
            SetPill(PillKind.Problem, failure);
            Log(failure);
            return;
        }

        _foregroundApp = ForegroundApp.Name();
        SetPill(PillKind.Listening, null);
    }

    private void OnLevel(float rms)
    {
        var target = 0f;
        if (rms > OverlayMetrics.MeterSilenceFloor)
            target = Math.Clamp(rms / OverlayMetrics.RmsFullScale * 1.35f, 0, 1);

        lock (_levelGate)
        {
            Level = target > Level ? target : Level * OverlayMetrics.LevelDecay;
        }
    }

    private void HoldForDoubleTap()
    {
        _discard?.Stop();
        _discard = new DispatcherTimer { Interval = TimeSpan.FromSeconds(GestureRecognizer.DoubleTapWindow) };
        _discard.Tick += (_, _) =>
        {
            _discard?.Stop();
            if (_gestures.IsHandsFree)
                return;
            if (_mic.IsRecording)
                _mic.Stop();
            SetPill(PillKind.Idle, null);
        };
        _discard.Start();
    }

    private void Finish(bool reviewOnly, bool allowShort)
    {
        var deviceSamples = _mic.Stop();
        var samples = PcmConvert.ResampleLinear(deviceSamples, _mic.SampleRate, DictationGuards.SampleRate);
        var seconds = DictationGuards.DurationSeconds(samples.Length, DictationGuards.SampleRate);
        var app = _foregroundApp;
        _foregroundApp = null;

        if (samples.Length == 0 || (!allowShort && seconds < 0.05))
        {
            Record("cancelled", "", seconds, app);
            SetPill(PillKind.Idle, null);
            return;
        }

        var rejected = DictationGuards.RejectRecording(samples, DictationGuards.SampleRate);
        if (rejected == DiscardReason.TooShort && !allowShort)
        {
            Record("cancelled", "", seconds, app);
            SetPill(PillKind.Idle, null);
            return;
        }
        if (rejected == DiscardReason.TooQuiet)
        {
            Record("tooQuiet", "", seconds, app);
            SetPill(PillKind.TooQuiet, null);
            ReturnToIdleAfter(OverlayMetrics.MessageHoldSeconds);
            return;
        }

        SetPill(PillKind.Transcribing, null);
        _transcribe?.Cancel();
        _transcribe = new CancellationTokenSource();
        var token = _transcribe.Token;
        _ = TranscribeAsync(samples, seconds, app, reviewOnly, token);
    }

    private async Task TranscribeAsync(
        float[] samples,
        double seconds,
        string? app,
        bool reviewOnly,
        CancellationToken cancellationToken)
    {
        string raw;
        try
        {
            raw = await _whisper.TranscribeAsync(samples, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Ui(() =>
            {
                Record("cancelled", "", seconds, app);
                SetPill(PillKind.Idle, null);
            });
            return;
        }
        catch (Exception ex)
        {
            Log("Transcription failed. " + ex.Message);
            await Ui(() =>
            {
                SetPill(PillKind.Problem, "Transcription failed. " + ex.Message);
                ReturnToIdleAfter(OverlayMetrics.MessageHoldSeconds);
            });
            return;
        }

        var text = VocabularyCorrector.Apply(raw, Settings.UserCorrections);
        await Ui(() => Deliver(text, seconds, app, reviewOnly));
    }

    private void Deliver(string text, double seconds, string? app, bool reviewOnly)
    {
        if (DictationGuards.RejectTranscript(text) is not null && !reviewOnly)
        {
            Record("hallucination", text, seconds, app);
            SetPill(PillKind.NothingHeard, null);
            ReturnToIdleAfter(OverlayMetrics.MessageHoldSeconds);
            return;
        }

        if (reviewOnly)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Record("hallucination", text, seconds, app);
                SetPill(PillKind.NothingHeard, null);
                ReturnToIdleAfter(OverlayMetrics.MessageHoldSeconds);
                return;
            }
            Record("cancelled", text, seconds, app);
            ReviewText = text;
            ReviewUntil = DateTimeOffset.Now.AddSeconds(OverlayMetrics.ReviewHoldSeconds);
            SetPill(PillKind.Review, null);
            ReturnToIdleAfter(OverlayMetrics.ReviewHoldSeconds);
            return;
        }

        var pasteError = PasteAtCursor.Paste(text);
        if (pasteError is null)
        {
            Record("pasted", text, seconds, app);
            SetPill(PillKind.Idle, $"Ready. Hold {HotkeyLabel}.");
            return;
        }

        Record("copied", text, seconds, app);
        SetPill(PillKind.Problem, pasteError);
        ReturnToIdleAfter(OverlayMetrics.MessageHoldSeconds);
    }

    private void CancelTake()
    {
        var wasActive = _mic.IsRecording || Pill is PillKind.Transcribing or PillKind.Review or PillKind.HandsFree;
        CancelTimers();
        _transcribe?.Cancel();
        var seconds = 0d;
        if (_mic.IsRecording)
        {
            var samples = _mic.Stop();
            seconds = samples.Length / (double)Math.Max(1, _mic.SampleRate);
        }
        _gestures.Reset();
        ReviewText = null;
        ReviewUntil = null;
        if (wasActive)
            Record("cancelled", "", seconds, _foregroundApp);
        _foregroundApp = null;
        SetPill(PillKind.Idle, $"Ready. Hold {HotkeyLabel}.");
    }

    private void ScheduleAutoLock()
    {
        _autoLock?.Stop();
        _autoLock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(GestureRecognizer.AutoLockSeconds) };
        _autoLock.Tick += (_, _) =>
        {
            _autoLock?.Stop();
            if (_gestures.AutoLockFromHold() == GestureIntent.LockHandsFree)
            {
                SetPill(PillKind.HandsFree, null);
                Log("Auto-lock after hold.");
            }
        };
        _autoLock.Start();
    }

    private void ReturnToIdleAfter(double seconds)
    {
        _returnToIdle?.Stop();
        _returnToIdle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _returnToIdle.Tick += (_, _) =>
        {
            _returnToIdle?.Stop();
            ReviewText = null;
            ReviewUntil = null;
            if (Pill is PillKind.TooQuiet or PillKind.NothingHeard or PillKind.Problem or PillKind.Review)
                SetPill(PillKind.Idle, ModelReady ? $"Ready. Hold {HotkeyLabel}." : Status);
        };
        _returnToIdle.Start();
    }

    private void CancelAutoLock() => _autoLock?.Stop();

    private void CancelDiscard() => _discard?.Stop();

    private void CancelTimers()
    {
        _autoLock?.Stop();
        _discard?.Stop();
        _returnToIdle?.Stop();
    }

    private void SetPill(PillKind kind, string? status)
    {
        Pill = kind;
        if (kind != PillKind.Review)
        {
            if (kind != PillKind.Problem)
                ReviewText = null;
        }
        if (kind is PillKind.Idle or PillKind.Listening)
            lock (_levelGate)
                Level = kind == PillKind.Idle ? 0 : Level;

        _hook.Armed = kind is PillKind.Listening or PillKind.HandsFree or PillKind.Transcribing or PillKind.Review;
        if (status is not null)
            Status = status;
        Notify();
    }

    private void Record(string outcome, string text, double seconds, string? app = null)
    {
        if (!Settings.HistoryEnabled)
            return;
        var entry = new HistoryEntry(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            text,
            seconds,
            app ?? _foregroundApp,
            WhisperModelNames.HistoryModelId,
            outcome);
        var failure = _history.Append(entry);
        if (failure is not null)
            Log(failure);
    }

    private void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Paths.Root);
            File.AppendAllText(Paths.LogFile, $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Dictation must not stop because the log file is locked.
        }
    }

    private void Notify() => Changed?.Invoke();

    private static Task Ui(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();

    private static double MonotonicSeconds() =>
        Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
