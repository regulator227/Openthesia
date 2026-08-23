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

public sealed class PracticeChart
{
    public PracticeChart(
        ChartId id,
        ChartTime duration,
        IReadOnlyList<PracticeChartNote> notes)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(notes);
        if (duration.CompareTo(ChartTime.Zero) <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        Id = id;
        Duration = duration;
        Notes = notes.ToArray();
    }

    public ChartId Id { get; }
    public ChartTime Duration { get; }
    public IReadOnlyList<PracticeChartNote> Notes { get; }
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

public sealed record PracticeRange(ChartTime Start, ChartTime End);

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
    PracticeTarget? Target);

public abstract record PracticeSignal(SessionTime At)
{
    public sealed record Begin(SessionTime At) : PracticeSignal(At);
    public sealed record Pulse(SessionTime At) : PracticeSignal(At);
    public sealed record NoteOn(SessionTime At, byte Pitch, byte Velocity) : PracticeSignal(At);
    public sealed record Pause(SessionTime At) : PracticeSignal(At);
    public sealed record Resume(SessionTime At) : PracticeSignal(At);
    public sealed record Seek(SessionTime At, ChartTime Position) : PracticeSignal(At);
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
    InvalidPosition
}

public enum PracticeStartError
{
    InvalidRange,
    InvalidTempo,
    RequiredHandHasNoNotes,
    InvalidAccompaniment
}

public sealed record PracticeSessionStartResult(
    PracticeSession? Session,
    PracticeStartError? Error);

public sealed class PracticeSession
{
    private readonly PracticeSessionPlan _plan;
    private readonly PracticeRange _range;
    private readonly IReadOnlyList<PracticeTarget> _targets;
    private readonly IReadOnlyList<int> _audibleChartNoteIds;
    private PracticeSessionSnapshot _snapshot;
    private SessionTime _lastSignalTime;
    private int _targetIndex;

    private PracticeSession(
        PracticeSessionPlan plan,
        PracticeRange range,
        IReadOnlyList<PracticeTarget> targets,
        IReadOnlyList<int> audibleChartNoteIds)
    {
        _plan = plan;
        _range = range;
        _targets = targets;
        _audibleChartNoteIds = audibleChartNoteIds;
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
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(plan);

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

        return new PracticeSessionStartResult(
            new PracticeSession(plan, range, targets, audibleChartNoteIds),
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

        var clockEvents = new List<PracticeEvent>();
        var clockEffects = new List<PracticeEffect>();
        if (signal is not PracticeSignal.Begin && _snapshot.State != PracticeSessionState.Ready)
            AdvanceClock(signal.At, clockEvents, clockEffects);

        if (_snapshot.State is PracticeSessionState.Completed or PracticeSessionState.Abandoned)
        {
            _lastSignalTime = signal.At;
            return new PracticeTransition(_snapshot, clockEvents, clockEffects);
        }

        var transition = signal switch
        {
            PracticeSignal.Begin when _snapshot.State == PracticeSessionState.Ready => Begin(),
            PracticeSignal.Pulse => new PracticeTransition(_snapshot),
            PracticeSignal.NoteOn noteOn => NoteOn(noteOn.Pitch, noteOn.Velocity),
            PracticeSignal.Pause => Pause(),
            PracticeSignal.Resume => Resume(),
            PracticeSignal.Seek seek => Seek(seek.Position),
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

    private PracticeTransition Begin()
    {
        var target = NextTarget;
        var state = _plan.Mode == PracticeMode.WaitForNotes && target?.Onset == _snapshot.Position
            ? PracticeSessionState.WaitingForInput
            : PracticeSessionState.Running;
        _snapshot = _snapshot with
        {
            State = state,
            Target = state == PracticeSessionState.WaitingForInput ? target : null
        };

        var events = new List<PracticeEvent>
        {
            new PracticeEvent.SessionStarted(
                _plan.Mode,
                _plan.RequiredHands,
                _plan.Accompaniment,
                _plan.TempoRatio,
                _range)
        };
        if (target is not null && state == PracticeSessionState.WaitingForInput)
            events.Add(new PracticeEvent.TargetBecameDue(target));

        var effects = new List<PracticeEffect>
        {
            new PracticeEffect.ConfigurePlayback(_audibleChartNoteIds, _plan.TempoRatio)
        };
        effects.Add(state == PracticeSessionState.WaitingForInput
            ? new PracticeEffect.PausePlayback(_snapshot.Position)
            : new PracticeEffect.StartPlayback(_snapshot.Position));

        return new PracticeTransition(_snapshot, events, effects);
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
        var destination = ChartTime.FromMicroseconds(
            Math.Min(_snapshot.Position.Microseconds + chartMicroseconds, _range.End.Microseconds));
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
            var completed = destination == _range.End;
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
        _snapshot = _snapshot with { State = PracticeSessionState.LearnerPaused };
        return new PracticeTransition(
            _snapshot,
            new PracticeEvent[] { new PracticeEvent.SessionPaused(_snapshot.Position) },
            new PracticeEffect[] { new PracticeEffect.PausePlayback(_snapshot.Position) });
    }

    private PracticeTransition Resume()
    {
        _snapshot = _snapshot with
        {
            State = _snapshot.Target is null
                ? PracticeSessionState.Running
                : PracticeSessionState.WaitingForInput
        };
        return new PracticeTransition(
            _snapshot,
            new PracticeEvent[] { new PracticeEvent.SessionResumed(_snapshot.Position) },
            _snapshot.State == PracticeSessionState.Running
                ? new PracticeEffect[] { new PracticeEffect.StartPlayback(_snapshot.Position) }
                : new PracticeEffect[] { new PracticeEffect.PausePlayback(_snapshot.Position) });
    }

    private PracticeTransition Seek(ChartTime position)
    {
        var runningIntent = _snapshot.State is PracticeSessionState.Running or PracticeSessionState.WaitingForInput;
        _snapshot = _snapshot with { State = PracticeSessionState.Seeking };
        _targetIndex = 0;
        while (_targetIndex < _targets.Count && _targets[_targetIndex].Onset.CompareTo(position) < 0)
            _targetIndex++;

        var target = NextTarget is { } nextTarget && nextTarget.Onset == position
            ? nextTarget
            : null;
        var state = position == _range.End
            ? PracticeSessionState.Completed
            : runningIntent
                ? _plan.Mode == PracticeMode.WaitForNotes && target is not null
                    ? PracticeSessionState.WaitingForInput
                    : PracticeSessionState.Running
                : PracticeSessionState.LearnerPaused;
        _snapshot = _snapshot with
        {
            State = state,
            Position = position,
            Target = target
        };

        var effects = new List<PracticeEffect>
        {
            new PracticeEffect.SeekPlayback(position)
        };
        effects.Add(state switch
        {
            PracticeSessionState.Running => new PracticeEffect.StartPlayback(position),
            PracticeSessionState.Completed => new PracticeEffect.StopPlayback(position),
            _ => new PracticeEffect.PausePlayback(position)
        });

        var events = new List<PracticeEvent>
        {
            new PracticeEvent.AssistanceUsed(PracticeAssistance.Seek, position)
        };
        if (state == PracticeSessionState.Completed)
            events.Add(new PracticeEvent.SessionCompleted(position));

        return new PracticeTransition(_snapshot, events, effects);
    }

    private PracticeTransition Abandon()
    {
        _snapshot = _snapshot with { State = PracticeSessionState.Abandoned };
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
            PracticeSignal.Pulse => _snapshot.State is PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.NoteOn => _snapshot.State is PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.Pause => _snapshot.State is PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput,
            PracticeSignal.Resume => _snapshot.State == PracticeSessionState.LearnerPaused,
            PracticeSignal.Seek => _snapshot.State is PracticeSessionState.Running or
                PracticeSessionState.WaitingForInput or
                PracticeSessionState.LearnerPaused,
            PracticeSignal.Abandon => _snapshot.State is PracticeSessionState.Running or
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
}
