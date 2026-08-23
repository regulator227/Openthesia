using IconFonts;
using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Midi;
using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using System.Numerics;

namespace Openthesia.Ui.Windows;

public class ModeSelectionWindow : ImGuiWindow
{
    private static PracticePreferences _practicePreferences = PracticePreferences.Default;
    private static (LearnerId LearnerId, ChartId ChartId)? _loadedPreferencesFor;
    private static string? _practiceWarning;

    public ModeSelectionWindow()
    {
        _id = Enums.Windows.ModeSelection.ToString();
        _active = false;
    }

    public static void RenderContainer()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ThemeManager.MainBgCol * 0.8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 2f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
        ImGui.SetNextWindowPos(new((ImGui.GetIO().DisplaySize.X - ImGui.GetIO().DisplaySize.X / 1.2f) / 2, ImGuiUtils.FixedSize(new Vector2(120)).Y));
        if (ImGui.BeginChild("Container", new Vector2(ImGui.GetIO().DisplaySize.X / 1.2f, ImGui.GetIO().DisplaySize.Y / 1.2f),
            ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Border))
        {
            ImGui.PopStyleVar(2);

            if (CoreSettings.AnimatedBackground)
                Drawings.RenderMatrixBackground();

            RenderTitle(MidiFileData.FileName.Replace(".mid", string.Empty), 50 * FontController.DSF);
            EnsurePracticePreferencesLoaded();

            RenderIconWithText(FontAwesome6.Music, "Listen with the selected visualization", 0.1f, 2.5f);
            RenderIconWithText(FontAwesome6.Gamepad, "Playback waits for each required target", 0.36f, 2.5f);
            RenderIconWithText(FontAwesome6.Hands, "Author left- and right-hand assignments", 0.625f, 2.5f);

            RenderPracticeConfiguration();

            RenderButton("Performance Visualization", "#31CB15", 0.1f, 1.4f, SetupVisualization);
            RenderButton("Wait for Notes", "#0EA5E9", 0.36f, 1.4f, SetupPractice);
            RenderButton("Assign Hands", "#772525", 0.625f, 1.4f, SetupHandAssignment);

            if (_practiceWarning is not null)
            {
                ImGui.SetCursorPos(new Vector2(
                    ImGui.GetIO().DisplaySize.X * 0.36f,
                    ImGui.GetIO().DisplaySize.Y * 0.82f));
                ImGui.TextWrapped(_practiceWarning);
            }

            ImGui.EndChild();
        }
        ImGui.PopStyleColor();
    }

    private static void RenderBackButton()
    {
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.SetCursorScreenPos(ImGuiUtils.FixedSize(new Vector2(22, 50)));
        if (ImGui.Button(FontAwesome6.ArrowLeftLong, ImGuiUtils.FixedSize(new Vector2(100, 50))))
        {
            WindowsManager.SetWindow(Enums.Windows.MidiBrowser);
        }
        ImGui.PopFont();
    }

    private static void RenderTitle(string text, float offsetY)
    {
        ImGui.PushFont(FontController.Title);
        ImGui.SetCursorPos(new Vector2((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) / 2, offsetY));
        ImGui.Text(text);
        ImGui.PopFont();
    }

    private static void RenderIconWithText(string icon, string text, float xFactor, float yFactor)
    {
        var io = ImGui.GetIO();
        ImGui.PushFont(FontController.BigIcon);
        float xPos = io.DisplaySize.X * xFactor + ImGuiUtils.FixedSize(new Vector2(125)).X - ImGui.CalcTextSize(icon).X / 2;
        float yPos = io.DisplaySize.Y / yFactor;
   
        ImGui.SetCursorPos(new Vector2(xPos, yPos));
        ImGui.Text(icon);
        ImGui.PopFont();

        ImGui.PushFont(FontController.GetFontOfSize(22));
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(new Vector2(io.DisplaySize.X * xFactor + ImGuiUtils.FixedSize(new Vector2(125)).X, yPos),
            ImGui.GetColorU32(Vector4.One), text);
        ImGui.PopFont();
    }

    private static void RenderButton(string label, string color, float xFactor, float yFactor, Action onClick)
    {
        var io = ImGui.GetIO();
        ImGuiTheme.PushButton(
            ImGuiTheme.HtmlToVec4(color),
            ImGuiTheme.HtmlToVec4(color, 0.7f),
            ImGuiTheme.HtmlToVec4(color)
        );

        ImGui.PushFont(FontController.GetFontOfSize(22));
        ImGui.SetCursorPos(new Vector2(io.DisplaySize.X * xFactor, io.DisplaySize.Y / yFactor));
        if (ImGui.Button(label, ImGuiUtils.FixedSize(new Vector2(250, 100))))
        {
            onClick.Invoke();
        }
        ImGui.PopFont();
        ImGuiTheme.PopButton();
    }

    private static void RenderPracticeConfiguration()
    {
        var x = ImGui.GetIO().DisplaySize.X * 0.36f;
        var y = ImGui.GetIO().DisplaySize.Y * 0.50f;
        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(250)).X);
        ImGui.SetCursorPos(new Vector2(x, y));
        if (ImGui.BeginCombo("##RequiredHands", $"Hands: {_practicePreferences.RequiredHands}"))
        {
            foreach (var hands in Enum.GetValues<RequiredHands>())
            {
                if (ImGui.Selectable(hands.ToString(), hands == _practicePreferences.RequiredHands))
                    _practicePreferences = _practicePreferences.WithRequiredHands(hands);
            }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(250)).X);
        ImGui.SetCursorPos(new Vector2(x, y + ImGuiUtils.FixedSize(new Vector2(38)).Y));
        ImGui.BeginDisabled(_practicePreferences.RequiredHands == RequiredHands.Both);
        if (ImGui.BeginCombo("##Accompaniment", $"Accompaniment: {_practicePreferences.Accompaniment}"))
        {
            foreach (var accompaniment in Enum.GetValues<Accompaniment>())
            {
                if (ImGui.Selectable(
                    accompaniment.ToString(),
                    accompaniment == _practicePreferences.Accompaniment))
                {
                    _practicePreferences = _practicePreferences with { Accompaniment = accompaniment };
                }
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();

        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(250)).X);
        ImGui.SetCursorPos(new Vector2(x, y + ImGuiUtils.FixedSize(new Vector2(76)).Y));
        if (ImGui.BeginCombo("##PracticeTempo", $"Tempo: {_practicePreferences.TempoRatio:0.##}x"))
        {
            foreach (var tempoRatio in new[] { 0.25m, 0.5m, 0.75m, 1m, 1.25m, 1.5m, 2m })
            {
                if (ImGui.Selectable(
                    $"{tempoRatio:0.##}x",
                    tempoRatio == _practicePreferences.TempoRatio))
                {
                    _practicePreferences = _practicePreferences with { TempoRatio = tempoRatio };
                }
            }
            ImGui.EndCombo();
        }
    }

    private static void EnsurePracticePreferencesLoaded()
    {
        if (ProgramData.ActiveLearner is not { } learner || MidiFileData.Context is not { } context)
            return;

        var key = (learner.Id, context.ChartId);
        if (_loadedPreferencesFor == key)
            return;

        var loaded = new PracticePreferencesStore(ProgramData.DataPath).Load(learner.Id, context.ChartId);
        _practicePreferences = loaded.Preferences;
        _practiceWarning = loaded.Warning;
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

        _practiceWarning = MidiPracticeSession.Start(_practicePreferences);
        if (_practiceWarning is null)
            WindowsManager.SetWindow(Enums.Windows.MidiPlayback);
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
