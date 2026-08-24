using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeGuidanceTests
{
    [Fact]
    public void CountInFreezesChartTimeAndClicksIndependentlyOfTheMetronome()
    {
        var session = StartSession(
            new PracticeGuidance(
                CountInBeats: 2,
                CountInBeatDuration: SessionTime.FromMicroseconds(500_000),
                MetronomeEnabled: false));

        var started = session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        var secondClick = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)));
        var beganPractice = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)));

        Assert.Equal(PracticeSessionState.CountingIn, started.Snapshot.State);
        Assert.Equal(ChartTime.Zero, started.Snapshot.Position);
        Assert.Equal(1, started.Snapshot.CountInBeatsRemaining);
        Assert.Contains(started.Effects, effect =>
            effect is PracticeEffect.Click { Source: PracticeClickSource.CountIn });
        Assert.Equal(PracticeSessionState.CountingIn, secondClick.Snapshot.State);
        Assert.Equal(ChartTime.Zero, secondClick.Snapshot.Position);
        Assert.Equal(0, secondClick.Snapshot.CountInBeatsRemaining);
        Assert.Contains(secondClick.Effects, effect =>
            effect is PracticeEffect.Click { Source: PracticeClickSource.CountIn });
        Assert.Equal(PracticeSessionState.Running, beganPractice.Snapshot.State);
        Assert.Equal(ChartTime.Zero, beganPractice.Snapshot.Position);
        Assert.Contains(beganPractice.Events, practiceEvent =>
            practiceEvent is PracticeEvent.CountInCompleted);
        Assert.DoesNotContain(beganPractice.Effects, effect =>
            effect is PracticeEffect.Click { Source: PracticeClickSource.Metronome });
    }

    [Fact]
    public void MetronomeClicksAtTempoAwareChartBeats()
    {
        var session = StartSession(
            new PracticeGuidance(
                CountInBeats: 0,
                CountInBeatDuration: SessionTime.FromMicroseconds(500_000),
                MetronomeEnabled: true));

        var started = session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        var beforeBeat = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(499_999)));
        var onBeat = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)));

        Assert.Contains(started.Effects, effect =>
            effect is PracticeEffect.Click
            {
                Source: PracticeClickSource.Metronome,
                Position: { Microseconds: 0 }
            });
        Assert.DoesNotContain(beforeBeat.Effects, effect => effect is PracticeEffect.Click);
        Assert.Contains(onBeat.Effects, effect =>
            effect is PracticeEffect.Click
            {
                Source: PracticeClickSource.Metronome,
                Position: { Microseconds: 500_000 }
            });
    }

    [Fact]
    public void SeekingToTheCurrentPositionIsANoOp()
    {
        var session = StartSession(PracticeGuidance.Default);
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var transition = session.Handle(
            new PracticeSignal.Seek(SessionTime.Zero, ChartTime.Zero));

        Assert.Equal(PracticeSessionState.Running, transition.Snapshot.State);
        Assert.Empty(transition.Events);
        Assert.Empty(transition.Effects);
    }

    [Fact]
    public void APausedSeekCountsInBeforeDiscontinuousResume()
    {
        var session = StartSession(
            new PracticeGuidance(
                CountInBeats: 2,
                CountInBeatDuration: SessionTime.FromMicroseconds(500_000),
                MetronomeEnabled: false));
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        session.Handle(new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)));
        session.Handle(new PracticeSignal.Pause(SessionTime.FromMicroseconds(1_000_000)));

        var sought = session.Handle(new PracticeSignal.Seek(
            SessionTime.FromMicroseconds(1_000_000),
            ChartTime.FromMicroseconds(500_000)));
        var resumed = session.Handle(
            new PracticeSignal.Resume(SessionTime.FromMicroseconds(1_000_000)));
        var completedCountIn = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(2_000_000)));

        Assert.Equal(PracticeSessionState.LearnerPaused, sought.Snapshot.State);
        Assert.True(sought.Snapshot.ResumeCountInPending);
        Assert.Equal(PracticeSessionState.CountingIn, resumed.Snapshot.State);
        Assert.Equal(PracticeSessionState.Running, completedCountIn.Snapshot.State);
        Assert.False(completedCountIn.Snapshot.ResumeCountInPending);
        Assert.Contains(completedCountIn.Events, practiceEvent =>
            practiceEvent is PracticeEvent.SessionResumed);
    }

    [Fact]
    public void APausedPlayInTimeSeekToANoteDoesNotBecomeWaitForNotes()
    {
        var session = StartSession(PracticeGuidance.Default);
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        session.Handle(new PracticeSignal.Pause(SessionTime.Zero));

        var sought = session.Handle(new PracticeSignal.Seek(
            SessionTime.Zero,
            ChartTime.FromMicroseconds(1_500_000)));
        var resumed = session.Handle(new PracticeSignal.Resume(SessionTime.Zero));

        Assert.Equal(PracticeSessionState.LearnerPaused, sought.Snapshot.State);
        Assert.Null(sought.Snapshot.Target);
        Assert.Equal(PracticeSessionState.Running, resumed.Snapshot.State);
        Assert.Null(resumed.Snapshot.Target);
    }

    private static PracticeSession StartSession(PracticeGuidance guidance)
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(2_000_000),
            new[]
            {
                new PracticeChartNote(
                    0,
                    60,
                    ChartTime.FromMicroseconds(1_500_000),
                    ChartTime.FromMicroseconds(250_000),
                    PianoHand.Right)
            },
            new[]
            {
                new PracticeBeat(ChartTime.Zero, IsDownbeat: true),
                new PracticeBeat(ChartTime.FromMicroseconds(500_000), IsDownbeat: false),
                new PracticeBeat(ChartTime.FromMicroseconds(1_000_000), IsDownbeat: false),
                new PracticeBeat(ChartTime.FromMicroseconds(1_500_000), IsDownbeat: false)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);

        return PracticeSession.TryStart(chart, plan, guidance).Session!;
    }
}
