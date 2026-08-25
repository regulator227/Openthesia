using IconFonts;
using ImGuiNET;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Core;
using Openthesia.Core.Accessibility;
using Openthesia.Core.Midi;
using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using Openthesia.Ui.Accessibility;
using System.Globalization;
using System.Numerics;

namespace Openthesia.Ui.Windows;

public class ModeSelectionWindow : ImGuiWindow
{
    private static PracticePreferences _practicePreferences = PracticePreferences.Default;
    private static PracticeProgress _practiceProgress = PracticeProgress.Empty;
    private static PracticeNavigation _practiceNavigation = PracticeNavigation.Empty;
    private static Guid? _selectedLoopId;
    private static (LearnerId LearnerId, ChartId ChartId)? _loadedPreferencesFor;
    private static string? _practiceWarning;

    public ModeSelectionWindow()
    {
        _id = Enums.Windows.ModeSelection.ToString();
        _active = false;
    }

    public static void RenderContainer()
    {
        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, Math.Min(ImGuiUtils.FixedSize(new Vector2(24)).X, display.X * 0.04f));
        var top = Math.Min(ImGuiUtils.FixedSize(new Vector2(115)).Y, display.Y * 0.22f);
        var size = new Vector2(
            Math.Max(200f, display.X - margin * 2),
            Math.Max(200f, display.Y - top - margin));

        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            AccessibilityRuntime.Presentation.UseSystemContrast
                ? AccessibilityRuntime.ContrastPalette.Window
                : new Vector4(ThemeManager.MainBgCol.X, ThemeManager.MainBgCol.Y, ThemeManager.MainBgCol.Z, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, Math.Max(2f, 2f * FontController.DSF));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f * FontController.DSF);
        ImGui.SetCursorScreenPos(new Vector2(margin, top));
        var visible = ImGui.BeginChild(
            "Container",
            size,
            ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Border,
            ImGuiWindowFlags.AlwaysVerticalScrollbar);
        ImGui.PopStyleVar(2);
        if (visible)
        {
            if (CoreSettings.AnimatedBackground && AccessibilityRuntime.Presentation.AllowDecorativeMotion)
                Drawings.RenderMatrixBackground();

            RenderTitle(MidiFileData.FileName.Replace(".mid", string.Empty));
            ImGui.TextWrapped("Choose how to use this Chart. Practice setup and progress remain available at every supported text and display scale.");
            ImGui.SeparatorText("Practice setup");
            EnsurePracticePreferencesLoaded();
            RenderPracticeConfiguration();
            RenderRecentProgress();

            if (_practiceWarning is not null)
            {
                ImGui.TextWrapped(_practiceWarning);
                ImGuiAccessibility.Text(
                    "practice-setup.warning",
                    "Practice setup status",
                    _practiceWarning,
                    liveSetting: AccessibilityLiveSetting.Polite);
            }

            ImGui.SeparatorText("Start");
            RenderButton(
                "practice-setup.performance-visualization",
                "Performance Visualization",
                "Listen with the selected visualization.",
                "#31CB15",
                SetupVisualization);
            RenderButton(
                "practice-setup.start-practice",
                $"Practice: {PracticeModeLabel(_practicePreferences.Mode)}",
                "Practice with accuracy and timing feedback.",
                "#0EA5E9",
                SetupPractice);
            RenderButton(
                "practice-setup.assign-hands",
                "Assign Hands",
                "Author left- and right-hand assignments.",
                "#772525",
                SetupHandAssignment);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static void RenderBackButton()
    {
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.SetCursorScreenPos(ImGuiUtils.FixedSize(new Vector2(22, 50)));
        void GoBack() => WindowsManager.SetWindow(Enums.Windows.MidiBrowser);
        var backInvoked = ImGui.Button(
                              $"{FontAwesome6.ArrowLeftLong} Back",
                              ImGuiUtils.FixedSize(new Vector2(120, 50))) ||
                          EscapeReturns();
        if (backInvoked)
            GoBack();
        ImGuiAccessibility.Button(
            "practice-setup.back",
            "Back",
            GoBack,
            "Return to Song and Chart selection.",
            invoked: backInvoked);
        ImGui.PopFont();
    }

    private static bool EscapeReturns()
    {
        return !ImGui.GetIO().WantTextInput &&
               !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId) &&
               ImGui.IsKeyPressed(ImGuiKey.Escape, false);
    }

    private static void RenderTitle(string text)
    {
        ImGui.PushFont(FontController.GetFontOfSize(22));
        ImGui.TextWrapped(text);
        ImGuiAccessibility.Text(
            "practice-setup.chart",
            "Selected Chart",
            text);
        ImGui.PopFont();
    }

    private static void RenderButton(
        string id,
        string label,
        string description,
        string color,
        Action onClick)
    {
        ImGui.TextWrapped(description);
        ImGuiTheme.PushButton(
            ImGuiTheme.HtmlToVec4(color),
            ImGuiTheme.HtmlToVec4(color, 0.7f),
            ImGuiTheme.HtmlToVec4(color)
        );

        ImGui.PushFont(FontController.GetFontOfSize(22));
        var buttonWidth = Math.Max(100f, ImGui.GetContentRegionAvail().X);
        var invoked = ImGui.Button(
            label,
            new Vector2(buttonWidth, ImGuiUtils.FixedSize(new Vector2(54)).Y));
        if (invoked)
        {
            onClick.Invoke();
        }
        ImGuiAccessibility.Button(id, label, onClick, description, invoked: invoked);
        ImGui.PopFont();
        ImGuiTheme.PopButton();
    }

    private static void RenderPracticeConfiguration()
    {
        var controlWidth = Math.Min(
            ImGuiUtils.FixedSize(new Vector2(420)).X,
            Math.Max(100f, ImGui.GetContentRegionAvail().X));
        ImGui.SetNextItemWidth(controlWidth);
        ImGuiAccessibility.ComboBox(
            "practice-setup.mode",
            "Practice Mode",
            _practicePreferences.Mode,
            Enum.GetValues<PracticeMode>().Select(mode => (
                $"practice-setup.mode.{mode.ToString().ToLowerInvariant()}",
                PracticeModeLabel(mode),
                mode)),
            mode => _practicePreferences = _practicePreferences with { Mode = mode },
            "Choose how Chart time, guidance, and feedback behave.");

        ImGui.SetNextItemWidth(controlWidth);
        ImGuiAccessibility.ComboBox(
            "practice-setup.required-hands",
            "Required Hands",
            _practicePreferences.RequiredHands,
            Enum.GetValues<RequiredHands>().Select(hands => (
                $"practice-setup.required-hands.{hands.ToString().ToLowerInvariant()}",
                hands.ToString(),
                hands)),
            hands => _practicePreferences = _practicePreferences.WithRequiredHands(hands));

        ImGui.SetNextItemWidth(controlWidth);
        ImGui.BeginDisabled(_practicePreferences.RequiredHands == RequiredHands.Both);
        var accompanimentEnabled = _practicePreferences.RequiredHands != RequiredHands.Both;
        ImGuiAccessibility.ComboBox(
            "practice-setup.accompaniment",
            "Accompaniment",
            _practicePreferences.Accompaniment,
            Enum.GetValues<Accompaniment>().Select(accompaniment => (
                $"practice-setup.accompaniment.{accompaniment.ToString().ToLowerInvariant()}",
                accompaniment.ToString(),
                accompaniment)),
            accompaniment => _practicePreferences = _practicePreferences with { Accompaniment = accompaniment },
            "Choose whether Chart notes outside the Required Hands play automatically.",
            accompanimentEnabled);
        ImGui.EndDisabled();

        ImGui.SetNextItemWidth(controlWidth);
        var tempoOptions = new[] { 0.25m, 0.5m, 0.75m, 1m, 1.25m, 1.5m, 2m };
        ImGuiAccessibility.ComboBox(
            "practice-setup.tempo",
            "Tempo",
            _practicePreferences.TempoRatio,
            tempoOptions.Select(tempoRatio => (
                $"practice-setup.tempo.{tempoRatio.ToString(CultureInfo.InvariantCulture).Replace('.', '-')}",
                $"{tempoRatio:0.##}x",
                tempoRatio)),
            tempoRatio => _practicePreferences = _practicePreferences with { TempoRatio = tempoRatio });

        ImGui.SetNextItemWidth(controlWidth);
        ImGuiAccessibility.ComboBox(
            "practice-setup.count-in",
            "Count-in",
            _practicePreferences.CountInBeats,
            PracticePreferences.SupportedCountInBeats.Select(beats => (
                $"practice-setup.count-in.{beats}",
                beats == 0 ? "No count-in" : $"{beats} beats",
                beats)),
            beats => _practicePreferences = _practicePreferences with { CountInBeats = beats });

        var metronomeEnabled = _practicePreferences.MetronomeEnabled;
        if (ImGui.Checkbox("Metronome", ref metronomeEnabled))
            _practicePreferences = _practicePreferences with { MetronomeEnabled = metronomeEnabled };
        ImGuiAccessibility.Toggle(
            "practice-setup.metronome",
            "Metronome",
            _practicePreferences.MetronomeEnabled,
            () => _practicePreferences = _practicePreferences with
            {
                MetronomeEnabled = !_practicePreferences.MetronomeEnabled
            });

        var countInOnLoopRepeat = _practicePreferences.CountInOnLoopRepeat;
        if (ImGui.Checkbox("Count in on every loop pass", ref countInOnLoopRepeat))
        {
            _practicePreferences = _practicePreferences with
            {
                CountInOnLoopRepeat = countInOnLoopRepeat
            };
        }
        ImGuiAccessibility.Toggle(
            "practice-setup.count-in-on-loop-repeat",
            "Count in on every loop pass",
            _practicePreferences.CountInOnLoopRepeat,
            () => _practicePreferences = _practicePreferences with
            {
                CountInOnLoopRepeat = !_practicePreferences.CountInOnLoopRepeat
            });

        var selectedLoop = _selectedLoopId is { } selectedId
            ? _practiceNavigation.Loops.FirstOrDefault(loop => loop.Id == selectedId)
            : null;
        ImGui.SetNextItemWidth(controlWidth);
        var selectedRange = selectedLoop?.Id.ToString("D") ?? "full";
        var rangeOptions = new[]
            {
                ("practice-setup.range.full", "Full Chart", "full")
            }
            .Concat(_practiceNavigation.Loops.OrderBy(loop => loop.Range.Start).Select(loop => (
                $"practice-setup.range.{loop.Id:D}",
                loop.Name,
                loop.Id.ToString("D"))));
        ImGuiAccessibility.ComboBox(
            "practice-setup.range",
            "Practice range",
            selectedRange,
            rangeOptions,
            value => _selectedLoopId = value == "full" ? null : Guid.Parse(value),
            "Choose the full Chart or a saved loop.");
    }

    private static void RenderRecentProgress()
    {
        if (MidiFileData.Context is not { } context || MidiFileData.MidiFile is null)
            return;

        var selectedLoop = _selectedLoopId is { } selectedId
            ? _practiceNavigation.Loops.FirstOrDefault(loop => loop.Id == selectedId)
            : null;
        var currentSetup = new ComparablePracticeSetup(
            context.ChartId,
            _practicePreferences.Mode,
            _practicePreferences.RequiredHands,
            _practicePreferences.Accompaniment,
            _practicePreferences.TempoRatio,
            selectedLoop?.Range ?? new PracticeRange(
                    ChartTime.Zero,
                    ChartTime.FromMicroseconds(
                        MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalMicroseconds)),
            PracticeAssessment.CurrentScoringPolicyVersion);
        var latest = _practiceProgress.Results.LastOrDefault(result => result.Setup == currentSetup);
        if (latest is null)
        {
            RenderNonComparableHistory(_practiceProgress.Results.LastOrDefault());
            return;
        }

        var progress = _practiceProgress.For(
            currentSetup,
            latest.Timing?.CalibrationRevision ?? 0);
        var timing = latest.Timing is null
            ? "Timing N/A"
            : $"Timing {latest.Timing.AverageAbsoluteErrorMicroseconds / 1_000m:0} ms avg";
        var latestLine = $"Last comparable result · {latest.Outcome} · " +
                         $"Completion {latest.Completion.Ratio:P1} · " +
                         $"{latest.Accuracy.RequiredNotesHitRatio:P1} notes hit · " +
                         $"{latest.Accuracy.ExtraNotes} Extra · {timing}" +
                         (latest.Assisted ? " · Assisted" : string.Empty);
        var progressParts = new List<string>();
        if (progress.BestAccuracy is { } accuracyBest)
        {
            progressParts.Add(
                $"Accuracy PB · {accuracyBest.Result.Accuracy.RequiredNotesHitRatio:P1} notes hit · " +
                $"{accuracyBest.Result.Accuracy.ExtraNotes} Extra{PersonalBestStatus(latest, accuracyBest)}");
        }
        if (progress.BestTiming is { } timingBest)
        {
            progressParts.Add(
                $"Timing PB · {timingBest.Result.Timing!.AverageAbsoluteErrorMicroseconds / 1_000m:0} ms avg" +
                PersonalBestStatus(latest, timingBest));
        }
        if (progress.FirstCompletion is { } firstCompletion)
            progressParts.Add($"First completion · {firstCompletion.Result.EndedAtUtc.ToLocalTime():d}");
        progressParts.Add(
            $"Trend A/E/T · {progress.RecentTrend.Accuracy}/{progress.RecentTrend.Extras}/{progress.RecentTrend.Timing}");
        ImGui.SeparatorText("Recent progress");
        var summary = $"{latestLine}\n{string.Join(" · ", progressParts)}";
        ImGui.TextWrapped(summary);
        ImGuiAccessibility.Text(
            "practice-setup.recent-progress",
            "Recent Practice progress",
            summary);
    }

    private static void RenderNonComparableHistory(PracticeResult? latest)
    {
        if (latest is null)
            return;

        var assisted = latest.Assisted ? " · Assisted" : string.Empty;
        var summary = $"Recent history (not comparable) · {PracticeModeLabel(latest.Setup.Mode)} · " +
                      $"{latest.Outcome} · Completion {latest.Completion.Ratio:P1} · " +
                      $"Accuracy {latest.Accuracy.RequiredNotesHitRatio:P1} · " +
                      $"{latest.Accuracy.ExtraNotes} Extra{assisted}";
        ImGui.SeparatorText("Recent progress");
        ImGui.TextWrapped(summary);
        ImGuiAccessibility.Text(
            "practice-setup.recent-progress",
            "Recent Practice progress",
            summary);
    }

    private static string PersonalBestStatus(PracticeResult latest, PracticePersonalBest best)
    {
        if (best.LatestMatchedAtUtc != latest.EndedAtUtc)
            return string.Empty;
        return best.MatchCount == 1 ? " · achieved" : $" · matched ×{best.MatchCount}";
    }

    private static void EnsurePracticePreferencesLoaded()
    {
        if (ProgramData.ActiveLearner is not { } learner || MidiFileData.Context is not { } context)
            return;

        var key = (learner.Id, context.ChartId);
        if (_loadedPreferencesFor == key)
        {
            _practiceNavigation = MidiPracticeSession.Navigation;
            if (_selectedLoopId is { } selectedLoopId &&
                _practiceNavigation.Loops.All(loop => loop.Id != selectedLoopId))
            {
                _selectedLoopId = null;
            }
            if (MidiPracticeSession.LatestProgress is { } latestProgress)
                _practiceProgress = latestProgress;
            _practiceWarning = MidiPracticeSession.ProgressWarning ?? _practiceWarning;
            return;
        }

        var loaded = new PracticePreferencesStore(ProgramData.DataPath).Load(learner.Id, context.ChartId);
        var progress = new PracticeProgressStore(ProgramData.DataPath).Load(learner.Id, context.ChartId);
        var navigation = MidiPracticeSession.LoadNavigation();
        _practicePreferences = loaded.Preferences;
        _practiceProgress = progress.Progress;
        _practiceNavigation = navigation.Navigation;
        _selectedLoopId = null;
        _practiceWarning = loaded.Warning ?? progress.Warning ?? navigation.Warning;
        _loadedPreferencesFor = key;
    }

    private static void SetupVisualization()
    {
        MidiPracticeSession.Deactivate();
        ScreenCanvasControls.SetEditMode(false);
        ScreenCanvasControls.LeftHandActive = true;
        ScreenCanvasControls.RightHandActive = true;
        PrepareHandAssignments();
        WindowsManager.SetWindow(Enums.Windows.MidiPlayback);
    }

    private static void SetupPractice()
    {
        if (ProgramData.ActiveLearner is not { } learner || MidiFileData.Context is not { } context)
        {
            _practiceWarning = "Learner and Chart identity are required to start Practice.";
            return;
        }

        MidiPracticeSession.Deactivate();
        ScreenCanvasControls.SetEditMode(false);
        PrepareHandAssignments();
        var saved = new PracticePreferencesStore(ProgramData.DataPath).Save(
            learner.Id,
            context.ChartId,
            _practicePreferences);
        if (!saved.Saved)
        {
            _practiceWarning = saved.Warning;
            return;
        }

        _practiceWarning = MidiPracticeSession.Start(_practicePreferences, _selectedLoopId);
        if (_practiceWarning is null)
            WindowsManager.SetWindow(Enums.Windows.MidiPlayback);
    }

    private static string PracticeModeLabel(PracticeMode mode)
    {
        return mode switch
        {
            PracticeMode.WaitForNotes => "Wait for Notes",
            PracticeMode.PlayInTime => "Play in Time",
            PracticeMode.Recital => "Recital",
            _ => mode.ToString()
        };
    }

    private static void SetupHandAssignment()
    {
        MidiPracticeSession.Deactivate();
        ScreenCanvasControls.SetEditMode(true);
        PrepareHandAssignments();
        WindowsManager.SetWindow(Enums.Windows.MidiPlayback);
    }

    private static void PrepareHandAssignments()
    {
        LeftRightData.S_IsRightNote.Clear();
        foreach (var note in MidiFileData.Notes)
        {
            LeftRightData.S_IsRightNote.Add(true);
        }
        MidiEditing.ReadData();

        // Map each note (possibly multiple at the same time/number) to its indices in the MIDI file
        LeftRightData.S_NoteIndexMap = new Dictionary<string, List<int>>();

        foreach (var (note, i) in MidiFileData.Notes.Select((note, i) => (note, i)))
        {
            // Build a stable composite key
            var key = $"{note.NoteNumber}_{note.Time}";

            // Create or append to the list
            if (!LeftRightData.S_NoteIndexMap.TryGetValue(key, out var indexList))
            {
                indexList = new List<int>();
                LeftRightData.S_NoteIndexMap[key] = indexList;
            }

            indexList.Add(i);
        }
    }


    protected override void OnImGui()
    {
        RenderBackButton();
        RenderContainer();
    }
}
