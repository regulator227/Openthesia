using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeAssessmentTests
{
    [Theory]
    [InlineData(-90_000, TimingJudgment.Early)]
    [InlineData(-21_501, TimingJudgment.Early)]
    [InlineData(-21_500, TimingJudgment.Fantastic)]
    [InlineData(21_500, TimingJudgment.Fantastic)]
    [InlineData(21_501, TimingJudgment.Late)]
    [InlineData(90_000, TimingJudgment.Late)]
    public void TimingJudgmentUsesTheConfirmedInclusiveBoundaries(
        long offsetMicroseconds,
        TimingJudgment expectedJudgment)
    {
        var onset = ChartTime.FromMicroseconds(500_000);
        var chart = CreateSingleNoteChart(onset);
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);

        var assessed = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(
                SessionTime.FromMicroseconds(onset.Microseconds + offsetMicroseconds),
                60,
                100),
            startedAt.AddTicks((onset.Microseconds + offsetMicroseconds) * 10));

        var feedback = Assert.Single(assessed.Feedback);
        Assert.Equal(expectedJudgment, feedback.Judgment);
        Assert.Equal(offsetMicroseconds, feedback.SignedOffsetMicroseconds);
    }

    [Fact]
    public void CalibrationAndTempoAreAppliedBeforeTimingJudgment()
    {
        var onset = ChartTime.FromMicroseconds(500_000);
        var chart = CreateSingleNoteChart(onset);
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 0.5m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(
            chart,
            plan,
            new TimingCalibration(20_000, Revision: 3, IsCalibrated: true),
            startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);

        var assessed = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(1_020_000), 60, 100),
            startedAt.AddMilliseconds(1_020));

        var feedback = Assert.Single(assessed.Feedback);
        Assert.Equal(TimingJudgment.Fantastic, feedback.Judgment);
        Assert.Equal(0, feedback.SignedOffsetMicroseconds);
    }

    [Fact]
    public void FirstNoteWithoutAFullEarlyWindowIsScoredForAccuracyButNotTiming()
    {
        var chart = CreateSingleNoteChart(ChartTime.Zero);
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.Zero, 60, 100), startedAt);

        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)),
            startedAt.AddSeconds(1));

        Assert.Equal(1, completed.Result!.Accuracy.RequiredNotesHit);
        Assert.Null(completed.Result.Timing);
    }

    [Fact]
    public void PlayInTimeReportsFantasticForAnAttackAtTheExpectedOnset()
    {
        var onset = ChartTime.FromMicroseconds(500_000);
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(
                    Id: 0,
                    Pitch: 60,
                    Onset: onset,
                    Duration: ChartTime.FromMicroseconds(250_000),
                    Hand: PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var assessment = PracticeAssessment.Start(
            chart,
            plan,
            TimingCalibration.Uncalibrated,
            DateTimeOffset.Parse("2026-08-23T20:00:00Z"));
        assessment.Apply(
            session.Handle(new PracticeSignal.Begin(SessionTime.Zero)),
            DateTimeOffset.Parse("2026-08-23T20:00:00Z"));

        var assessed = assessment.Apply(
            session.Handle(new PracticeSignal.NoteOn(
                SessionTime.FromMicroseconds(500_000),
                Pitch: 60,
                Velocity: 100)),
            DateTimeOffset.Parse("2026-08-23T20:00:00.500Z"));

        var feedback = Assert.Single(assessed.Feedback);
        Assert.Equal(TimingJudgment.Fantastic, feedback.Judgment);
        Assert.Equal(0, feedback.SignedOffsetMicroseconds);
    }

    [Fact]
    public void PlayInTimeReportsMissAfterTheNinetyMillisecondWindowCloses()
    {
        var onset = ChartTime.FromMicroseconds(500_000);
        var chart = CreateSingleNoteChart(onset);
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(
            chart,
            plan,
            TimingCalibration.Uncalibrated,
            startedAt);
        assessment.Apply(
            session.Handle(new PracticeSignal.Begin(SessionTime.Zero)),
            startedAt);

        var assessed = assessment.Apply(
            session.Handle(new PracticeSignal.Pulse(
                SessionTime.FromMicroseconds(590_001))),
            startedAt.AddTicks(5_900_010));

        var feedback = Assert.Single(assessed.Feedback);
        Assert.Equal(TimingJudgment.Miss, feedback.Judgment);
        Assert.Equal(60, feedback.Pitch);
        Assert.Null(feedback.SignedOffsetMicroseconds);
    }

    [Fact]
    public void CompletedPlayInTimeReturnsSeparateCompletionAccuracyAndTiming()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.FromMicroseconds(100_000), ChartTime.FromMicroseconds(100_000), PianoHand.Right),
                new PracticeChartNote(1, 64, ChartTime.FromMicroseconds(500_000), ChartTime.FromMicroseconds(100_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(
            chart,
            plan,
            TimingCalibration.Uncalibrated,
            startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.NoteOn(
            SessionTime.FromMicroseconds(100_000), Pitch: 60, Velocity: 100), startedAt.AddMilliseconds(100));
        Apply(assessment, session, new PracticeSignal.NoteOn(
            SessionTime.FromMicroseconds(300_000), Pitch: 61, Velocity: 100), startedAt.AddMilliseconds(300));

        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)),
            startedAt.AddSeconds(1));

        var result = Assert.IsType<PracticeResult>(completed.Result);
        Assert.Equal(2, result.Completion.EvaluatedRequiredNotes);
        Assert.Equal(2, result.Completion.TotalRequiredNotes);
        Assert.Equal(1m, result.Completion.Ratio);
        Assert.Equal(1, result.Accuracy.RequiredNotesHit);
        Assert.Equal(2, result.Accuracy.TotalRequiredNotes);
        Assert.Equal(1, result.Accuracy.ExtraNotes);
        Assert.Equal(0.5m, result.Accuracy.RequiredNotesHitRatio);
        Assert.Equal(1, result.Timing!.MatchedNotes);
        Assert.Equal(0m, result.Timing.AverageAbsoluteErrorMicroseconds);
        Assert.Equal(0m, result.Timing.AverageSignedOffsetMicroseconds);
        Assert.False(result.Timing.IsCalibrated);
    }

    [Fact]
    public void CompletedWaitForNotesReportsCorrectAttackRatioWithoutTiming()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.Zero, ChartTime.FromMicroseconds(100_000), PianoHand.Right),
                new PracticeChartNote(1, 64, ChartTime.Zero, ChartTime.FromMicroseconds(100_000), PianoHand.Right),
                new PracticeChartNote(2, 67, ChartTime.Zero, ChartTime.FromMicroseconds(100_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(100_000), 60, 100), startedAt.AddMilliseconds(100));
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(200_000), 64, 100), startedAt.AddMilliseconds(200));
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(300_000), 65, 100), startedAt.AddMilliseconds(300));
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(400_000), 67, 100), startedAt.AddMilliseconds(400));

        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_400_000)),
            startedAt.AddMilliseconds(1_400));

        var result = Assert.IsType<PracticeResult>(completed.Result);
        Assert.Equal(3, result.Accuracy.RequiredNotesHit);
        Assert.Equal(1, result.Accuracy.ExtraNotes);
        Assert.Equal(0.75m, result.Accuracy.CorrectAttackRatio);
        Assert.Null(result.Timing);
    }

    [Fact]
    public void WaitForNotesCorrectAttackRatioCountsOnlyAttacksWhileATargetIsDue()
    {
        var chart = CreateSingleNoteChart(ChartTime.FromMicroseconds(500_000));
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(100_000), 65, 100), startedAt.AddMilliseconds(100));
        Apply(assessment, session, new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)), startedAt.AddMilliseconds(500));
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(500_000), 60, 100), startedAt.AddMilliseconds(500));

        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_500_000)),
            startedAt.AddMilliseconds(1_500));

        Assert.Equal(0, completed.Result!.Accuracy.ExtraNotes);
        Assert.Equal(1m, completed.Result.Accuracy.CorrectAttackRatio);
    }

    [Fact]
    public void WaitForNotesDoesNotMatchANearbyFutureTargetEarly()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.FromMicroseconds(500_000), ChartTime.FromMicroseconds(50_000), PianoHand.Right),
                new PracticeChartNote(1, 64, ChartTime.FromMicroseconds(550_000), ChartTime.FromMicroseconds(50_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.WaitForNotes,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)), startedAt.AddMilliseconds(500));

        var earlyFutureAttack = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(500_000), 64, 100),
            startedAt.AddMilliseconds(500));
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(500_000), 60, 100), startedAt.AddMilliseconds(500));
        Apply(assessment, session, new PracticeSignal.Pulse(SessionTime.FromMicroseconds(550_000)), startedAt.AddMilliseconds(550));
        var onTargetAttack = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(550_000), 64, 100),
            startedAt.AddMilliseconds(550));

        Assert.Equal(TimingJudgment.Extra, Assert.Single(earlyFutureAttack.Feedback).Judgment);
        Assert.Equal(TimingJudgment.Fantastic, Assert.Single(onTargetAttack.Feedback).Judgment);
    }

    [Fact]
    public void AttackDuringANoteTailAfterTheTimingWindowIsExtraAndTheOnsetIsMissed()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(
                    Id: 0,
                    Pitch: 60,
                    Onset: ChartTime.FromMicroseconds(100_000),
                    Duration: ChartTime.FromMicroseconds(800_000),
                    Hand: PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);

        var assessed = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(300_000), 60, 100),
            startedAt.AddMilliseconds(300));

        Assert.Contains(assessed.Feedback, feedback => feedback.Judgment == TimingJudgment.Miss);
        Assert.Contains(assessed.Feedback, feedback => feedback.Judgment == TimingJudgment.Extra);
        Assert.Equal(PracticeSessionState.Running, session.Snapshot.State);
    }

    [Fact]
    public void PlayInTimeIgnoresLearnerInputWhilePaused()
    {
        var chart = CreateSingleNoteChart(ChartTime.FromMicroseconds(500_000));
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.Pause(
            SessionTime.FromMicroseconds(400_000)), startedAt.AddMilliseconds(400));

        var pausedInput = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(500_000), 60, 100),
            startedAt.AddMilliseconds(500));

        Assert.Empty(pausedInput.Feedback);
    }

    [Fact]
    public void ACorrectAttackFromANonRequiredHandIsIgnored()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 48, ChartTime.FromMicroseconds(500_000), ChartTime.FromMicroseconds(100_000), PianoHand.Left),
                new PracticeChartNote(1, 60, ChartTime.FromMicroseconds(500_000), ChartTime.FromMicroseconds(100_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Right,
            Accompaniment.Automatic,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);

        var ignored = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(500_000), 48, 100),
            startedAt.AddMilliseconds(500));
        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)),
            startedAt.AddSeconds(1));

        Assert.Empty(ignored.Feedback);
        Assert.Equal(0, completed.Result!.Accuracy.ExtraNotes);
    }

    [Fact]
    public void RecitalSuppressesLiveFeedbackButKeepsNoteDetailsForTheResult()
    {
        var chart = CreateSingleNoteChart(ChartTime.FromMicroseconds(500_000));
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.Recital,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);

        var live = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(530_000), 60, 100),
            startedAt.AddMilliseconds(530));
        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(1_000_000)),
            startedAt.AddSeconds(1));

        Assert.Empty(live.Feedback);
        var detail = Assert.Single(completed.Result!.NoteDetails);
        Assert.Equal(TimingJudgment.Late, detail.Judgment);
        Assert.Equal(30_000, detail.SignedOffsetMicroseconds);
    }

    [Fact]
    public void ForwardSeekLeavesSkippedRequiredNotesUnevaluatedAndMarksResultAssisted()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.FromMicroseconds(100_000), ChartTime.FromMicroseconds(100_000), PianoHand.Right),
                new PracticeChartNote(1, 64, ChartTime.FromMicroseconds(500_000), ChartTime.FromMicroseconds(100_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(100_000), 60, 100), startedAt.AddMilliseconds(100));
        Apply(assessment, session, new PracticeSignal.Pulse(SessionTime.FromMicroseconds(200_000)), startedAt.AddMilliseconds(200));

        Apply(
            assessment,
            session,
            new PracticeSignal.Seek(SessionTime.FromMicroseconds(200_000), ChartTime.FromMicroseconds(700_000)),
            startedAt.AddMilliseconds(200));
        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(500_000)),
            startedAt.AddMilliseconds(500));

        Assert.True(completed.Result!.Assisted);
        Assert.Equal(1, completed.Result.Completion.EvaluatedRequiredNotes);
        Assert.DoesNotContain(completed.Result.NoteDetails, detail => detail.Pitch == 64);
    }

    [Fact]
    public void AttackNearAForwardSkippedOnsetIsExtraAndDoesNotReviveTheOpportunity()
    {
        var chart = new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(300_000),
            new[]
            {
                new PracticeChartNote(0, 60, ChartTime.FromMicroseconds(120_000), ChartTime.FromMicroseconds(30_000), PianoHand.Right)
            });
        var plan = PracticeSessionPlan.FullChart(
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            tempoRatio: 1m);
        var session = PracticeSession.TryStart(chart, plan).Session!;
        var startedAt = DateTimeOffset.Parse("2026-08-23T20:00:00Z");
        var assessment = PracticeAssessment.Start(chart, plan, TimingCalibration.Uncalibrated, startedAt);
        Apply(assessment, session, new PracticeSignal.Begin(SessionTime.Zero), startedAt);
        Apply(assessment, session, new PracticeSignal.Pulse(SessionTime.FromMicroseconds(100_000)), startedAt.AddMilliseconds(100));
        Apply(assessment, session, new PracticeSignal.Seek(
            SessionTime.FromMicroseconds(100_000),
            ChartTime.FromMicroseconds(150_000)), startedAt.AddMilliseconds(100));

        var attack = Apply(
            assessment,
            session,
            new PracticeSignal.NoteOn(SessionTime.FromMicroseconds(100_000), 60, 100),
            startedAt.AddMilliseconds(100));
        var completed = Apply(
            assessment,
            session,
            new PracticeSignal.Pulse(SessionTime.FromMicroseconds(250_000)),
            startedAt.AddMilliseconds(250));

        Assert.Equal(TimingJudgment.Extra, Assert.Single(attack.Feedback).Judgment);
        Assert.Equal(0, completed.Result!.Completion.EvaluatedRequiredNotes);
        Assert.Equal(0, completed.Result.Accuracy.RequiredNotesHit);
        Assert.Equal(1, completed.Result.Accuracy.ExtraNotes);
    }

    private static PracticeAssessmentTransition Apply(
        PracticeAssessment assessment,
        PracticeSession session,
        PracticeSignal signal,
        DateTimeOffset occurredAtUtc)
    {
        return assessment.Apply(session.Handle(signal), occurredAtUtc);
    }

    private static PracticeChart CreateSingleNoteChart(ChartTime onset)
    {
        return new PracticeChart(
            ChartId.FromHash(new byte[32]),
            ChartTime.FromMicroseconds(1_000_000),
            new[]
            {
                new PracticeChartNote(
                    Id: 0,
                    Pitch: 60,
                    Onset: onset,
                    Duration: ChartTime.FromMicroseconds(250_000),
                    Hand: PianoHand.Right)
            });
    }
}
