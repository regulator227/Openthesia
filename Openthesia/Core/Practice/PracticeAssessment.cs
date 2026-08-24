using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public readonly record struct TimingCalibration(
    long InputOffsetMicroseconds,
    int Revision,
    bool IsCalibrated)
{
    public static TimingCalibration Uncalibrated { get; } = new(
        InputOffsetMicroseconds: 0,
        Revision: 0,
        IsCalibrated: false);
}

public enum TimingJudgment
{
    Fantastic,
    Early,
    Late,
    Miss,
    Extra
}

public sealed record PracticeFeedback(
    byte Pitch,
    ChartTime Position,
    TimingJudgment Judgment,
    long? SignedOffsetMicroseconds);

public sealed record PracticeAssessmentTransition(
    IReadOnlyList<PracticeFeedback> Feedback,
    PracticeResult? Result = null);

public enum PracticeResultOutcome
{
    Completed,
    Abandoned
}

public sealed record ComparablePracticeSetup(
    ChartId ChartId,
    PracticeMode Mode,
    RequiredHands RequiredHands,
    Accompaniment Accompaniment,
    decimal TempoRatio,
    PracticeRange Range,
    string ScoringPolicyVersion);

public sealed record PracticeCompletion(
    int EvaluatedRequiredNotes,
    int TotalRequiredNotes)
{
    public decimal Ratio => TotalRequiredNotes == 0
        ? 0
        : (decimal)EvaluatedRequiredNotes / TotalRequiredNotes;
}

public sealed record PracticeAccuracy(
    int RequiredNotesHit,
    int TotalRequiredNotes,
    int ExtraNotes,
    decimal? CorrectAttackRatio)
{
    public decimal RequiredNotesHitRatio => TotalRequiredNotes == 0
        ? 0
        : (decimal)RequiredNotesHit / TotalRequiredNotes;
}

public sealed record PracticeTiming(
    int MatchedNotes,
    decimal AverageAbsoluteErrorMicroseconds,
    decimal AverageSignedOffsetMicroseconds,
    bool IsCalibrated,
    int CalibrationRevision);

public sealed record PracticeResult(
    Guid Id,
    ComparablePracticeSetup Setup,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    PracticeResultOutcome Outcome,
    bool Assisted,
    PracticeCompletion Completion,
    PracticeAccuracy Accuracy,
    PracticeTiming? Timing,
    IReadOnlyList<PracticeFeedback> NoteDetails)
{
    public bool IsEligible => Outcome == PracticeResultOutcome.Completed && !Assisted;
}

public sealed class PracticeAssessment
{
    private const long FantasticWindowMicroseconds = 21_500;
    private const long MatchWindowMicroseconds = 90_000;
    public const string CurrentScoringPolicyVersion = "practice-result-v1";

    private readonly PracticeChart _chart;
    private readonly PracticeSessionPlan _plan;
    private readonly TimingCalibration _calibration;
    private readonly PracticeRange _range;
    private readonly DateTimeOffset _startedAtUtc;
    private readonly IReadOnlyList<ExpectedNote> _expectedNotes;
    private readonly IReadOnlyList<ExpectedNote> _ignoredNotes;
    private readonly List<PracticeFeedback> _noteDetails = new();
    private readonly List<long> _timingOffsets = new();
    private int _requiredNotesHit;
    private int _extraNotes;
    private bool _assisted;
    private bool _paused;
    private bool _waitForNotesTargetDue;
    private ChartTime? _waitForNotesTargetOnset;

    private PracticeAssessment(
        PracticeChart chart,
        PracticeSessionPlan plan,
        TimingCalibration calibration,
        DateTimeOffset startedAtUtc)
    {
        _chart = chart;
        _plan = plan;
        _calibration = calibration;
        _range = plan.Range ?? new PracticeRange(ChartTime.Zero, chart.Duration);
        _startedAtUtc = startedAtUtc.ToUniversalTime();
        _expectedNotes = chart.Notes
            .Where(note => IsRequired(note.Hand, plan.RequiredHands))
            .Where(note => note.Onset.CompareTo(_range.Start) >= 0 && note.Onset.CompareTo(_range.End) < 0)
            .GroupBy(note => new { note.Pitch, note.Onset })
            .Select(group => new ExpectedNote(
                group.Key.Pitch,
                group.Key.Onset,
                HasFullEarlyWindow(group.Key.Onset)))
            .OrderBy(note => note.Onset)
            .ThenBy(note => note.Pitch)
            .ToArray();
        _ignoredNotes = chart.Notes
            .Where(note => !IsRequired(note.Hand, plan.RequiredHands))
            .Where(note => note.Onset.CompareTo(_range.Start) >= 0 && note.Onset.CompareTo(_range.End) < 0)
            .GroupBy(note => new { note.Pitch, note.Onset })
            .Select(group => new ExpectedNote(
                group.Key.Pitch,
                group.Key.Onset,
                timingEligible: false))
            .OrderBy(note => note.Onset)
            .ThenBy(note => note.Pitch)
            .ToArray();
    }

    public static PracticeAssessment Start(
        PracticeChart chart,
        PracticeSessionPlan plan,
        TimingCalibration calibration,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(plan);
        return new PracticeAssessment(chart, plan, calibration, startedAtUtc);
    }

    public PracticeAssessmentTransition Apply(
        PracticeTransition transition,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(transition);
        _ = occurredAtUtc;
        var feedback = new List<PracticeFeedback>();
        foreach (var practiceEvent in transition.Events)
        {
            switch (practiceEvent)
            {
                case PracticeEvent.AssistanceUsed:
                    _assisted = true;
                    break;
                case PracticeEvent.SessionPaused:
                    _paused = true;
                    break;
                case PracticeEvent.SessionResumed:
                    _paused = false;
                    break;
                case PracticeEvent.TargetBecameDue targetDue:
                    _waitForNotesTargetDue = true;
                    _waitForNotesTargetOnset = targetDue.Target.Onset;
                    break;
                case PracticeEvent.TargetSatisfied:
                    _waitForNotesTargetDue = false;
                    _waitForNotesTargetOnset = null;
                    break;
                case PracticeEvent.SessionSeeking seeking:
                    ResetForSeek(seeking);
                    break;
                case PracticeEvent.LearnerNoteObserved note
                    when (!_paused || _plan.Mode == PracticeMode.WaitForNotes) &&
                         (_plan.Mode != PracticeMode.WaitForNotes || _waitForNotesTargetDue):
                    var assessed = Assess(note);
                    if (assessed is not null)
                        feedback.Add(assessed);
                    break;
            }
        }
        if (_plan.Mode == PracticeMode.WaitForNotes)
        {
            _waitForNotesTargetDue = transition.Snapshot.Target is not null;
            _waitForNotesTargetOnset = transition.Snapshot.Target?.Onset;
        }

        foreach (var expected in _expectedNotes.Where(note => !note.Evaluated && !note.Skipped))
        {
            var offset = ToSessionMicroseconds(
                transition.Snapshot.Position.Microseconds - expected.Onset.Microseconds) -
                _calibration.InputOffsetMicroseconds;
            if (offset <= MatchWindowMicroseconds)
                continue;

            expected.Evaluated = true;
            var missed = new PracticeFeedback(
                expected.Pitch,
                expected.Onset,
                TimingJudgment.Miss,
                SignedOffsetMicroseconds: null);
            feedback.Add(missed);
            expected.Assessment = missed;
            _noteDetails.Add(missed);
        }

        var result = transition.Events.OfType<PracticeEvent.SessionCompleted>().Any()
            ? CreateResult(PracticeResultOutcome.Completed, occurredAtUtc)
            : transition.Events.OfType<PracticeEvent.SessionAbandoned>().Any()
                ? CreateResult(PracticeResultOutcome.Abandoned, occurredAtUtc)
                : null;
        IReadOnlyList<PracticeFeedback> visibleFeedback = _plan.Mode == PracticeMode.Recital
            ? Array.Empty<PracticeFeedback>()
            : feedback;
        return new PracticeAssessmentTransition(visibleFeedback, result);
    }

    private PracticeFeedback? Assess(PracticeEvent.LearnerNoteObserved observed)
    {
        var matchableExpectedNotes = _plan.Mode == PracticeMode.WaitForNotes
            ? _expectedNotes.Where(note => note.Onset == _waitForNotesTargetOnset)
            : _expectedNotes;
        var candidate = FindClosestCandidate(matchableExpectedNotes, observed);
        if (candidate is null)
        {
            var ignored = FindClosestCandidate(_ignoredNotes, observed);
            if (ignored is not null)
            {
                ignored.Note.Evaluated = true;
                return null;
            }
        }

        if (candidate is null)
        {
            _extraNotes++;
            var extra = new PracticeFeedback(
                observed.Pitch,
                observed.Position,
                TimingJudgment.Extra,
                SignedOffsetMicroseconds: null);
            _noteDetails.Add(extra);
            return extra;
        }

        candidate.Note.Evaluated = true;
        _requiredNotesHit++;
        var judgment = Math.Abs(candidate.Offset) <= FantasticWindowMicroseconds
            ? TimingJudgment.Fantastic
            : candidate.Offset < 0
                ? TimingJudgment.Early
                : TimingJudgment.Late;
        var matched = new PracticeFeedback(
            observed.Pitch,
            observed.Position,
            judgment,
            candidate.Offset);
        candidate.Note.Assessment = matched;
        _noteDetails.Add(matched);
        if (_plan.Mode != PracticeMode.WaitForNotes && candidate.Note.TimingEligible)
            _timingOffsets.Add(candidate.Offset);
        return matched;
    }

    private MatchCandidate? FindClosestCandidate(
        IEnumerable<ExpectedNote> notes,
        PracticeEvent.LearnerNoteObserved observed)
    {
        return notes
            .Where(note => !note.Evaluated && !note.Skipped && note.Pitch == observed.Pitch)
            .Select(note => new MatchCandidate(
                note,
                ToSessionMicroseconds(
                    observed.Position.Microseconds - note.Onset.Microseconds) -
                    _calibration.InputOffsetMicroseconds))
            .Where(candidate => Math.Abs(candidate.Offset) <= MatchWindowMicroseconds)
            .OrderBy(candidate => Math.Abs(candidate.Offset))
            .ThenBy(candidate => candidate.Note.Onset)
            .FirstOrDefault();
    }

    private PracticeResult CreateResult(
        PracticeResultOutcome outcome,
        DateTimeOffset endedAtUtc)
    {
        var timing = _plan.Mode == PracticeMode.WaitForNotes || _timingOffsets.Count == 0
            ? null
            : new PracticeTiming(
                _timingOffsets.Count,
                _timingOffsets.Average(offset => (decimal)Math.Abs(offset)),
                _timingOffsets.Average(offset => (decimal)offset),
                _calibration.IsCalibrated,
                _calibration.Revision);
        return new PracticeResult(
            Guid.NewGuid(),
            new ComparablePracticeSetup(
                _chart.Id,
                _plan.Mode,
                _plan.RequiredHands,
                _plan.Accompaniment,
                _plan.TempoRatio,
                _range,
                CurrentScoringPolicyVersion),
            _startedAtUtc,
            endedAtUtc.ToUniversalTime(),
            outcome,
            _assisted,
            new PracticeCompletion(
                _expectedNotes.Count(note => note.Evaluated),
                _expectedNotes.Count),
            new PracticeAccuracy(
                _requiredNotesHit,
                _expectedNotes.Count,
                _extraNotes,
                CorrectAttackRatio: _plan.Mode == PracticeMode.WaitForNotes &&
                                    _requiredNotesHit + _extraNotes > 0
                    ? (decimal)_requiredNotesHit / (_requiredNotesHit + _extraNotes)
                    : null),
            timing,
            _noteDetails.ToArray());
    }

    private void ResetForSeek(PracticeEvent.SessionSeeking seeking)
    {
        ResetAssessmentsFrom(seeking.To);
        if (seeking.To.CompareTo(seeking.From) > 0)
        {
            foreach (var note in _expectedNotes.Where(note =>
                         !note.Evaluated &&
                         note.Onset.CompareTo(seeking.From) >= 0 &&
                         note.Onset.CompareTo(seeking.To) < 0))
            {
                note.Skipped = true;
            }

            foreach (var note in _ignoredNotes.Where(note =>
                         !note.Evaluated &&
                         note.Onset.CompareTo(seeking.From) >= 0 &&
                         note.Onset.CompareTo(seeking.To) < 0))
            {
                note.Skipped = true;
            }
            return;
        }
    }

    private void ResetAssessmentsFrom(ChartTime position)
    {
        foreach (var note in _expectedNotes.Where(note => note.Onset.CompareTo(position) >= 0))
        {
            if (note.Assessment is { } assessment)
            {
                _noteDetails.Remove(assessment);
                if (assessment.Judgment is not TimingJudgment.Miss)
                {
                    _requiredNotesHit--;
                    if (_plan.Mode != PracticeMode.WaitForNotes &&
                        note.TimingEligible &&
                        assessment.SignedOffsetMicroseconds is { } offset)
                    {
                        _timingOffsets.Remove(offset);
                    }
                }
            }

            note.Assessment = null;
            note.Evaluated = false;
            note.Skipped = false;
        }

        foreach (var note in _ignoredNotes.Where(note => note.Onset.CompareTo(position) >= 0))
        {
            note.Evaluated = false;
            note.Skipped = false;
        }

        var removedExtras = _noteDetails
            .Where(detail => detail.Judgment == TimingJudgment.Extra)
            .Where(detail => detail.Position.CompareTo(position) >= 0)
            .ToArray();
        foreach (var extra in removedExtras)
            _noteDetails.Remove(extra);
        _extraNotes -= removedExtras.Length;
    }

    private bool HasFullEarlyWindow(ChartTime onset)
    {
        var onsetFromRangeStart = ToSessionMicroseconds(
            onset.Microseconds - _range.Start.Microseconds);
        return onsetFromRangeStart + _calibration.InputOffsetMicroseconds >=
               MatchWindowMicroseconds;
    }

    private long ToSessionMicroseconds(long chartMicroseconds)
    {
        return (long)(chartMicroseconds / _plan.TempoRatio);
    }

    private static bool IsRequired(PianoHand hand, RequiredHands requiredHands)
    {
        return requiredHands == RequiredHands.Both ||
               requiredHands == RequiredHands.Left && hand == PianoHand.Left ||
               requiredHands == RequiredHands.Right && hand == PianoHand.Right;
    }

    private sealed class ExpectedNote
    {
        public ExpectedNote(byte pitch, ChartTime onset, bool timingEligible)
        {
            Pitch = pitch;
            Onset = onset;
            TimingEligible = timingEligible;
        }

        public byte Pitch { get; }
        public ChartTime Onset { get; }
        public bool TimingEligible { get; }
        public bool Evaluated { get; set; }
        public bool Skipped { get; set; }
        public PracticeFeedback? Assessment { get; set; }
    }

    private sealed record MatchCandidate(ExpectedNote Note, long Offset);
}
