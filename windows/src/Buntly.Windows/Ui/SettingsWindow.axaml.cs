using Avalonia.Controls;
using Avalonia.Layout;
using Buntly.Core;

namespace Buntly.Windows.Ui;

internal sealed class SettingsWindow : Window
{
    private readonly DictationSession _session;
    private readonly StackPanel _rows = new() { Spacing = 8 };

    public SettingsWindow(DictationSession session)
    {
        _session = session;
        Title = "Buntly settings";
        Width = 520;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Classes.Add("buntly");

        var key = new Button { Content = "Change dictation key" };
        key.Click += (_, _) => _session.BeginHotkeyCapture();

        var top = new CheckBox { Content = "Show the pill at the top of the screen" };
        top.IsChecked = session.Settings.OverlayAtTop;
        top.IsCheckedChanged += (_, _) =>
        {
            session.Settings.OverlayAtTop = top.IsChecked == true;
            session.SaveSettings();
        };

        var history = new CheckBox { Content = "Keep a local history of what you dictate" };
        history.IsChecked = session.Settings.HistoryEnabled;
        history.IsCheckedChanged += (_, _) =>
        {
            session.Settings.HistoryEnabled = history.IsChecked == true;
            session.SaveSettings();
        };

        var startup = new CheckBox { Content = "Start Buntly when I sign in" };
        startup.IsChecked = session.Settings.StartWithWindows;
        startup.IsCheckedChanged += (_, _) =>
        {
            session.Settings.StartWithWindows = startup.IsChecked == true;
            session.SaveSettings();
        };

        var retention = new ComboBox
        {
            ItemsSource = new[] { "Keep until I clear it", "7 days", "30 days", "90 days" },
            SelectedIndex = RetentionIndex(session.Settings.HistoryRetentionDays),
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        retention.SelectionChanged += (_, _) =>
        {
            session.Settings.HistoryRetentionDays = retention.SelectedIndex switch
            {
                1 => 7,
                2 => 30,
                3 => 90,
                _ => 0,
            };
            session.SaveSettings();
        };

        var wrong = new TextBox { Watermark = "What Whisper wrote" };
        var correct = new TextBox { Watermark = "What you meant" };
        var add = new Button { Content = "Add correction" };
        add.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(wrong.Text) || string.IsNullOrWhiteSpace(correct.Text))
                return;
            session.Settings.UserCorrections.Add(new UserCorrection(wrong.Text.Trim(), correct.Text.Trim()));
            wrong.Text = "";
            correct.Text = "";
            session.SaveSettings();
            RebuildRows();
        };

        var keyLabel = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        void RefreshKey() => keyLabel.Text = $"Dictation key: {session.HotkeyLabel}. Hold to talk. Fn on a laptop usually cannot be used.";
        session.Changed += RefreshKey;
        Closed += (_, _) => session.Changed -= RefreshKey;
        RefreshKey();

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(28),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Settings", FontSize = 20 },
                    keyLabel,
                    key,
                    top,
                    history,
                    new TextBlock { Text = "How long to keep history" },
                    retention,
                    startup,
                    new TextBlock { Text = "Your vocabulary", FontSize = 16 },
                    new TextBlock
                    {
                        Text = "Built-in fixes (LIHTC, ASHRAE, and the rest) always apply. Rows you add are extra. A row only replaces a whole word, so it cannot rewrite a longer word that merely contains those letters.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Opacity = 0.85,
                    },
                    wrong,
                    correct,
                    add,
                    _rows,
                },
            },
        };
        RebuildRows();
    }

    private void RebuildRows()
    {
        _rows.Children.Clear();
        foreach (var entry in _session.Settings.UserCorrections.ToList())
        {
            var remove = new Button { Content = "Remove" };
            var captured = entry;
            remove.Click += (_, _) =>
            {
                _session.Settings.UserCorrections.Remove(captured);
                _session.SaveSettings();
                RebuildRows();
            };
            _rows.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"{captured.Wrong}  →  {captured.Correct}", VerticalAlignment = VerticalAlignment.Center },
                    remove,
                },
            });
        }
    }

    private static int RetentionIndex(int days) => days switch
    {
        7 => 1,
        30 => 2,
        90 => 3,
        _ => 0,
    };
}
