using System.Diagnostics;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Core.Midi;
using Openthesia.Core.Plugins;
using Openthesia.Core.Songs;
using Openthesia.Settings;

namespace Openthesia.Core.Practice;

public static class MidiPracticeSession
{
    private static readonly object Sync = new();
    private static PracticeSession? _session;
    private static PracticeChart? _chart;
    private static PracticeSessionPlan? _plan;
    private static PracticeAssessment? _assessment;
    private static LearnerId? _learnerId;
    private static Stopwatch? _signalClock;
    private static IReadOnlyList<PracticeFeedback> _latestFeedback = Array.Empty<PracticeFeedback>();
    private static DateTimeOffset _feedbackExpiresAtUtc;
    private static PracticeResult? _latestResult;
    private static PracticeProgress? _latestProgress;
    private static string? _progressWarning;
    private static PracticePreferences? _preferences;
    private static PracticeNavigation _navigation = PracticeNavigation.Empty;
    private static Guid? _activeLoopId;
    private static string? _navigationWarning;

    private const int ClickChannel = 9;
    private const int ClickNote = 77;
    private const int AccentClickNote = 76;

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

    public static PracticeMode? Mode
    {
        get
        {
            lock (Sync)
                return _plan?.Mode;
        }
    }

    public static PracticePreferences? Preferences
    {
        get
        {
            lock (Sync)
                return _preferences;
        }
    }

    public static PracticeAccessibilityDescription? AccessibilityDescription
    {
        get
        {
            lock (Sync)
            {
                return _chart is not null && _session is not null
                    ? PracticeAccessibility.Describe(
                        _chart,
                        _session.Snapshot,
                        DateTimeOffset.UtcNow <= _feedbackExpiresAtUtc
                            ? _latestFeedback
                            : Array.Empty<PracticeFeedback>(),
                        _navigation,
                        ActiveLoopCore())
                    : null;
            }
        }
    }

    public static PracticeNavigation Navigation
    {
        get
        {
            lock (Sync)
                return _navigation;
        }
    }

    public static PracticeLoop? ActiveLoop
    {
        get
        {
            lock (Sync)
                return _activeLoopId is { } id
                    ? _navigation.Loops.FirstOrDefault(loop => loop.Id == id)
                    : null;
        }
    }

    public static ChartTime? ChartDuration
    {
        get
        {
            lock (Sync)
                return _chart?.Duration;
        }
    }

    public static string? NavigationWarning
    {
        get
        {
            lock (Sync)
                return _navigationWarning;
        }
    }

    public static bool CanRestartAfterError
    {
        get
        {
            lock (Sync)
            {
                return _assessment?.HasRecordedError == true &&
                       _session?.Snapshot.State is PracticeSessionState.CountingIn or
                           PracticeSessionState.Running or
                           PracticeSessionState.WaitingForInput or
                           PracticeSessionState.LearnerPaused;
            }
        }
    }

    public static IReadOnlyList<PracticeFeedback> LatestFeedback
    {
        get
        {
            lock (Sync)
                return DateTimeOffset.UtcNow <= _feedbackExpiresAtUtc
                    ? _latestFeedback
                    : Array.Empty<PracticeFeedback>();
        }
    }

    public static PracticeResult? LatestResult
    {
        get
        {
            lock (Sync)
                return _latestResult;
        }
    }

    public static PracticeProgress? LatestProgress
    {
        get
        {
            lock (Sync)
                return _latestProgress;
        }
    }

    public static string? ProgressWarning
    {
        get
        {
            lock (Sync)
                return _progressWarning;
        }
    }

    public static PracticeNavigationLoadResult LoadNavigation()
    {
        if (ProgramData.ActiveLearner is not { } learner ||
            MidiFileData.Context is not { } context ||
            MidiFileData.MidiFile is null)
        {
            return new PracticeNavigationLoadResult(
                PracticeNavigation.Empty,
                "Learner and Chart identity are required to load loops and bookmarks.");
        }

        var duration = ChartTime.FromMicroseconds(
            MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalMicroseconds);
        var loaded = new PracticeNavigationStore(ProgramData.DataPath).Load(
            learner.Id,
            context.ChartId,
            duration);
        lock (Sync)
        {
            _navigation = loaded.Navigation;
            _navigationWarning = loaded.Warning;
            if (_activeLoopId is { } activeLoopId &&
                _navigation.Loops.All(loop => loop.Id != activeLoopId))
            {
                _activeLoopId = null;
            }
        }
        return loaded;
    }

    public static string? Start(PracticePreferences preferences, Guid? activeLoopId = null)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (ProgramData.ActiveLearner is not { } learner)
            return "Choose a Learner before starting Practice.";
        if (MidiFileData.Context is not { } context || MidiFileData.MidiFile is null)
            return "Open a Chart from a MIDI Source before starting Practice.";
        if (LeftRightData.S_IsRightNote.Count != MidiFileData.Notes.Count())
            return "Hand Assignments do not match this Chart.";

        var hands = LeftRightData.S_IsRightNote
            .Select(isRight => isRight ? PianoHand.Right : PianoHand.Left)
            .ToArray();
        var chart = PracticeChartFactory.FromMidi(context.ChartId, MidiFileData.MidiFile, hands);
        var loadedNavigation = new PracticeNavigationStore(ProgramData.DataPath).Load(
            learner.Id,
            context.ChartId,
            chart.Duration);
        var activeLoop = activeLoopId is { } loopId
            ? loadedNavigation.Navigation.Loops.FirstOrDefault(loop => loop.Id == loopId)
            : null;
        if (activeLoopId is not null && activeLoop is null)
            return "The selected loop is no longer available.";
        var plan = CreatePlan(preferences, activeLoop?.Range);

        lock (Sync)
        {
            DeactivateCore(abandon: true);
            _learnerId = learner.Id;
            _preferences = preferences;
            _navigation = loadedNavigation.Navigation;
            _navigationWarning = loadedNavigation.Warning;
            _activeLoopId = activeLoop?.Id;
            return StartCore(chart, plan, preserveLatestResult: false);
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

            var chartPosition = ChartTime.FromMicroseconds(
                Math.Clamp(position.Microseconds, 0, _chart.Duration.Microseconds));
            var activeLoop = ActiveLoopCore();
            if (activeLoop is not null && !activeLoop.Range.Contains(chartPosition))
            {
                StartAssistedAttemptAt(chartPosition, disableLoop: true);
                return;
            }

            Apply(_session.Handle(new PracticeSignal.Seek(CurrentSessionTime, chartPosition)));
        }
    }

    public static string? UpdatePreferences(PracticePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        lock (Sync)
        {
            if (_learnerId is not { } learnerId || _chart is null)
                return "There is no active Practice Session to update.";

            var previous = _preferences;
            var comparableSetupChanged = previous is null ||
                previous.Mode != preferences.Mode ||
                previous.RequiredHands != preferences.RequiredHands ||
                previous.Accompaniment != preferences.Accompaniment ||
                previous.TempoRatio != preferences.TempoRatio;
            PracticeSessionPlan? replacementPlan = null;
            if (comparableSetupChanged && _session is not null)
            {
                replacementPlan = CreatePlan(preferences, ActiveLoopCore()?.Range);
                var validation = PracticeSession.TryStart(
                    _chart,
                    replacementPlan,
                    CreateGuidance(_chart, replacementPlan, preferences));
                if (validation.Session is null)
                    return StartErrorMessage(validation.Error);
            }

            var saved = new PracticePreferencesStore(ProgramData.DataPath).Save(
                learnerId,
                _chart.Id,
                preferences);
            if (!saved.Saved)
                return saved.Warning;

            _preferences = preferences;
            if (_session is null || _plan is null)
                return null;
            if (replacementPlan is not null)
                return ReplaceSession(_chart, replacementPlan, preserveLatestResult: true);

            var guidance = CreateGuidance(_chart, _plan, preferences);
            Apply(_session.Handle(new PracticeSignal.ChangeGuidance(
                CurrentSessionTime,
                guidance)));
            return null;
        }
    }

    public static string? SetActiveLoop(Guid? loopId)
    {
        lock (Sync)
        {
            var loop = loopId is { } id
                ? _navigation.Loops.FirstOrDefault(item => item.Id == id)
                : null;
            if (loopId is not null && loop is null)
                return "The selected loop is no longer available.";

            if (_session is null || _chart is null || _preferences is null)
            {
                _activeLoopId = loop?.Id;
                return null;
            }

            var plan = CreatePlan(_preferences, loop?.Range);
            var warning = ReplaceSession(_chart, plan, preserveLatestResult: true);
            if (warning is null)
                _activeLoopId = loop?.Id;
            return warning;
        }
    }

    public static string? SaveLoop(Guid id, string? name, PracticeRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        lock (Sync)
        {
            if (!TryGetNavigationIdentity(out var learnerId, out var chartId, out var duration))
                return "Learner and Chart identity are required to save a loop.";

            var previousLoop = _navigation.Loops.FirstOrDefault(loop => loop.Id == id);
            var candidate = _navigation.SaveLoop(id, name, range);
            var saved = new PracticeNavigationStore(ProgramData.DataPath).Save(
                learnerId,
                chartId,
                duration,
                candidate);
            if (!saved.Saved)
                return SetNavigationWarning(saved.Warning);

            _navigation = candidate;
            _navigationWarning = null;
            if (_activeLoopId == id &&
                previousLoop?.Range != range &&
                _chart is not null &&
                _preferences is not null)
            {
                var plan = CreatePlan(_preferences, range);
                return ReplaceSession(_chart, plan, preserveLatestResult: true);
            }
            return null;
        }
    }

    public static string? DeleteLoop(Guid id)
    {
        lock (Sync)
        {
            if (!TryGetNavigationIdentity(out var learnerId, out var chartId, out var duration))
                return "Learner and Chart identity are required to delete a loop.";
            var candidate = _navigation.DeleteLoop(id);
            var saved = new PracticeNavigationStore(ProgramData.DataPath).Save(
                learnerId,
                chartId,
                duration,
                candidate);
            if (!saved.Saved)
                return SetNavigationWarning(saved.Warning);

            _navigation = candidate;
            _navigationWarning = null;
            return _activeLoopId == id ? SetActiveLoop(null) : null;
        }
    }

    public static string? SaveBookmark(Guid id, string? name, ChartTime position)
    {
        lock (Sync)
        {
            if (!TryGetNavigationIdentity(out var learnerId, out var chartId, out var duration))
                return "Learner and Chart identity are required to save a bookmark.";
            var candidate = _navigation.SaveBookmark(id, name, position);
            var saved = new PracticeNavigationStore(ProgramData.DataPath).Save(
                learnerId,
                chartId,
                duration,
                candidate);
            if (!saved.Saved)
                return SetNavigationWarning(saved.Warning);

            _navigation = candidate;
            _navigationWarning = null;
            return null;
        }
    }

    public static string? DeleteBookmark(Guid id)
    {
        lock (Sync)
        {
            if (!TryGetNavigationIdentity(out var learnerId, out var chartId, out var duration))
                return "Learner and Chart identity are required to delete a bookmark.";
            var candidate = _navigation.DeleteBookmark(id);
            var saved = new PracticeNavigationStore(ProgramData.DataPath).Save(
                learnerId,
                chartId,
                duration,
                candidate);
            if (!saved.Saved)
                return SetNavigationWarning(saved.Warning);

            _navigation = candidate;
            _navigationWarning = null;
            return null;
        }
    }

    public static void GoToBookmark(Guid id)
    {
        lock (Sync)
        {
            var bookmark = _navigation.Bookmarks.FirstOrDefault(item => item.Id == id);
            if (bookmark is not null)
                Seek(bookmark.Position);
        }
    }

    public static void GoToLoopStart(Guid id)
    {
        lock (Sync)
        {
            var loop = _navigation.Loops.FirstOrDefault(item => item.Id == id);
            if (loop is not null)
                Seek(loop.Range.Start);
        }
    }

    public static void GoToBookmark(PracticeNavigationDirection direction)
    {
        lock (Sync)
        {
            if (_session is null)
                return;
            var bookmark = _navigation.FindBookmark(_session.Snapshot.Position, direction);
            if (bookmark is not null)
                Seek(bookmark.Position);
        }
    }

    public static ChartTime SnapToNearestBeat(ChartTime position)
    {
        lock (Sync)
        {
            if (_chart is null || _chart.Beats.Count == 0)
                return position;
            return _chart.Beats
                .OrderBy(beat => Math.Abs(beat.Position.Microseconds - position.Microseconds))
                .ThenBy(beat => beat.Position)
                .First()
                .Position;
        }
    }

    public static ChartTime SnapToNextBeatBoundary(ChartTime position)
    {
        lock (Sync)
        {
            if (_chart is null)
                return position;
            return _chart.Beats
                       .Where(beat => beat.Position.CompareTo(position) > 0)
                       .Select(beat => (ChartTime?)beat.Position)
                       .FirstOrDefault() ?? _chart.Duration;
        }
    }

    public static string? RestartAfterError()
    {
        lock (Sync)
        {
            if (_chart is null || _plan is null)
                return "There is no Practice Session to restart.";
            if (_assessment?.HasRecordedError != true ||
                _session?.Snapshot.State is not (PracticeSessionState.CountingIn or
                    PracticeSessionState.Running or
                    PracticeSessionState.WaitingForInput or
                    PracticeSessionState.LearnerPaused))
            {
                return "Restart becomes available after an error in the current Practice Session.";
            }

            return ReplaceSession(_chart, _plan, preserveLatestResult: true);
        }
    }

    public static void Deactivate()
    {
        lock (Sync)
            DeactivateCore(abandon: true);
    }

    private static string? StartCore(
        PracticeChart chart,
        PracticeSessionPlan plan,
        bool preserveLatestResult,
        int? countInBeatsOverride = null)
    {
        var preferences = _preferences ?? PracticePreferences.Default;
        var guidance = CreateGuidance(chart, plan, preferences, countInBeatsOverride);
        var started = PracticeSession.TryStart(chart, plan, guidance);
        if (started.Session is null)
            return StartErrorMessage(started.Error);

        _chart = chart;
        _plan = plan;
        _session = started.Session;
        _assessment = PracticeAssessment.Start(
            chart,
            plan,
            TimingCalibration.Uncalibrated,
            DateTimeOffset.UtcNow);
        _signalClock = Stopwatch.StartNew();
        _latestFeedback = Array.Empty<PracticeFeedback>();
        if (!preserveLatestResult)
        {
            _latestResult = null;
            _latestProgress = null;
            _progressWarning = null;
        }
        MovePlaybackTo(chartTime: plan.Range?.Start ?? ChartTime.Zero);
        Apply(_session.Handle(new PracticeSignal.Begin(SessionTime.Zero)));
        return null;
    }

    private static string? ReplaceSession(
        PracticeChart chart,
        PracticeSessionPlan plan,
        bool preserveLatestResult,
        int? countInBeatsOverride = null)
    {
        var preferences = _preferences ?? PracticePreferences.Default;
        var guidance = CreateGuidance(chart, plan, preferences, countInBeatsOverride);
        var validation = PracticeSession.TryStart(chart, plan, guidance);
        if (validation.Session is null)
            return StartErrorMessage(validation.Error);

        DeactivateCore(abandon: true);
        return StartCore(chart, plan, preserveLatestResult, countInBeatsOverride);
    }

    private static void DeactivateCore(bool abandon)
    {
        if (abandon && _session is not null)
            Apply(_session.Handle(new PracticeSignal.Abandon(CurrentSessionTime)));

        _session = null;
        _chart = null;
        _plan = null;
        _assessment = null;
        _signalClock = null;
        StopClick();
        PracticePlaybackFilter.Disable();
    }

    private static void Apply(PracticeTransition transition)
    {
        if (transition.Error is not null)
            return;

        var assessed = _assessment?.Apply(transition, DateTimeOffset.UtcNow);
        if (assessed is not null)
        {
            if (assessed.Feedback.Count > 0)
            {
                _latestFeedback = assessed.Feedback;
                _feedbackExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(1.5);
            }

            if (assessed.Result is { } result)
            {
                _latestResult = result;
                if (_learnerId is { } learnerId)
                {
                    var recorded = new PracticeProgressStore(ProgramData.DataPath).Record(learnerId, result);
                    _latestProgress = recorded.Progress;
                    _progressWarning = recorded.Warning;
                }
            }
        }

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
                case PracticeEffect.Click click:
                    PlayClick(click.Accent);
                    break;
            }
        }

        if (transition.Events.Any(item => item is PracticeEvent.SessionCompleted) &&
            ActiveLoopCore() is not null &&
            _chart is not null &&
            _plan is not null &&
            _preferences is not null)
        {
            var countInBeats = _preferences.CountInOnLoopRepeat
                ? _preferences.CountInBeats
                : 0;
            StartCore(
                _chart,
                _plan,
                preserveLatestResult: true,
                countInBeatsOverride: countInBeats);
        }
    }

    private static void StartAssistedAttemptAt(ChartTime position, bool disableLoop)
    {
        if (_chart is null || _preferences is null)
            return;

        var plan = CreatePlan(_preferences, range: null);
        if (disableLoop)
            _activeLoopId = null;

        var countInBeats = _preferences.CountInBeats;
        var warning = ReplaceSession(
            _chart,
            plan,
            preserveLatestResult: true,
            countInBeatsOverride: 0);
        if (warning is not null || _session is null)
        {
            _navigationWarning = warning;
            return;
        }

        var guidance = CreateGuidance(_chart, plan, _preferences, countInBeats);
        Apply(_session.Handle(new PracticeSignal.ChangeGuidance(CurrentSessionTime, guidance)));
        Apply(_session.Handle(new PracticeSignal.Seek(CurrentSessionTime, position)));
    }

    private static PracticeGuidance CreateGuidance(
        PracticeChart chart,
        PracticeSessionPlan plan,
        PracticePreferences preferences,
        int? countInBeatsOverride = null)
    {
        var rangeStart = plan.Range?.Start ?? ChartTime.Zero;
        var atOrBefore = chart.Beats.LastOrDefault(beat => beat.Position.CompareTo(rangeStart) <= 0);
        var after = chart.Beats.FirstOrDefault(beat =>
            atOrBefore is not null && beat.Position.CompareTo(atOrBefore.Position) > 0);
        var chartBeatMicroseconds = atOrBefore is not null && after is not null
            ? after.Position.Microseconds - atOrBefore.Position.Microseconds
            : 500_000;
        var sessionBeatMicroseconds = Math.Max(
            1,
            decimal.ToInt64(decimal.Round(
                chartBeatMicroseconds / plan.TempoRatio,
                MidpointRounding.AwayFromZero)));
        return new PracticeGuidance(
            countInBeatsOverride ?? preferences.CountInBeats,
            SessionTime.FromMicroseconds(sessionBeatMicroseconds),
            preferences.MetronomeEnabled);
    }

    private static PracticeSessionPlan CreatePlan(
        PracticePreferences preferences,
        PracticeRange? range)
    {
        return new PracticeSessionPlan(
            preferences.Mode,
            preferences.RequiredHands,
            preferences.Accompaniment,
            preferences.TempoRatio,
            range);
    }

    private static PracticeLoop? ActiveLoopCore()
    {
        return _activeLoopId is { } id
            ? _navigation.Loops.FirstOrDefault(loop => loop.Id == id)
            : null;
    }

    private static bool TryGetNavigationIdentity(
        out LearnerId learnerId,
        out ChartId chartId,
        out ChartTime duration)
    {
        if (ProgramData.ActiveLearner is not { } learner ||
            MidiFileData.Context is not { } context ||
            MidiFileData.MidiFile is null)
        {
            learnerId = default;
            chartId = null!;
            duration = default;
            return false;
        }

        learnerId = learner.Id;
        chartId = context.ChartId;
        duration = _chart?.Duration ?? ChartTime.FromMicroseconds(
            MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalMicroseconds);
        return true;
    }

    private static string? SetNavigationWarning(string? warning)
    {
        _navigationWarning = warning;
        return warning;
    }

    private static void PlayClick(bool accent)
    {
        StopClick();
        var note = accent ? AccentClickNote : ClickNote;
        var velocity = accent ? 112 : 88;
        MidiPlayer.SoundFontEngine?.PlayNote(ClickChannel, note, velocity);

        var noteOn = new NoteOnEvent(
            (SevenBitNumber)(byte)note,
            (SevenBitNumber)(byte)velocity)
        {
            Channel = (FourBitNumber)ClickChannel
        };
        if (CoreSettings.SoundEngine == Enums.SoundEngine.Plugins)
            VstPlayer.PluginsChain?.PluginInstrument?.ReceiveMidiEvent(noteOn);
        DevicesManager.ODevice?.SendEvent(noteOn);
    }

    private static void StopClick()
    {
        foreach (var note in new[] { ClickNote, AccentClickNote })
        {
            MidiPlayer.SoundFontEngine?.StopNote(ClickChannel, note);
            var noteOff = new NoteOffEvent(
                (SevenBitNumber)(byte)note,
                (SevenBitNumber)(byte)0)
            {
                Channel = (FourBitNumber)ClickChannel
            };
            if (CoreSettings.SoundEngine == Enums.SoundEngine.Plugins)
                VstPlayer.PluginsChain?.PluginInstrument?.ReceiveMidiEvent(noteOff);
            DevicesManager.ODevice?.SendEvent(noteOff);
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
            PracticeStartError.InvalidGuidance => "The selected count-in is invalid.",
            _ => "The Practice Session could not be started."
        };
    }

    private static SessionTime CurrentSessionTime =>
        SessionTime.FromMicroseconds(
            (long)((_signalClock?.Elapsed.TotalSeconds ?? 0) * 1_000_000));
}
