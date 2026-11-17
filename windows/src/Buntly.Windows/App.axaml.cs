using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Buntly.Speech;
using Buntly.Windows.Ui;

namespace Buntly.Windows;

public sealed class App : Application
{
    private DictationSession? _session;
    private TrayIcon? _tray;
    private PillWindow? _pill;
    private ChecklistWindow? _checklist;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Closing the checklist must not quit the process. Quit is a tray item.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _session = new DictationSession();
            _session.Changed += RefreshTrayAndPill;
            SetupTray(desktop);
            _session.Start();
            if (!_session.Settings.ChecklistComplete || !WhisperModel.IsPresent(_session.Paths.ModelFile))
                ShowChecklist();
            RefreshTrayAndPill();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var history = new NativeMenuItem("History");
        history.Click += (_, _) => OpenHistory();
        var settings = new NativeMenuItem("Settings");
        settings.Click += (_, _) => OpenSettings();
        var setup = new NativeMenuItem("Setup");
        setup.Click += (_, _) => ShowChecklist();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) =>
        {
            _pill?.Close();
            _session?.Dispose();
            desktop.Shutdown();
        };

        _tray = new TrayIcon
        {
            ToolTipText = "Buntly",
            Menu = new NativeMenu
            {
                history,
                settings,
                setup,
                new NativeMenuItemSeparator(),
                quit,
            },
        };

        try
        {
            _tray.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Buntly/Assets/menubar.png")));
        }
        catch (Exception)
        {
            // The menu still opens from the tray slot. The mark is optional.
        }

        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    private void RefreshTrayAndPill()
    {
        if (_session is null || _tray is null)
            return;

        _tray.ToolTipText = string.IsNullOrWhiteSpace(_session.Status) ? "Buntly" : _session.Status;
        if (!_session.ModelReady)
            return;

        _pill ??= new PillWindow(_session);
        if (!_pill.IsVisible)
            _pill.Show();
    }

    private void ShowChecklist()
    {
        if (_session is null)
            return;
        if (_checklist is { IsVisible: true })
        {
            _checklist.Activate();
            return;
        }

        _checklist = new ChecklistWindow(_session);
        _checklist.Closed += (_, _) => _checklist = null;
        _checklist.Show();
    }

    private void OpenHistory()
    {
        if (_session is null)
            return;
        new HistoryWindow(_session).Show();
    }

    private void OpenSettings()
    {
        if (_session is null)
            return;
        new SettingsWindow(_session).Show();
    }
}
