using Avalonia.Controls;
using Avalonia.Layout;
using Buntly.Core;
using Buntly.Windows.Platform;

namespace Buntly.Windows.Ui;

/// <summary>
/// History stays behind the Windows confirmation prompt. The list is filled
/// only after that prompt succeeds. Audio is not in this list.
/// </summary>
internal sealed class HistoryWindow : Window
{
    private readonly DictationSession _session;
    private readonly StackPanel _body = new() { Spacing = 10 };

    public HistoryWindow(DictationSession session)
    {
        _session = session;
        Title = "Buntly history";
        Width = 560;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Classes.Add("buntly");
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(28),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "History", FontSize = 20 },
                    _body,
                },
            },
        };
        Opened += (_, _) => Ask();
    }

    private void Ask()
    {
        _body.Children.Clear();
        if (!_session.Settings.HistoryEnabled)
        {
            _body.Children.Add(new TextBlock
            {
                Text = "History is turned off in Settings. Nothing is being saved.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            return;
        }

        var ok = _session.ConfirmHistory(WindowChrome.HandleOf(this), out var failure);
        if (!ok)
        {
            _body.Children.Add(new TextBlock
            {
                Text = failure ?? "History stays locked.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            return;
        }

        ShowEntries();
    }

    private void ShowEntries()
    {
        var clear = new Button { Content = "Clear history", HorizontalAlignment = HorizontalAlignment.Left };
        clear.Click += (_, _) =>
        {
            _session.ClearHistory();
            _body.Children.Clear();
            _body.Children.Add(new TextBlock { Text = "History cleared." });
        };
        _body.Children.Add(clear);

        var entries = _session.ReadHistory();
        if (entries.Count == 0)
        {
            _body.Children.Add(new TextBlock { Text = "Nothing dictated yet." });
            return;
        }

        foreach (var entry in entries)
            _body.Children.Add(Row(entry));
    }

    private static Control Row(HistoryEntry entry)
    {
        var copy = new Button { Content = "Copy" };
        copy.Click += (_, _) => PasteAtCursor.TrySetText(entry.Text);
        var when = entry.At.ToLocalTime().ToString("MMM d, h:mm tt");
        var where = string.IsNullOrWhiteSpace(entry.App) ? entry.Outcome : $"{entry.App} · {entry.Outcome}";
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = $"{when}  {where}", Opacity = 0.7, FontSize = 12 },
                new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Text) ? "—" : entry.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                copy,
            },
        };
    }
}
