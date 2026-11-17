using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Buntly.Core;
using Buntly.Windows.Platform;

namespace Buntly.Windows.Ui;

/// <summary>
/// The floating pill. It does not take keyboard focus. Clicks pass through
/// it except while hands-free (the ✕) or the review card (Copy) is up.
/// </summary>
internal sealed class PillWindow : Window
{
    private const double Pad = 18;
    private readonly DictationSession _session;
    private readonly PillChrome _chrome;
    private bool _clickThrough = true;

    public PillWindow(DictationSession session)
    {
        _session = session;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        CanResize = false;
        Focusable = false;
        _chrome = new PillChrome(session);
        Content = _chrome;

        Opened += (_, _) =>
        {
            WindowChrome.ApplyNoActivate(this);
            Sync(force: true);
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) => Sync(force: false);
        timer.Start();
    }

    private void Sync(bool force)
    {
        var size = OverlayMetrics.SizeFor(_session.Pill);
        var width = size.Width + Pad * 2;
        var height = size.Height + Pad * 2;
        if (Math.Abs(Width - width) > 0.5 || Math.Abs(Height - height) > 0.5 || force)
        {
            Width = width;
            Height = height;
            Place();
        }

        var needsClick = _session.Pill is PillKind.HandsFree or PillKind.Review;
        if (needsClick == _clickThrough || force)
        {
            _clickThrough = !needsClick;
            WindowChrome.SetClickThrough(this, _clickThrough);
        }

        _chrome.InvalidateVisual();
    }

    private void Place()
    {
        var screen = Screens?.Primary;
        if (screen is null)
            return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var width = (int)(Width * scale);
        var height = (int)(Height * scale);
        var margin = (int)(_session.Settings.OverlayMargin * scale);
        var x = area.X + (area.Width - width) / 2;
        var y = _session.Settings.OverlayAtTop
            ? area.Y + margin
            : area.Y + area.Height - height - margin;
        Position = new PixelPoint(x, y);
    }
}
