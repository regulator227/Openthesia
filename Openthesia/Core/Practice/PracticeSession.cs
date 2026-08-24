using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public readonly record struct ChartTime(long Microseconds) : IComparable<ChartTime>
{
    public static ChartTime Zero { get; } = new(0);

    public static ChartTime FromMicroseconds(long microseconds)
    {
        if (microseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(microseconds));

        return new ChartTime(microseconds);
    }

    public int CompareTo(ChartTime other)
    {
        return Microseconds.CompareTo(other.Microseconds);
    }
}

public readonly record struct SessionTime(long Microseconds) : IComparable<SessionTime>
{
    public static SessionTime Zero { get; } = new(0);

    public static SessionTime FromMicroseconds(long microseconds)
    {
        if (microseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(microseconds));

        return new SessionTime(microseconds);
    }

    public int CompareTo(SessionTime other)
    {
        return Microseconds.CompareTo(other.Microseconds);
    }
}

public sealed record PracticeChartNote(
    int Id,
    byte Pitch,
    ChartTime Onset,
    ChartTime Duration,
    PianoHand Hand);

public sealed record PracticeBeat(
    ChartTime Position,
    bool IsDownbeat);

public sealed class PracticeChart
{
    public PracticeChart(
        ChartId id,
        ChartTime duration,
        IReadOnlyList<PracticeChartNote> notes,
        IReadOnlyList<PracticeBeat>? beats = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(notes);
        if (duration.CompareTo(ChartTime.Zero) <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        Id = id;
        Duration = duration;
        Notes = notes.ToArray();
        Beats = (beats ?? Array.Empty<PracticeBeat>())
            .Where(beat => beat.Position.CompareTo(ChartTime.Zero) >= 0)
            .Where(beat => beat.Position.CompareTo(duration) <= 0)
            .GroupBy(beat => beat.Position)
            .Select(group => new PracticeBeat(
                group.Key,
                group.Any(beat => beat.IsDownbeat)))
            .OrderBy(beat => beat.Position)
            .ToArray();
    }

    public ChartId Id { get; }
    public ChartTime Duration { get; }
    public IReadOnlyList<PracticeChartNote> Notes { get; }
    public IReadOnlyList<PracticeBeat> Beats { get; }
}

public enum PracticeMode
{
    WaitForNotes,
    PlayInTime,
    Recital
}

public enum RequiredHands
{
    Left,
    Right,
    Both
}

public enum Accompaniment
{
    Automatic,
    Silent
}

public sealed record PracticeRange(ChartTime Start, ChartTime End)
{
    public bool Contains(ChartTime position)
    {
        return position.CompareTo(Start) >= 0 && position.CompareTo(End) < 0;
    }
}

public sealed record PracticeGuidance(
    int CountInBeats,
    SessionTime CountInBeatDuration,
    bool MetronomeEnabled)
{
    public static PracticeGuidance Default { get; } = new(
        CountInBeats: 0,
        CountInBeatDuration: SessionTime.FromMicroseconds(500_000),
        MetronomeEnabled: false);
}

public sealed record PracticeSessionPlan(
    PracticeMode Mode,
    RequiredHands RequiredHands,
    Accompaniment Accompaniment,
    decimal TempoRatio,
    PracticeRange? Range)
{
    public static PracticeSessionPlan FullChart(
        PracticeMode mode,
        RequiredHands requiredHands,
        Accompaniment accompaniment,
        decimal tempoRatio)
    {
        return new PracticeSessionPlan(
            mode,
            requiredHands,
            accompaniment,
            tempoRatio,
            Range: null);
    }
}

public enum PracticeSessionState
{
    Ready,
    CountingIn,
    Running,
    WaitingForInput,
    LearnerPaused,
    Seeking,
    Completed,
    Abandoned
}

public sealed record PracticeTarget(
    ChartTime Onset,
    IReadOnlyList<byte> Pitches);

public sealed record PracticeSessionSnapshot(
    PracticeSessionState State,
    ChartTime Position,
    PracticeTarget? Target,
    int CountInBeatsRemaining = 0,
    bool ResumeCountInPending = false);

public abstract record PracticeSignal(SessionTime At)
{
    public sealed record Begin(SessionTime At) : PracticeSignal(At);
    public sealed record Pulse(SessionTime At) : PracticeSignal(At);
    public sealed record NoteOn(SessionTime At, byte Pitch, byte Velocity) : PracticeSignal(At);
    public sealed record Pause(SessionTime At) : PracticeSignal(At);
    public sealed record Resume(SessionTime At) : PracticeSignal(At);
    public sealed record Seek(SessionTime At, ChartTime Position) : PracticeSignal(At);
    public sealed record ChangeGuidance(SessionTime At, PracticeGuidance Guidance) : PracticeSignal(At);
    public sealed record Abandon(SessionTime At) : PracticeSignal(At);
}

public enum PracticeAssistance
{
    Seek
}

public abstract record PracticeEvent
{
    public sealed record SessionStarted(
        PracticeMode Mode,
        RequiredHands RequiredHands,
        Accompaniment Accompaniment,
        decimal TempoRatio,
        PracticeRange Range) : PracticeEvent;

    public sealed record LearnerNoteObserved(
        byte Pitch,
        byte Velocity,
        ChartTime Position) : PracticeEvent;

    public sealed record TargetBecameDue(PracticeTarget Target) : PracticeEvent;

    public sealed record TargetSatisfied(PracticeTarget Target) : PracticeEvent;

    public sealed record SessionCompleted(ChartTime Position) : PracticeEvent;

    public sealed record SessionAbandoned(ChartTime Position) : PracticeEvent;

    public sealed record SessionPaused(ChartTime Position) : PracticeEvent;

    public sealed record SessionResumed(ChartTime Position) : PracticeEvent;

    public sealed record CountInStarted(int Beats) : PracticeEvent;

    public sealed record CountInCompleted : PracticeEvent;

    public sealed record SessionSeeking(
        ChartTime From,
        ChartTime To) : PracticeEvent;

    public sealed record AssistanceUsed(
        PracticeAssistance Assistance,
        ChartTime Position) : PracticeEvent;
}

public abstract record PracticeEffect
{
    public sealed record ConfigurePlayback(
        IReadOnlyList<int> AudibleChartNoteIds,
        decimal TempoRatio) : PracticeEffect;

    public sealed record StartPlayback(ChartTime From) : PracticeEffect;

    public sealed record PausePlayback(ChartTime At) : PracticeEffect;

    public sealed record StopPlayback(ChartTime At) : PracticeEffect;

    public sealed record SeekPlayback(ChartTime To) : PracticeEffect;

    public sealed record Click(
        PracticeClickSource Source,
        ChartTime Position,
        bool Accent) : PracticeEffect;
}

public enum PracticeClickSource
{
    CountIn,
    Metronome
}

public sealed record PracticeTransition(
    PracticeSessionSnapshot Snapshot,
    IReadOnlyList<PracticeEvent> Events,
    IReadOnlyList<PracticeEffect> Effects,
    PracticeSignalError? Error = null)
{
    public PracticeTransition(PracticeSessionSnapshot snapshot)
        : this(snapshot, Array.Empty<PracticeEvent>(), Array.Empty<PracticeEffect>())
    {
    }
}

public enum PracticeSignalError
{
    OutOfOrder,
    InvalidForState,
    InvalidPosition,
    InvalidGuidance
}

public enum PracticeStartError
{
    InvalidRange,
    InvalidTempo,
    RequiredHandHasNoNotes,
    InvalidAccompaniment,
    InvalidGuidance
}

public sealed record PracticeSessionStartResult(
    PracticeSession? Session,
    PracticeStartError? Error);

public sealed class PracticeSession
{
    private readonly PracticeSessionPlan _plan;
    private readonly PracticeRange _range;
    private readonly ChartTime _playbackEnd;
    private readonly IReadOnlyList<PracticeTarget> _targets;
    private readonly IReadOnlyList<int> _audibleChartNoteIds;
    private readonly IReadOnlyList<PracticeBeat> _beats;
    private PracticeGuidance _guidance;
    private PracticeSessionSnapshot _snapshot;
    private SessionTime _lastSignalTime;
    private int _targetIndex;
    private SessionTime? _countInStartedAt;
    private int _countInClicksEmitted;
    private int _activeCountInBeats;
    private SessionTime _activeCountInBeatDuration;
    private PracticeSessionState _stateAfterCountIn;
    private bool _resumeAfterCountIn;

    private PracticeSession(
        PracticeSessionPlan plan,
        PracticeGuidance guidance,
        PracticeRange range,
        ChartTime playbackEnd,
        IReadOnlyList<PracticeTarget> targets,
        IReadOnlyList<int> audibleChartNoteIds,
        IReadOnlyList<PracticeBeat> beats)
    {
        _plan = plan;
        _guidance = guidance;
        _range = range;
        _playbackEnd = playbackEnd;
        _targets = targets;
        _audibleChartNoteIds = audibleChartNoteIds;
        _beats = beats;
        _snapshot = new PracticeSessionSnapshot(
            PracticeSessionState.Ready,
            range.Start,
            Target: null);
    }

    public PracticeSessionSnapshot Snapshot => _snapshot;

    public static PracticeSessionStartResult TryStart(
        PracticeChart chart,
        PracticeSessionPlan plan)
    {
        return TryStart(chart, plan, PracticeGuidance.Default);
    }

    public static PracticeSessionStartResult TryStart(
        PracticeChart chart,
        PracticeSessionPlan plan,
        PracticeGuidance guidance)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(guidance);

        var range = plan.Range ?? new PracticeRange(ChartTime.Zero, chart.Duration);
        if (range.Start.CompareTo(ChartTime.Zero) < 0 ||
            range.End.CompareTo(range.Start) <= 0 ||
            range.End.CompareTo(chart.Duration) > 0)
        {
            return new PracticeSessionStartResult(null, PracticeStartError.InvalidRange);
        }

        if (plan.TempoRatio <= 0)
            return new PracticeSessionStartResult(null, PracticeStartError.InvalidTempo);
        if (plan.RequiredHands == RequiredHands.Both && plan.Accompaniment == Accompaniment.Automatic)
            return new PracticeSessionStartResult(null, PracticeStartError.InvalidAccompaniment);
        if (!ValidGuidance(guidance))
            return new PracticeSessionStartResult(null, PracticeStartError.InvalidGuidance);

        var targets = chart.Notes
            .Where(note => IsRequired(note.Hand, plan.RequiredHands))
            .Where(note => note.Onset.CompareTo(range.Start) >= 0 && note.Onset.CompareTo(range.End) < 0)
            .GroupBy(note => note.Onset)
            .OrderBy(group => group.Key)
            .Select(group => new PracticeTarget(
                group.Key,
                group.Select(note => note.Pitch).Distinct().OrderBy(pitch => pitch).ToArray()))
            .ToArray();
        if (targets.Length == 0)
            return new PracticeSessionStartResult(null, PracticeStartError.RequiredHandHasNoNotes);

        var audibleChartNoteIds = plan.Accompaniment == Accompaniment.Automatic
            ? chart.Notes
                .Where(note => !IsRequired(note.Hand, plan.RequiredHands))
                .Where(note => note.Onset.CompareTo(range.Start) >= 0 && note.Onset.CompareTo(range.End) < 0)
                .Select(note => note.Id)
                .ToArray()
            : Array.Empty<int>();
        var finalIncludedTail = chart.Notes
            .Where(note => note.Onset.CompareTo(range.Start) >= 0 && note.Onset.CompareTo(range.End) < 0)
            .Select(note => note.Onset.Microseconds + note.Duration.Microseconds)
            .DefaultIfEmpty(range.End.Microseconds)
            .Max();
        var playbackEnd = ChartTime.FromMicroseconds(
            Math.Min(
                chart.Duration.Microseconds,
                Math.Max(range.End.Microseconds, finalIncludedTail)));
        var beats = chart.Beats
            .Where(beat => beat.Position.CompareTo(range.Start) >= 0)
            .Where(beat => beat.Position.CompareTo(range.End) < 0)
            .ToArray();

        return new PracticeSessionStartResult(
            new PracticeSession(
                plan,
                guidance,
                range,
                playbackEnd,
                targets,
                audibleChartNoteIds,
                beats),
            Error: null);
    }

    public PracticeTransition Handle(PracticeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (signal.At.CompareTo(_lastSignalTime) < 0)
            return InvalidSignal(PracticeSignalError.OutOfOrder);
        if (!CanHandle(signal))
            return InvalidSignal(PracticeSignalError.InvalidForState);
        if (signal is PracticeSignal.Seek seekSignal &&
            (seekSignal.Position.CompareTo(_range.Start) < 0 || seekSignal.Position.CompareTo(_range.End) > 0))
        {
            return InvalidSignal(PracticeSignalError.InvalidPosition);
        }
        if (signal is PracticeSignal.ChangeGuidance guidanceChange && !ValidGuidance(guidanceChange.Guidance))
            return InvalidSignal(PracticeSignalError.InvalidGuidance);

        var clockEvents = new List<PracticeEvent>();
        var clockEffects = new List<PracticeEffect>();
        if (signal is not PracticeSignal.Begin && _snapshot.State != PracticeSessionState.Ready)
            AdvanceTime(signal.At, clockEvents, clockEffects);

        if (_snapshot.State is PracticeSessionState.Completed or PracticeSessionState.Abandoned)
        {
            _lastSignalTime = signal.At;
            return new PracticeTransition(_snapshot, clockEvents, clockEffects);
        }

        var transition = signal switch
        {
            PracticeSignal.Begin begin when _snapshot.State == PracticeSessionState.Ready => Begin(begin.At),
            PracticeSignal.Pulse => new PracticeTransition(_snapshot),
            PracticeSignal.NoteOn noteOn => NoteOn(noteOn.Pitch, noteOn.Velocity),
            PracticeSignal.Pause => Pause(),
            PracticeSignal.Resume resume => Resume(resume.At),
            PracticeSignal.Seek seek => Seek(seek.At, seek.Position),
            PracticeSignal.ChangeGuidance changed => ChangeGuidance(changed.Guidance),
            PracticeSignal.Abandon => Abandon(),
            _ => InvalidSignal(PracticeSignalError.InvalidForState)
        };
        _lastSignalTime = signal.At;
        if (clockEvents.Count == 0 && clockEffects.Count == 0)
            return transition;

        return new PracticeTransition(
            transition.Snapshot,
            clockEvents.Concat(transition.Events).ToArray(),
            clockEffects.Concat(transition.Effects).ToArray());
    }

    private PracticeTransition Begin(SessionTime at)
    {
        var events = new List<PracticeEvent>
        {
            new PracticeEvent.SessionStarted(
                _plan.Mode,
                _plan.RequiredHands,
                _plan.Accompaniment,
                _plan.TempoRatio,
                _range)
        };
        var effects = new List<PracticeEffect>
        {
            new PracticeEffect.ConfigurePlayback(_audibleChartNoteIds, _plan.TempoRatio)
        };
        var initialState = StateAtCurrentPosition();

        if (_guidance.CountInBeats > 0)
        {
            StartCountIn(
                at,
                initialState,
                resumeAfterCountIn: false,
                events,
                effects);
        }
        else
        {
            _stateAfterCountIn = initialState;
            EnterPractice(
                resumed: false,
                completedCountIn: false,
                events,
                effects);
        }

        return new PracticeTransition(_snapshot, events, effects);
    }

    private void AdvanceTime(
        SessionTime at,
        ICollection<PracticeEvent> events,
        ICollection<PracticeEffect> effects)
    {
        if (_snapshot.State == PracticeSessionState.CountingIn)
        {
            AdvanceCountIn(at, events, effects);
            return;
        }

        AdvanceClock(at, events, effects);
    }

    private void AdvanceCountIn(
        SessionTime at,
        ICollection<PracticeEvent> events,
        ICollection<PracticeEffect> effects)
    {
        if (_countInStartedAt is not { } startedAt)
            return;

        var elapsed = at.Microseconds - startedAt.Microseconds;
        var clickCount = Math.Min(
            _activeCountInBeats,
            (int)(elapsed / _activeCountInBeatDuration.Microseconds) + 1);
        while (_countInClicksEmitted < clickCount)
        {
            effects.Add(new PracticeEffect.Click(
                PracticeClickSource.CountIn,
                _snapshot.Position,
                Accent: _countInClicksEmitted == 0));
            _countInClicksEmitted++;
        }
        _snapshot = _snapshot with
        {
            CountInBeatsRemaining = _activeCountInBeats - _countInClicksEmitted
        };

        if (elapsed < _activeCountInBeats * _activeCountInBeatDuration.Microseconds)
            return;

        _countInStartedAt = null;
        EnterPractice(
            _resumeAfterCountIn,
            completedCountIn: true,
            events,
            effects);
    }

    private void AdvanceClock(
        SessionTime at,
        ICollection<PracticeEvent> events,
        ICollection<PracticeEffect> effects)
    {
        if (_snapshot.State != PracticeSessionState.Running)
            return;

        var elapsedMicroseconds = at.Microseconds - _lastSignalTime.Microseconds;
        var chartMicroseconds = (long)(elapsedMicroseconds * _plan.TempoRatio);
        var previousPosition = _snapshot.Position;
        var destination = ChartTime.FromMicroseconds(
            Math.Min(_snapshot.Position.Microseconds + chartMicroseconds, _playbackEnd.Microseconds));
        var target = _plan.Mode == PracticeMode.WaitForNotes
            ? NextTarget is { } nextTarget && nextTarget.Onset.CompareTo(destination) <= 0
                ? nextTarget
                : null
            : null;

        if (target is not null)
        {
            _snapshot = _snapshot with
            {
                State = PracticeSessionState.WaitingForInput,
                Position = target.Onset,
                Target = target
            };
            events.Add(new PracticeEvent.TargetBecameDue(target));
            effects.Add(new PracticeEffect.PausePlayback(target.Onset));
        }
        else
        {
            var completed = destination == _playbackEnd;
            _snapshot = _snapshot with
            {
                State = completed
                    ? PracticeSessionState.Completed
                    : PracticeSessionState.Running,
                Position = destination,
                Target = null
            };
            if (completed)
            {
                events.Add(new PracticeEvent.SessionCompleted(destination));
                effects.Add(new PracticeEffect.StopPlayback(destination));
            }
        }

        AddMetronomeClicks(previousPosition, _snapshot.Position, effects);

    }

    private void StartCountIn(
        SessionTime at,
        PracticeSessionState stateAfterCountIn,
        bool resumeAfterCountIn,
        ICollection<PracticeEvent> events,
        ICollection<PracticeEffect> effects)
    {
        _activeCountInBeats = _guidance.CountInBeats;
        _activeCountInBeatDuration = _guidance.CountInBeatDuration;
        _stateAfterCountIn = stateAfterCountIn;
        _resumeAfterCountIn = resumeAfterCountIn;
        _countInStartedAt = at;
        _countInClicksEmitted = 1;
        _snapshot = _snapshot with
        {
            State = PracticeSessionState.CountingIn,
            Target = null,
            CountInBeatsRemaining = _activeCountInBeats - 1,
            ResumeCountInPending = false
        };
        events.Add(new PracticeEvent.CountInStarted(_activeCountInBeats));
        effects.Add(new PracticeEffect.PausePlayback(_snapshot.Position));
        effects.Add(new PracticeEffect.Click(
            PracticeClickSource.CountIn,
            _snapshot.Position,
            Accent: true));
    }

    private void EnterPractice(
        bool resumed,
        bool completedCountIn,
        ICollection<PracticeEvent> events,
        ICollection<PracticeEffect> effects)
    {
        var target = NextTarget is { } nextTarget && nextTarget.Onset == _snapshot.Position
            ? nextTarget
            : null;
        var state = _stateAfterCountIn == PracticeSessionState.WaitingForInput && target is not null
            ? PracticeSessionState.WaitingForInput
            : PracticeSessionState.Running;
        _snapshot = _snapshot with
        {
            State = state,
            Target = state == PracticeSessionState.WaitingForInput ? target : null,
            CountInBeatsRemaining = 0,
            ResumeCountInPending = false
        };
        if (completedCountIn)
            events.Add(new PracticeEvent.CountInCompleted());
        if (resumed)
            events.Add(new PracticeEvent.SessionResumed(_snapshot.Position));
        if (target is not null && state == PracticeSessionState.WaitingForInput)
            events.Add(new PracticeEvent.TargetBecameDue(target));

        AddMetronomeClickAt(_snapshot.Position, effects);
        effects.Add(state == PracticeSessionState.WaitingForInput
            ? new PracticeEffect.PausePlayback(_snapshot.Position)
            : new PracticeEffect.StartPlayback(_snapshot.Position));
    }

    private PracticeSessionState StateAtCurrentPosition()
    {
        var target = NextTarget is { } nextTarget && nextTarget.Onset == _snapshot.Position
            ? nextTarget
            : null;
        return _plan.Mode == PracticeMode.WaitForNotes && target is not null
            ? PracticeSessionState.WaitingForInput
            : PracticeSessionState.Running;
    }

    private void AddMetronomeClicks(
        ChartTime fromExclusive,
        ChartTime toInclusive,
        ICollection<PracticeEffect> effects)
    {
        if (!_guidance.MetronomeEnabled)
            return;

        foreach (var beat in _beats.Where(beat =>
                     beat.Position.CompareTo(fromExclusive) > 0 &&
                     beat.Position.CompareTo(toInclusive) <= 0))
        {
            effects.Add(new PracticeEffect.Click(
                PracticeClickSource.Metronome,
                beat.Position,
                beat.IsDownbeat));
        }
    }

    private void AddMetronomeClickAt(
        ChartTime position,
        ICollection<PracticeEffect> effects)
    {
        if (!_guidance.MetronomeEnabled)
            return;
        var beat = _beats.FirstOrDefault(item => item.Position == position);
        if (beat is null)
            return;
        effects.Add(new PracticeEffect.Click(
            PracticeClickSource.Metronome,
            beat.Position,
            beat.IsDownbeat));
    }

    private PracticeTransition NoteOn(byte pitch, byte velocity)
    {
        var events = new List<PracticeEvent>
        {
            new PracticeEvent.LearnerNoteObserved(pitch, velocity, _snapshot.Position)
        };
        if (_snapshot.State is not (PracticeSessionState.WaitingForInput or PracticeSessionState.LearnerPaused) ||
            _snapshot.Target is null)
            return new PracticeTransition(_snapshot, events, Array.Empty<PracticeEffect>());

        var target = NextTarget ?? _snapshot.Target;
        var remainingPitches = _snapshot.Target.Pitches.ToList();
        if (!remainingPitches.Remove(pitch))
            return new PracticeTransition(_snapshot, events, Array.Empty<PracticeEffect>());

        if (remainingPitches.Count == 0)
        {
            _targetIndex++;
            events.Add(new PracticeEvent.TargetSatisfied(target));
            var wasLearnerPaused = _snapshot.State == PracticeSessionState.LearnerPaused;
            _snapshot = _snapshot with
            {
                State = wasLearnerPaused
                    ? PracticeSessionState.LearnerPaused
                    : PracticeSessionState.Running,
                Target = null
            };

            return new PracticeTransition(
                _snapshot,
                events,
                wasLearnerPaused
                    ? Array.Empty<PracticeEffect>()
                    : new PracticeEffect[] { new PracticeEffect.StartPlayback(_snapshot.Position) });
        }

        _snapshot = _snapshot with
        {
            Target = _snapshot.Target with { Pitches = remainingPitches.ToArray() }
        };

        return new PracticeTransition(_snapshot, events, Array.Empty<PracticeEffect>());
    }

    private PracticeTransition Pause()
    {
        var resumeCountInPending = _snapshot.State == PracticeSessionState.CountingIn ||
                                   _snapshot.ResumeCountInPending;
        _countInStartedAt = null;
        _snapshot = _snapshot with
        {
            State = PracticeSessionState.LearnerPaused,
            CountInBeatsRemaining = 0,
            ResumeCountInPending = resumeCountInPending
        };
        return new PracticeTransition(
            _snapshot,
            new PracticeEvent[] { new PracticeEvent.SessionPaused(_snapshot.Position) },
            new PracticeEffect[] { new PracticeEffect.PausePlayback(_snapshot.Position) });
    }

    private PracticeTransition Resume(SessionTime at)
    {
        if (_snapshot.ResumeCountInPending && _guidance.CountInBeats > 0)
        {
            var countInEvents = new List<PracticeEvent>();
            var countInEffects = new List<PracticeEffect>();
            StartCountIn(
                at,
                StateAtCurrentPosition(),
                resumeAfterCountIn: true,
                countInEvents,
                countInEffects);
            return new PracticeTransition(_snapshot, countInEvents, countInEffects);
        }

        _snapshot = _snapshot with
        {
            State = _snapshot.Target is null
                ? PracticeSessionState.Running
                : PracticeSessionState.WaitingForInput,
            ResumeCountInPending = false
        };
        var effects = new List<PracticeEffect>();
        AddMetronomeClickAt(_snapshot.Position, effects);
        effects.Add(_snapshot.State == PracticeSessionState.Running
            ? new PracticeEffect.StartPlayback(_snapshot.Position)
            : new PracticeEffect.PausePlayback(_snapshot.Position));
        return new PracticeTransition(
            _snapshot,
            new PracticeEvent[] { new PracticeEvent.SessionResumed(_snapshot.Position) },
            effects);
    }

    private PracticeTransition Seek(SessionTime at, ChartTime position)
    {
        if (position == _snapshot.Position)
            return new PracticeTransition(_snapshot);

        var runningIntent = _snapshot.State is PracticeSessionState.Running or
            PracticeSessionState.WaitingForInput or
            PracticeSessionState.CountingIn;
        var previousPosition = _snapshot.Position;
        _countInStartedAt = null;
        _snapshot = _snapshot with { State = PracticeSessionState.Seeking };
        _targetIndex = 0;
        while (_targetIndex < _targets.Count && _targets[_targetIndex].Onset.CompareTo(position) < 0)
            _targetIndex++;

        var target = _plan.Mode == PracticeMode.WaitForNotes &&
                     NextTarget is { } nextTarget &&
                     nextTarget.Onset == position
            ? nextTarget
            : null;
        var destinationState = position == _playbackEnd
            ? PracticeSessionState.Completed
            : runningIntent
                ? _plan.Mode == PracticeMode.WaitForNotes && target is not null
                    ? PracticeSessionState.WaitingForInput
                    : PracticeSessionState.Running
                : PracticeSessionState.LearnerPaused;
        _snapshot = _snapshot with
        {
            State = destinationState,
            Position = position,
            Target = target,
            CountInBeatsRemaining = 0,
            ResumeCountInPending = !runningIntent &&
                                   position != _playbackEnd &&
                                   _guidance.CountInBeats > 0
        };

        var effects = new List<PracticeEffect>
        {
            new PracticeEffect.SeekPlayback(position)
        };

        var events = new List<PracticeEvent>
        {
            new PracticeEvent.SessionSeeking(previousPosition, position),
            new PracticeEvent.AssistanceUsed(PracticeAssistance.Seek, position)
        };
        if (destinationState == PracticeSessionState.Completed)
        {
            events.Add(new PracticeEvent.SessionCompleted(position));
            effects.Add(new PracticeEffect.StopPlayback(position));
        }
        else if (!runningIntent)
        {
            effects.Add(new PracticeEffect.PausePlayback(position));
        }
        else if (_guidance.CountInBeats > 0)
        {
            StartCountIn(
                at,
                destinationState,
                resumeAfterCountIn: false,
                events,
                effects);
        }
        else
        {
            AddMetronomeClickAt(position, effects);
            effects.Add(destinationState == PracticeSessionState.Running
                ? new PracticeEffect.StartPlayback(position)
                : new PracticeEffect.PausePlayback(position));
        }

        return new PracticeTransition(_snapshot, events, effects);
    }

    private PracticeTransition ChangeGuidance(PracticeGuidance guidance)
    {
        _guidance = guidance;
        return new PracticeTransition(_snapshot);
    }

    private PracticeTransition Abandon()
    {
        _countInStartedAt = null;
        _snapshot = _snapshot with
        {
            State = PracticeSessionState.Abandoned,
            CountInBeatsRemaining = 0,
            ResumeCountInPending = false
        };
        return new PracticeTransition(
            _snapshot,
            new PracticeEvent[] { new PracticeEvent.SessionAbandoned(_snapshot.Position) },
            new PracticeEffect[] { new PracticeEffect.StopPlayback(_snapshot.Position) });
    }

    private PracticeTarget? NextTarget =>
        _targetIndex < _targets.Count ? _targets[_targetIndex] : null;

    private bool CanHandle(PracticeSignal signal)
    {
        return signal switch
        {
            PracticeSignal.Begin => _snapshot.State == PracticeSessionState.Ready,
            PracticeSignal.Pulse => _snapshot.State is PracticeSessionState.CountingIn or
                PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.NoteOn => _snapshot.State is PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.Pause => _snapshot.State is PracticeSessionState.CountingIn or
                PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput,
            PracticeSignal.Resume => _snapshot.State == PracticeSessionState.LearnerPaused,
            PracticeSignal.Seek => _snapshot.State is PracticeSessionState.CountingIn or
                PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.ChangeGuidance => _snapshot.State is PracticeSessionState.CountingIn or
                PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.Abandon => _snapshot.State is PracticeSessionState.CountingIn or
                PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            _ => false
        };
    }

    private PracticeTransition InvalidSignal(PracticeSignalError error)
    {
        return new PracticeTransition(
            _snapshot,
            Array.Empty<PracticeEvent>(),
            Array.Empty<PracticeEffect>(),
            error);
    }

    private static bool IsRequired(PianoHand hand, RequiredHands requiredHands)
    {
        return requiredHands == RequiredHands.Both ||
               requiredHands == RequiredHands.Left && hand == PianoHand.Left ||
               requiredHands == RequiredHands.Right && hand == PianoHand.Right;
    }

    private static bool ValidGuidance(PracticeGuidance guidance)
    {
        return guidance.CountInBeats >= 0 &&
               guidance.CountInBeatDuration.CompareTo(SessionTime.Zero) > 0;
    }
}
