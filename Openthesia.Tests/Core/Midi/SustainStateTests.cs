using Openthesia.Core.Midi;
using Xunit;

namespace Openthesia.Tests.Core.Midi;

public sealed class SustainStateTests
{
    [Fact]
    public void ReleasingPedalDoesNotStopNoteThatIsStillHeld()
    {
        var state = new SustainState();

        state.PressPedal();
        state.NotePressed(60);

        var notesToStop = state.ReleasePedal();

        Assert.Empty(notesToStop);
    }

    [Fact]
    public void ReleasingPedalDoesNotStopPitchWithAnotherPressStillHeld()
    {
        var state = new SustainState();

        state.PressPedal();
        state.NotePressed(60);
        state.NotePressed(60);
        state.NoteReleased(60);

        var notesToStop = state.ReleasePedal();

        Assert.Empty(notesToStop);
    }

    [Fact]
    public void ReleasingPedalStopsNoteReleasedWhileSustainWasActive()
    {
        var state = new SustainState();

        state.PressPedal();
        state.NotePressed(60);
        state.NoteReleased(60);

        var notesToStop = state.ReleasePedal();

        Assert.Equal(new[] { 60 }, notesToStop);
    }

    [Fact]
    public void HeldNoteStopsWhenKeyIsReleasedAfterPedalIsReleased()
    {
        var state = new SustainState();

        state.PressPedal();
        state.NotePressed(60);
        state.ReleasePedal();

        var shouldStopNote = state.NoteReleased(60);

        Assert.True(shouldStopNote);
    }

    [Fact]
    public void RepeatedPitchStopsOnlyAfterFinalKeyRelease()
    {
        var state = new SustainState();

        state.NotePressed(60);
        state.NotePressed(60);

        var shouldStopAfterFirstRelease = state.NoteReleased(60);
        var shouldStopAfterSecondRelease = state.NoteReleased(60);

        Assert.False(shouldStopAfterFirstRelease);
        Assert.True(shouldStopAfterSecondRelease);
    }
}
