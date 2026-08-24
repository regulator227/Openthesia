using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeSessionTests
{
    [Fact]
    public void SelectedRangeWaitsForTheLastIncludedNoteTailBeforeCompleting()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(600_000),
            new[]
            {
                new PracticeChartNote(
                    0,
                    60,
                    ChartTime.FromMicroseconds(100_000),
                    ChartTime.FromMicroseconds(500_000),
                    PianoHand.Right)
            });
        var plan = new PracticeSessionPlan(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            TempoRatio: 1m,
            new PracticeRange(ChartTime.Zero, ChartTime.FromMicroseconds(200_000)));
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var atRangeEnd = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(200_000)));
        var atTailEnd = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(600_000)));

        Assert.Equal(PracticeSessionState.Running, atRangeEnd.Snapshot.State);
        Assert.Equal(ChartTime.FromMicroseconds(200_000), atRangeEnd.Snapshot.Position);
        Assert.Equal(PracticeSessionState.Completed, atTailEnd.Snapshot.State);
        Assert.Equal(ChartTime.FromMicroseconds(600_000), atTailEnd.Snapshot.Position);
    }

    [Fact]
    public void WaitForNotesStartsAtATimeZeroTarget()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(
                    Id: 0,
                    Pitch: 60,
                    Onset: ChartTime.Zero,
                    Duration: ChartTime.FromMicroseconds(500_000),
                    Hand: PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);

        var started = PracticeSession.TryStart(chart, plan);
        var transition = started.Session!.Handle(
            new PracticeSignal.Begin(SessionTime.Zero));

        Assert.Equal(PracticeSessionState.WaitingForInput, transition.Snapshot.State);
        Assert.Equal(new byte[] { 60 }, transition.Snapshot.Target!.Pitches);
    }

    [Fact]
    public void WaitForNotesStopsAtTheFirstTargetCrossedByTheChartClock()
    {
        var targetTime = ChartTime.FromMicroseconds(500_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(2_000_000),
            new[]
            {
                new PracticeChartNote(
                    Id: 0,
                    Pitch: 60,
                    Onset: targetTime,
                    Duration: ChartTime.FromMicroseconds(250_000),
                    Hand: PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var transition = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(750_000)));

        Assert.Equal(PracticeSessionState.WaitingForInput, transition.Snapshot.State);
        Assert.Equal(targetTime, transition.Snapshot.Position);
    }

    [Fact]
    public void WaitForNotesAdvancesOnlyAfterEveryUniqueTargetPitchIsAttacked()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(500_000), PianoHand.Right),
                new PracticeChartNote(1, 60, ChartTime.Zero, ChartTime.FromMicroseconds(500_000), PianoHand.Right),
                new PracticeChartNote(2, 64, ChartTime.Zero, ChartTime.FromMicroseconds(500_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var afterWrongNote = session.Handle(
            new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 61, Velocity: 100));
        var afterFirstChordPitch = session.Handle(
            new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 60, Velocity: 100));
        var afterChord = session.Handle(
            new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 64, Velocity: 100));

        Assert.Equal(PracticeSessionState.WaitingForInput, afterWrongNote.Snapshot.State);
        Assert.Equal(new byte[] { 64 }, afterFirstChordPitch.Snapshot.Target!.Pitches);
        Assert.Equal(PracticeSessionState.Running, afterChord.Snapshot.State);
    }

    [Fact]
    public void WaitForNotesDoesNotAcceptAnAttackBeforeTheTargetIsDue()
    {
        var targetTime = ChartTime.FromMicroseconds(500_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, targetTime, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        session.Handle(new PracticeSignal.NoteOn(
            SessionTime.FromMicroseconds(100_000), Pitch: 60, Velocity: 100));

        var targetDue = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)));
        var afterNewAttack = session.Handle(
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(500_000), Pitch: 60, Velocity: 100));

        Assert.Equal(PracticeSessionState.WaitingForInput, targetDue.Snapshot.State);
        Assert.Equal(targetTime, targetDue.Snapshot.Position);
        Assert.Equal(PracticeSessionState.Running, afterNewAttack.Snapshot.State);
    }

    [Fact]
    public void SatisfyingATargetWhileLearnerPausedDoesNotResumeTheChartClock()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        var paused = session.Handle(new PracticeSignal.Pause(SessionTime.Zero));

        var afterTarget = session.Handle(
            new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 60, Velocity: 100));
        var whilePaused = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)));
        var resumed = session.Handle(
            new PracticeSignal.Resume(SessionTime.FromMicroseconds(500_000)));

        Assert.Equal(PracticeSessionState.LearnerPaused, afterTarget.Snapshot.State);
        Assert.Equal(ChartTime.Zero, whilePaused.Snapshot.Position);
        Assert.Equal(PracticeSessionState.Running, resumed.Snapshot.State);
        Assert.IsType<PracticeEffect.PausePlayback>(Assert.Single(paused.Effects));
        Assert.IsType<PracticeEffect.StartPlayback>(Assert.Single(resumed.Effects));
    }

    [Fact]
    public void SeekingFromWaitRecalculatesTheTargetAndRestoresRunningIntent()
    {
        var firstTarget = ChartTime.FromMicroseconds(200_000);
        var secondTarget = ChartTime.FromMicroseconds(700_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, firstTarget, ChartTime.FromMicroseconds(100_000), PianoHand.Right),
                new PracticeChartNote(1, 64, secondTarget, ChartTime.FromMicroseconds(100_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        session.Handle(new PracticeSignal.Pulse(SessionTime.FromMicroseconds(200_000)));

        var transition = session.Handle(new PracticeSignal.Seek(
            SessionTime.FromMicroseconds(200_000), secondTarget));

        Assert.Equal(PracticeSessionState.WaitingForInput, transition.Snapshot.State);
        Assert.Equal(secondTarget, transition.Snapshot.Position);
        Assert.Equal(new byte[] { 64 }, transition.Snapshot.Target!.Pitches);
        Assert.Collection(
            transition.Events,
            practiceEvent =>
            {
                var seeking = Assert.IsType<PracticeEvent.SessionSeeking>(practiceEvent);
                Assert.Equal(firstTarget, seeking.From);
                Assert.Equal(secondTarget, seeking.To);
            },
            practiceEvent => Assert.IsType<PracticeEvent.AssistanceUsed>(practiceEvent));
    }

    [Fact]
    public void OneHandAutomaticAccompanimentPlaysOnlyTheOtherHand()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(10, 48, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Left),
                new PracticeChartNote(20, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Right,
            Accompaniment.Automatic,
            tempoRatio: 0.75m);
        var session = PracticeSession.TryStart(chart, plan).Session!;

        var transition = session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var configurePlayback = Assert.IsType<PracticeEffect.ConfigurePlayback>(
            Assert.Single(transition.Effects.OfType<PracticeEffect.ConfigurePlayback>()));
        Assert.Equal(new[] { 10 }, configurePlayback.AudibleChartNoteIds);
        Assert.Equal(0.75m, configurePlayback.TempoRatio);
    }

    [Fact]
    public void WaitForNotesPublishesOrderedTargetEventsAndPlaybackEffects()
    {
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60 });
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;

        var started = session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        var satisfied = session.Handle(
            new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 60, Velocity: 96));

        Assert.Collection(
            started.Events,
            practiceEvent => Assert.IsType<PracticeEvent.SessionStarted>(practiceEvent),
            practiceEvent => AssertTarget(target, Assert.IsType<PracticeEvent.TargetBecameDue>(practiceEvent).Target));
        Assert.Collection(
            started.Effects,
            effect => Assert.IsType<PracticeEffect.ConfigurePlayback>(effect),
            effect => Assert.Equal(ChartTime.Zero, Assert.IsType<PracticeEffect.PausePlayback>(effect).At));
        Assert.Collection(
            satisfied.Events,
            practiceEvent => Assert.Equal(60, Assert.IsType<PracticeEvent.LearnerNoteObserved>(practiceEvent).Pitch),
            practiceEvent => AssertTarget(target, Assert.IsType<PracticeEvent.TargetSatisfied>(practiceEvent).Target));
        Assert.Equal(ChartTime.Zero, Assert.IsType<PracticeEffect.StartPlayback>(
            Assert.Single(satisfied.Effects)).From);
    }

    [Fact]
    public void ReachingTheRangeEndCompletesTheSession()
    {
        var end = ChartTime.FromMicroseconds(1_000_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            end,
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        session.Handle(new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 60, Velocity: 96));

        var transition = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)));

        Assert.Equal(PracticeSessionState.Completed, transition.Snapshot.State);
        Assert.IsType<PracticeEvent.SessionCompleted>(Assert.Single(transition.Events));
        Assert.Equal(end, Assert.IsType<PracticeEffect.StopPlayback>(
            Assert.Single(transition.Effects)).At);
    }

    [Fact]
    public void EndingAnIncompleteAttemptAbandonsTheSession()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var transition = session.Handle(new PracticeSignal.Abandon(SessionTime.Zero));

        Assert.Equal(PracticeSessionState.Abandoned, transition.Snapshot.State);
        Assert.IsType<PracticeEvent.SessionAbandoned>(Assert.Single(transition.Events));
        Assert.Equal(ChartTime.Zero, Assert.IsType<PracticeEffect.StopPlayback>(
            Assert.Single(transition.Effects)).At);
    }

    [Fact]
    public void InvalidSignalsReturnTypedErrorsWithoutChangingTheSession()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;

        var transition = session.Handle(new PracticeSignal.Resume(SessionTime.Zero));

        Assert.Equal(PracticeSignalError.InvalidForState, transition.Error);
        Assert.Equal(PracticeSessionState.Ready, transition.Snapshot.State);
        Assert.Empty(transition.Events);
        Assert.Empty(transition.Effects);
    }

    [Fact]
    public void SeekingToTheRangeEndRecordsAssistanceAndCompletion()
    {
        var end = ChartTime.FromMicroseconds(1_000_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            end,
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var transition = session.Handle(
            new PracticeSignal.Seek(SessionTime.Zero, end));

        Assert.Equal(PracticeSessionState.Completed, transition.Snapshot.State);
        Assert.Collection(
            transition.Events,
            practiceEvent => Assert.IsType<PracticeEvent.SessionSeeking>(practiceEvent),
            practiceEvent => Assert.IsType<PracticeEvent.AssistanceUsed>(practiceEvent),
            practiceEvent => Assert.IsType<PracticeEvent.SessionCompleted>(practiceEvent));
    }

    [Theory]
    [InlineData(PracticeMode.PlayInTime)]
    [InlineData(PracticeMode.Recital)]
    public void ClockDrivenModesDoNotWaitAtPracticeTargets(PracticeMode mode)
    {
        var end = ChartTime.FromMicroseconds(1_000_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            end,
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            mode,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;

        var started = session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        var finished = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)));

        Assert.Equal(PracticeSessionState.Running, started.Snapshot.State);
        Assert.Null(started.Snapshot.Target);
        Assert.Equal(PracticeSessionState.Completed, finished.Snapshot.State);
    }

    [Fact]
    public void RepeatedPitchAtANewOnsetRequiresANewAttack()
    {
        var secondOnset = ChartTime.FromMicroseconds(500_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right),
                new PracticeChartNote(1, 60, secondOnset, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));
        session.Handle(new PracticeSignal.NoteOn(SessionTime.Zero, Pitch: 60, Velocity: 96));

        var dueAgain = session.Handle(
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)));

        Assert.Equal(PracticeSessionState.WaitingForInput, dueAgain.Snapshot.State);
        Assert.Equal(secondOnset, dueAgain.Snapshot.Target!.Onset);
        Assert.Equal(new byte[] { 60 }, dueAgain.Snapshot.Target.Pitches);
    }

    [Fact]
    public void RequiredHandAndAccompanimentValidationReturnsTypedStartErrors()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });

        var missingHand = PracticeSession.TryStart(
            chart,
            PracticeSessionPlan.FullChart(
                PracticeMode.WaitForNotes,
                RequiredHands.Left,
                Accompaniment.Silent,
                tempoRatio: 1m));
        var invalidAccompaniment = PracticeSession.TryStart(
            chart,
            PracticeSessionPlan.FullChart(
                PracticeMode.WaitForNotes,
                RequiredHands.Both,
                Accompaniment.Automatic,
                tempoRatio: 1m));

        Assert.Equal(PracticeStartError.RequiredHandHasNoNotes, missingHand.Error);
        Assert.Equal(PracticeStartError.InvalidAccompaniment, invalidAccompaniment.Error);
    }

    [Fact]
    public void ASignalArrivingAtTheRangeEndCannotResurrectACompletedSession()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(250_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        session.Handle(new PracticeSignal.Begin(SessionTime.Zero));

        var transition = session.Handle(
            new PracticeSignal.Pause(SessionTime.FromMicroseconds(1_000_000)));

        Assert.Equal(PracticeSessionState.Completed, transition.Snapshot.State);
        Assert.IsType<PracticeEvent.SessionCompleted>(Assert.Single(transition.Events));
    }

    private static void AssertTarget(PracticeTarget expected, PracticeTarget actual)
    {
        Assert.Equal(expected.Onset, actual.Onset);
        Assert.Equal(expected.Pitches, actual.Pitches);
    }
}
