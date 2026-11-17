namespace Buntly.Core;

/// <summary>The one key this recogniser knows about. The Windows hook decides which physical key that is.</summary>
public enum HotKey
{
    Dictation,
}

/// <summary>What the rest of the app should do with one key event. Null means "not yet".</summary>
public enum GestureIntent
{
    StartRecording,
    StopAndTranscribe,
    DiscardFragment,
    LockHandsFree,
}

/// <summary>
/// Hold, tap, double-tap, and tap-to-stop. Ported from
/// <c>GestureRecognizer.swift</c>. It never reads the clock. The caller
/// passes timestamps, so tests can run without a keyboard.
/// A hold of <see cref="AutoLockSeconds"/> is turned into hands-free by
/// the caller via <see cref="AutoLockFromHold"/>, same as the Mac app.
/// </summary>
public sealed class GestureRecognizer
{
    public const double TapMaxSeconds = 0.25;
    public const double DoubleTapWindow = 0.40;
    public const double AutoLockSeconds = 2.0;

    private bool _keyDown;
    private double _pressedAt;
    private double? _lastTapAt;
    private bool _ignoreNextHandsFreeRelease;

    public bool IsHandsFree { get; private set; }

    public void Reset()
    {
        _keyDown = false;
        _lastTapAt = null;
        IsHandsFree = false;
        _ignoreNextHandsFreeRelease = false;
    }

    /// <summary>
    /// The caller decided a hold has lasted long enough to lock.
    /// The release of that same hold must not stop the session.
    /// </summary>
    public GestureIntent? AutoLockFromHold()
    {
        if (!_keyDown || IsHandsFree)
            return null;

        IsHandsFree = true;
        _lastTapAt = null;
        _ignoreNextHandsFreeRelease = true;
        return GestureIntent.LockHandsFree;
    }

    public GestureIntent? Handle(bool isDown, double at)
    {
        return isDown ? HandlePress(at) : HandleRelease(at);
    }

    private GestureIntent? HandlePress(double at)
    {
        // Key-repeat and a second press while one is already down do nothing.
        if (_keyDown)
            return null;

        _keyDown = true;
        _pressedAt = at;

        // Hands-free is already recording. This press is only remembered so
        // its release can end the session. Starting another capture here
        // would run two microphones at once.
        if (IsHandsFree)
            return null;

        return GestureIntent.StartRecording;
    }

    private GestureIntent? HandleRelease(double at)
    {
        if (!_keyDown)
            return null;

        _keyDown = false;

        if (IsHandsFree)
        {
            if (_ignoreNextHandsFreeRelease)
            {
                // Thumb came off after auto-lock. Keep recording.
                _ignoreNextHandsFreeRelease = false;
                _lastTapAt = null;
                return null;
            }

            IsHandsFree = false;
            _lastTapAt = null;
            return GestureIntent.StopAndTranscribe;
        }

        var heldFor = at - _pressedAt;
        if (heldFor >= TapMaxSeconds)
        {
            _lastTapAt = null;
            return GestureIntent.StopAndTranscribe;
        }

        if (_lastTapAt is double last && at - last <= DoubleTapWindow)
        {
            IsHandsFree = true;
            _lastTapAt = null;
            return GestureIntent.LockHandsFree;
        }

        _lastTapAt = at;
        return GestureIntent.DiscardFragment;
    }
}
