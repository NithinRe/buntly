namespace Buntly.Core;

public enum OverlayStyle
{
    Bead,
}

public readonly record struct OverlaySize(double Width, double Height);

/// <summary>
/// Sizes and timings copied from the Mac bead style in <c>OverlayTokens.swift</c>.
/// Windows draws its own pill. These numbers are what make the motion match.
/// </summary>
public static class OverlayMetrics
{
    public const double IdleWidth = 9;
    public const double IdleHeight = 9;
    public const double ListeningWidth = 68;
    public const double ListeningHeight = 28;
    public const double HandsFreeWidth = 112;
    public const double HandsFreeHeight = 28;
    public const double TranscribingWidth = 58;
    public const double TranscribingHeight = 28;
    public const double MessageWidth = 46;
    public const double MessageHeight = 28;
    public const double ReviewWidth = 280;

    public const int BarCount = 11;
    public const double BarWidth = 2;
    public const double BarMaxHeight = 12;
    public const double BarSpacing = 2.5;
    public const float RmsFullScale = 0.035f;
    public const float MeterSilenceFloor = 0.0012f;
    public const float LevelDecay = 0.75f;

    public const double MorphSeconds = 0.28;
    public const double MessageHoldSeconds = 1.8;
    public const double ReviewHoldSeconds = 9;

    public static readonly double[] BarWeights =
        [0.82, 0.90, 0.96, 1, 1, 1, 1, 1, 0.96, 0.90, 0.82];

    public static OverlaySize SizeFor(PillKind kind) => kind switch
    {
        PillKind.Idle => new OverlaySize(IdleWidth, IdleHeight),
        PillKind.Listening => new OverlaySize(ListeningWidth, ListeningHeight),
        PillKind.HandsFree => new OverlaySize(HandsFreeWidth, HandsFreeHeight),
        PillKind.Transcribing => new OverlaySize(TranscribingWidth, TranscribingHeight),
        PillKind.Review => new OverlaySize(ReviewWidth, 152),
        _ => new OverlaySize(MessageWidth, MessageHeight),
    };
}

public enum PillKind
{
    Idle,
    Listening,
    HandsFree,
    Transcribing,
    TooQuiet,
    NothingHeard,
    Problem,
    Review,
}
