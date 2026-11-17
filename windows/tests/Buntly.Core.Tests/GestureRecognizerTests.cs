namespace Buntly.Core.Tests;

public class GestureRecognizerTests
{
    [Fact]
    public void A_hold_starts_and_then_transcribes()
    {
        var gestures = new GestureRecognizer();
        Assert.Equal(GestureIntent.StartRecording, gestures.Handle(isDown: true, at: 0));
        Assert.Equal(GestureIntent.StopAndTranscribe, gestures.Handle(isDown: false, at: 0.5));
    }

    [Fact]
    public void A_tap_is_only_a_fragment()
    {
        var gestures = new GestureRecognizer();
        Assert.Equal(GestureIntent.StartRecording, gestures.Handle(isDown: true, at: 0));
        Assert.Equal(GestureIntent.DiscardFragment, gestures.Handle(isDown: false, at: 0.1));
    }

    [Fact]
    public void Two_taps_lock_hands_free()
    {
        var gestures = new GestureRecognizer();
        Assert.Equal(GestureIntent.StartRecording, gestures.Handle(isDown: true, at: 0));
        Assert.Equal(GestureIntent.DiscardFragment, gestures.Handle(isDown: false, at: 0.1));
        Assert.Equal(GestureIntent.StartRecording, gestures.Handle(isDown: true, at: 0.2));
        Assert.Equal(GestureIntent.LockHandsFree, gestures.Handle(isDown: false, at: 0.3));
        Assert.True(gestures.IsHandsFree);
    }

    [Fact]
    public void A_tap_after_hands_free_stops()
    {
        var gestures = new GestureRecognizer();
        gestures.Handle(isDown: true, at: 0);
        gestures.Handle(isDown: false, at: 0.1);
        gestures.Handle(isDown: true, at: 0.2);
        gestures.Handle(isDown: false, at: 0.3);

        Assert.Null(gestures.Handle(isDown: true, at: 1.0));
        Assert.Equal(GestureIntent.StopAndTranscribe, gestures.Handle(isDown: false, at: 1.1));
        Assert.False(gestures.IsHandsFree);
    }

    [Fact]
    public void Key_repeat_while_held_does_nothing()
    {
        var gestures = new GestureRecognizer();
        Assert.Equal(GestureIntent.StartRecording, gestures.Handle(isDown: true, at: 0));
        Assert.Null(gestures.Handle(isDown: true, at: 0.05));
        Assert.Null(gestures.Handle(isDown: true, at: 0.1));
    }

    [Fact]
    public void Auto_lock_ignores_the_release_of_the_hold_that_locked_it()
    {
        var gestures = new GestureRecognizer();
        Assert.Equal(GestureIntent.StartRecording, gestures.Handle(isDown: true, at: 0));
        Assert.Equal(GestureIntent.LockHandsFree, gestures.AutoLockFromHold());
        Assert.Null(gestures.Handle(isDown: false, at: 2.1));
        Assert.True(gestures.IsHandsFree);

        Assert.Null(gestures.Handle(isDown: true, at: 3));
        Assert.Equal(GestureIntent.StopAndTranscribe, gestures.Handle(isDown: false, at: 3.1));
    }
}
