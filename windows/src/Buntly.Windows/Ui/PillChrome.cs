using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Buntly.Core;

namespace Buntly.Windows.Ui;

/// <summary>
/// Draws the bead, the listening meter, the hands-free ✕, and the review
/// card. Sizes come from <see cref="OverlayMetrics"/>, which copies the Mac
/// bead. Words stay off the pill except on the review card, which exists
/// so a cancelled hands-free take can still be copied.
/// </summary>
internal sealed class PillChrome : Control
{
    private static readonly IBrush Body = new SolidColorBrush(Color.Parse("#000000"));
    private static readonly IBrush Rim = new SolidColorBrush(Color.Parse("#24FFFFFF"));
    private static readonly IBrush Tick = Brushes.White;
    private static readonly IBrush HandsFree = new SolidColorBrush(Color.Parse("#C3A2F5"));
    private static readonly IBrush Working = new SolidColorBrush(Color.Parse("#B3CCF0"));
    private static readonly IBrush Danger = new SolidColorBrush(Color.Parse("#FF453A"));
    private static readonly IBrush CopyFill = new SolidColorBrush(Color.Parse("#EBC3A2F5"));
    private static readonly IBrush CopyText = new SolidColorBrush(Color.Parse("#101014"));

    private readonly DictationSession _session;
    private Rect _cancelDisc;
    private Rect _copyButton;
    private double _phase;

    public PillChrome(DictationSession session)
    {
        _session = session;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        if (_session.Pill == PillKind.HandsFree && _cancelDisc.Contains(point))
        {
            _session.CancelToReview();
            e.Handled = true;
        }
        else if (_session.Pill == PillKind.Review && _copyButton.Contains(point))
        {
            _session.CopyReview();
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        _phase += 0.35;
        var size = OverlayMetrics.SizeFor(_session.Pill);
        var origin = new Point((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2);
        var rect = new Rect(origin, new Size(size.Width, size.Height));
        var radius = Math.Min(size.Width, size.Height) / 2;
        var pen = new Pen(RimBrush(), 1);
        context.DrawRectangle(Body, pen, rect, radius, radius);

        switch (_session.Pill)
        {
            case PillKind.Listening:
            case PillKind.HandsFree:
                DrawMeter(context, rect);
                break;
            case PillKind.Transcribing:
                DrawDots(context, rect);
                break;
            case PillKind.TooQuiet:
                DrawMark(context, rect, "mic");
                break;
            case PillKind.NothingHeard:
                DrawMark(context, rect, "?");
                break;
            case PillKind.Problem:
                DrawMark(context, rect, "!");
                break;
            case PillKind.Review:
                DrawReview(context, rect);
                break;
        }

        if (_session.Pill == PillKind.HandsFree)
            DrawCancel(context, rect);
        else
            _cancelDisc = default;
    }

    private IBrush RimBrush() => _session.Pill switch
    {
        PillKind.HandsFree => HandsFree,
        PillKind.Transcribing => Working,
        PillKind.Problem or PillKind.TooQuiet => Danger,
        _ => Rim,
    };

    private void DrawMeter(DrawingContext context, Rect rect)
    {
        var count = OverlayMetrics.BarCount;
        var total = count * OverlayMetrics.BarWidth + (count - 1) * OverlayMetrics.BarSpacing;
        var left = rect.X + 8;
        if (_session.Pill == PillKind.HandsFree)
            left = rect.X + 8;
        var bottom = rect.Center.Y + OverlayMetrics.BarMaxHeight / 2;
        for (var i = 0; i < count; i++)
        {
            var dance = 0.65 + 0.35 * Math.Sin(_phase + i * 0.7);
            var height = Math.Max(2, OverlayMetrics.BarMaxHeight * _session.Level * OverlayMetrics.BarWeights[i] * dance);
            var x = left + i * (OverlayMetrics.BarWidth + OverlayMetrics.BarSpacing);
            var bar = new Rect(x, bottom - height, OverlayMetrics.BarWidth, height);
            context.DrawRectangle(Tick, null, bar, 1, 1);
        }
    }

    private void DrawDots(DrawingContext context, Rect rect)
    {
        for (var i = 0; i < 3; i++)
        {
            var bob = 0.4 + 0.6 * Math.Abs(Math.Sin(_phase - i * 0.6));
            var x = rect.Center.X + (i - 1) * 8;
            context.DrawEllipse(Working, null, new Point(x, rect.Center.Y), 2.2, 2.2 * bob + 1);
        }
    }

    private void DrawMark(DrawingContext context, Rect rect, string mark)
    {
        var text = new FormattedText(mark, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 12, Brushes.White);
        context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Center.Y - text.Height / 2));
    }

    private void DrawCancel(DrawingContext context, Rect rect)
    {
        var diameter = 18;
        var disc = new Rect(rect.Right - 8 - diameter, rect.Center.Y - diameter / 2, diameter, diameter);
        _cancelDisc = disc.Inflate(4);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#14FFFFFF")), new Pen(Brushes.White, 1), disc.Center, diameter / 2, diameter / 2);
        var span = 3.5;
        var pen = new Pen(Brushes.White, 1.5);
        context.DrawLine(pen, disc.Center + new Vector(-span, -span), disc.Center + new Vector(span, span));
        context.DrawLine(pen, disc.Center + new Vector(span, -span), disc.Center + new Vector(-span, span));
    }

    private void DrawReview(DrawingContext context, Rect rect)
    {
        var text = _session.ReviewText ?? "";
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 13, Brushes.White)
        {
            MaxTextWidth = rect.Width - 24,
            MaxTextHeight = rect.Height - 52,
        };
        context.DrawText(formatted, new Point(rect.X + 12, rect.Y + 10));

        _copyButton = new Rect(rect.Right - 84, rect.Bottom - 36, 72, 26);
        context.DrawRectangle(CopyFill, null, _copyButton, 8, 8);
        var label = new FormattedText("Copy", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 12, CopyText);
        context.DrawText(label, new Point(_copyButton.Center.X - label.Width / 2, _copyButton.Center.Y - label.Height / 2));

        var fraction = _session.ReviewFraction;
        var track = new Rect(rect.X + 12, rect.Bottom - 8, rect.Width - 24, 2);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#1FFFFFFF")), null, track);
        context.DrawRectangle(HandsFree, null, new Rect(track.X, track.Y, track.Width * fraction, track.Height));
    }
}
