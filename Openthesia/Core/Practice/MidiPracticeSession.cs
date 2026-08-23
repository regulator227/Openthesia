using System.Diagnostics;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Core.Midi;
using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public static class MidiPracticeSession
{
    private static readonly object Sync = new();
    private static PracticeSession? _session;
    private static PracticeChart? _chart;
    private static PracticeSessionPlan? _plan;
    private static Stopwatch? _signalClock;

    public static bool IsActive
    {
        get
        {
            lock (Sync)
                return _session is not null;
        }
    }

    public static PracticeSessionSnapshot? Snapshot
    {
        get
        {
            lock (Sync)
                return _session?.Snapshot;
        }
    }

    public static string? Start(PracticePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.Mode != PracticeMode.WaitForNotes)
            return "This Practice Mode is not available yet.";
        if (MidiFileData.Context is not { } context || MidiFileData.MidiFile is null)
            return "Open a Chart from a MIDI Source before starting Practice.";
        if (LeftRightData.S_IsRightNote.Count != MidiFileData.Notes.Count())
            return "Hand Assignments do not match this Chart.";

        var hands = LeftRightData.S_IsRightNote
            .Select(isRight => isRight ? PianoHand.Right : PianoHand.Left)
            .ToArray();
        var chart = PracticeChartFactory.FromMidi(context.ChartId, MidiFileData.MidiFile, hands);
        var plan = PracticeSessionPlan.FullChart(
            preferences.Mode,
            preferences.RequiredHands,
            preferences.Accompaniment,
            preferences.TempoRatio);

        lock (Sync)
        {
            DeactivateCore(abandon: true);
            return StartCore(chart, plan);
        }
    }

    public static void Advance()
    {
        lock (Sync)
        {
            if (_session is null)
                return;

            Apply(_session.Handle(new PracticeSignal.Pulse(CurrentSessionTime)));
        }
    }

    public static void ObserveNoteOn(byte pitch, byte velocity)
    {
        lock (Sync)
        {
            if (_session is null)
                return;

            Apply(_session.Handle(new PracticeSignal.NoteOn(CurrentSessionTime, pitch, velocity)));
        }
    }

    public static void TogglePause()
    {
        lock (Sync)
        {
            if (_session is null)
                return;

            var signal = _session.Snapshot.State == PracticeSessionState.LearnerPaused
                ? (PracticeSignal)new PracticeSignal.Resume(CurrentSessionTime)
                : new PracticeSignal.Pause(CurrentSessionTime);
            Apply(_session.Handle(signal));
        }
    }

    public static void Pause()
    {
        lock (Sync)
        {
            if (_session is null)
                return;

            Apply(_session.Handle(new PracticeSignal.Pause(CurrentSessionTime)));
        }
    }

    public static void Resume()
    {
        lock (Sync)
        {
            if (_session is null)
                return;

            Apply(_session.Handle(new PracticeSignal.Resume(CurrentSessionTime)));
        }
    }

    public static void Seek(ChartTime position)
    {
        lock (Sync)
        {
            if (_session is null || _chart is null || _plan is null)
                return;

            var start = _plan.Range?.Start ?? ChartTime.Zero;
            var end = _plan.Range?.End ?? _chart.Duration;
            var clamped = ChartTime.FromMicroseconds(
                Math.Clamp(position.Microseconds, start.Microseconds, end.Microseconds));
            Apply(_session.Handle(new PracticeSignal.Seek(CurrentSessionTime, clamped)));
        }
    }

    public static string? Restart()
    {
        lock (Sync)
        {
            if (_chart is null || _plan is null)
                return "There is no Practice Session to restart.";

            var chart = _chart;
            var plan = _plan;
            DeactivateCore(abandon: true);
            return StartCore(chart, plan);
        }
    }

    public static void Deactivate()
    {
        lock (Sync)
            DeactivateCore(abandon: true);
    }

    private static string? StartCore(PracticeChart chart, PracticeSessionPlan plan)
    {
        var started = PracticeSession.TryStart(chart, plan);
        if (started.Session is null)
            return StartErrorMessage(started.Error);

        _chart = chart;
        _plan = plan;
        _session = started.Session;
        _signalClock = Stopwatch.StartNew();
        MovePlaybackTo(chartTime: plan.Range?.Start ?? ChartTime.Zero);
        Apply(_session.Handle(new PracticeSignal.Begin(SessionTime.Zero)));
        return null;
    }

    private static void DeactivateCore(bool abandon)
    {
        if (abandon && _session is not null)
            Apply(_session.Handle(new PracticeSignal.Abandon(CurrentSessionTime)));

        _session = null;
        _chart = null;
        _plan = null;
        _signalClock = null;
        PracticePlaybackFilter.Disable();
    }

    private static void Apply(PracticeTransition transition)
    {
        if (transition.Error is not null)
            return;

        foreach (var effect in transition.Effects)
        {
            switch (effect)
            {
                case PracticeEffect.ConfigurePlayback configure:
                    PracticePlaybackFilter.Configure(configure.AudibleChartNoteIds);
                    if (MidiPlayer.Playback is not null)
                        MidiPlayer.Playback.Speed = (double)configure.TempoRatio;
                    break;
                case PracticeEffect.StartPlayback start:
                    MovePlaybackTo(start.From);
                    MidiPlayer.Playback?.Start();
                    MidiPlayer.StartTimer();
                    break;
                case PracticeEffect.PausePlayback pause:
                    MidiPlayer.Playback?.Stop();
                    MidiPlayer.StopTimer();
                    MovePlaybackTo(pause.At);
                    break;
                case PracticeEffect.SeekPlayback seek:
                    MovePlaybackTo(seek.To);
                    break;
                case PracticeEffect.StopPlayback stop:
                    MidiPlayer.Playback?.Stop();
                    MidiPlayer.StopTimer();
                    MovePlaybackTo(stop.At);
                    break;
            }
        }
    }

    private static void MovePlaybackTo(ChartTime chartTime)
    {
        MidiPlayer.Playback?.MoveToTime(new MetricTimeSpan(chartTime.Microseconds));
        MidiPlayer.Microseconds = chartTime.Microseconds;
        MidiPlayer.Milliseconds = chartTime.Microseconds / 1_000f;
        MidiPlayer.Seconds = chartTime.Microseconds / 1_000_000f;
        MidiPlayer.Timer = MidiPlayer.Seconds * 100 * ScreenCanvasControls.FallSpeedVal;
    }

    private static string StartErrorMessage(PracticeStartError? error)
    {
        return error switch
        {
            PracticeStartError.RequiredHandHasNoNotes => "The Required Hands selection has no notes in this Chart.",
            PracticeStartError.InvalidAccompaniment => "Automatic Accompaniment requires Left or Right Required Hands.",
            PracticeStartError.InvalidTempo => "Choose a positive Practice tempo.",
            PracticeStartError.InvalidRange => "The selected Practice range is invalid.",
            _ => "The Practice Session could not be started."
        };
    }

    private static SessionTime CurrentSessionTime =>
        SessionTime.FromMicroseconds(
            (long)((_signalClock?.Elapsed.TotalSeconds ?? 0) * 1_000_000));
}
