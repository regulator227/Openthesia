using IconFonts;
using ImGuiNET;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Core;
using Openthesia.Core.Midi;
using Openthesia.Core.SoundFonts;
using Openthesia.Enums;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using System.Numerics;
using Veldrid;
using ScreenRecorderLib;
using Note = Melanchall.DryWetMidi.Interaction.Note;
using static Openthesia.Core.ScreenCanvasControls;
using Openthesia.Core.Plugins;
using Openthesia.Core.Practice;
using Openthesia.Core.FileDialogs;
using Vanara.PInvoke;

namespace Openthesia.Ui;

public class ScreenCanvas
{
    public static Vector2 CanvasPos { get; private set; }

    // controls state to handle top bar hiding
    private static bool _leftHandColorPicker;
    private static bool _rightHandColorPicker;
    private static bool _comboFallSpeed;
    private static bool _comboPlaybackSpeed;
    private static bool _comboSoundFont;
    private static bool _comboPlugins;

    private static Vector2 _rectStart;
    private static Vector2 _rectEnd;
    private static bool _isRectMode;
    private static bool _isRightRect;
    private static bool _isProgressBarHovered;
    private static float _panVelocity;
    private static bool _isProgressBarActive;
    private static bool _showPracticeTools;
    private static string _practiceLoopName = "Loop";
    private static string _practiceBookmarkName = "Bookmark";
    private static Guid? _editingLoopId;
    private static Guid? _editingBookmarkId;
    private static ChartTime? _loopStart;
    private static ChartTime? _loopEnd;
    private static string? _practiceToolsWarning;
    private static readonly (ImGuiKey ImGuiKey, PracticeKey PracticeKey)[] PracticeCommandKeys =
    {
        (ImGuiKey.Space, PracticeKey.Space),
        (ImGuiKey.R, PracticeKey.R),
        (ImGuiKey.T, PracticeKey.T),
        (ImGuiKey.G, PracticeKey.G),
        (ImGuiKey.LeftArrow, PracticeKey.LeftArrow),
        (ImGuiKey.RightArrow, PracticeKey.RightArrow),
        (ImGuiKey.Escape, PracticeKey.Escape),
        (ImGuiKey.Backspace, PracticeKey.Backspace)
    };

    private static void RenderGrid()
    {
        var drawList = ImGui.GetWindowDrawList();
        for (int key = 0; key < 52; key++)
        {
            if (key % 7 == 2)
            {
                drawList.AddLine(CanvasPos + new Vector2(key * PianoRenderer.Width, 0),
                    new(PianoRenderer.P.X + key * PianoRenderer.Width, PianoRenderer.P.Y), ImGui.GetColorU32(new Vector4(Vector3.One, 0.08f)), 2);
            }
            else if (key % 7 == 5)
            {
                drawList.AddLine(CanvasPos + new Vector2(key * PianoRenderer.Width, 0),
                    new(PianoRenderer.P.X + key * PianoRenderer.Width, PianoRenderer.P.Y), ImGui.GetColorU32(new Vector4(Vector3.One, 0.06f)));
            }
        }
    }

    private static bool IsRectInside(Vector2 aMin, Vector2 aMax, Vector2 bMin, Vector2 bMax)
    {
        return aMin.X >= bMin.X && aMax.X <= bMax.X && aMin.Y >= bMin.Y && aMax.Y <= bMax.Y;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
    
    private static bool IsNoteEnabled(int index)
    {
        return LeftRightData.S_IsRightNote[index] && RightHandActive ||
               !LeftRightData.S_IsRightNote[index] && LeftHandActive;
    }

    private static Vector4 GetNoteColor(int index)
    {
        if (AccessibilityRuntime.Presentation.UseSystemContrast)
        {
            if (!IsNoteEnabled(index))
                return AccessibilityRuntime.ContrastPalette.Window;
            return LeftRightData.S_IsRightNote[index]
                ? AccessibilityRuntime.ContrastPalette.Highlight
                : AccessibilityRuntime.ContrastPalette.WindowText;
        }

        if (LeftRightData.S_IsRightNote[index])
        {
            return RightHandActive ? ThemeManager.RightHandCol : ThemeManager.MainBgCol;
        }
        return LeftHandActive ? ThemeManager.LeftHandCol : ThemeManager.MainBgCol;
    }

    private static uint GetSharpColor(int index)
    {
        var color = AccessibilityRuntime.Presentation.UseSystemContrast
            ? GetNoteColor(index)
            : IsNoteEnabled(index)
                ? ImGuiUtils.DarkenColor(GetNoteColor(index), 0.4f)
                : ThemeManager.MainBgCol;
        return ImGui.GetColorU32(color);
    }

    private static void DrawInputNotes()
    {
        var speed = 100f * ImGui.GetIO().DeltaTime * FallSpeedVal;
        var drawList = ImGui.GetWindowDrawList();

        int index = 0;
        List<IOHandle.NoteRect> toRemove = new();
        foreach (var note in IOHandle.NoteRects.ToArray())
        {
            float py1;
            float py2;

            //int idx = IOHandle.NoteRects.IndexOf(note);

            var n = IOHandle.NoteRects[index];
            n.Time += speed;
            IOHandle.NoteRects[index] = n;

            var length = note.WasReleased ? note.FinalTime : note.Time;

            py1 = note.PY1 - note.Time;
            py2 = note.PY2 + length - note.Time;

            if (py2 < 0)
            {
                toRemove.Add(note);
                //IOHandle.NoteRects.Remove(note);
                index++;
                continue;
            }

            if (note.IsBlack)
            {
                if (CoreSettings.NeonFx && AccessibilityRuntime.Presentation.AllowGlow)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float thickness = i * 2;
                        float alpha = 0.2f + (3 - i) * 0.2f;
                        uint color = ImGui.GetColorU32(new Vector4(ThemeManager.RightHandCol.X, ThemeManager.RightHandCol.Y, ThemeManager.RightHandCol.Z, alpha) * 0.5f * 0.7f);
                        drawList.AddRect(
                            new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4 - 1, py1 - 1),
                            new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4 + 1, py2 + 1),
                            color,
                            CoreSettings.NoteRoundness,
                            0,
                            thickness
                        );
                    }
                }
                else
                {
                    uint color = ImGui.GetColorU32(new Vector4(Vector3.Zero, 1f) * 0.5f);
                    drawList.AddRect(
                        new Vector2(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4 - 1, py1 - 1),
                        new Vector2(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4 + 1, py2 + 1),
                        color,
                        CoreSettings.NoteRoundness,
                        0,
                        1f
                    );
                }

                drawList.AddRectFilled(new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4, py1),
                  new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4, py2),
                  ImGui.GetColorU32(AccessibilityRuntime.Presentation.AllowTransparency
                      ? ThemeManager.RightHandCol * 0.7f
                      : new Vector4(ThemeManager.RightHandCol.X, ThemeManager.RightHandCol.Y, ThemeManager.RightHandCol.Z, 1f)),
                  CoreSettings.NoteRoundness,
                  ImDrawFlags.RoundCornersAll);
            }
            else
            {
                if (CoreSettings.NeonFx && AccessibilityRuntime.Presentation.AllowGlow)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float thickness = i * 2;
                        float alpha = 0.2f + (3 - i) * 0.2f;
                        uint color = ImGui.GetColorU32(new Vector4(ThemeManager.RightHandCol.X, ThemeManager.RightHandCol.Y, ThemeManager.RightHandCol.Z, alpha) * 0.5f);
                        drawList.AddRect(
                            new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width - 1, py1 - 1),
                            new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width + 1, py2 + 1),
                            color,
                            CoreSettings.NoteRoundness,
                            0,
                            thickness
                        );
                    }
                }
                else
                {
                    uint color = ImGui.GetColorU32(new Vector4(Vector3.Zero, 1f) * 0.5f);
                    drawList.AddRect(
                        new Vector2(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width - 1, py1 - 1),
                        new Vector2(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width + 1, py2 + 1),
                        color,
                        CoreSettings.NoteRoundness,
                        0,
                        1f
                    );
                }

                drawList.AddRectFilled(new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width, py1),
                    new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault((SevenBitNumber)note.KeyNum, 0) * PianoRenderer.Width + PianoRenderer.Width, py2),
                    ImGui.GetColorU32(ThemeManager.RightHandCol), CoreSettings.NoteRoundness, ImDrawFlags.RoundCornersAll);
            }
            index++;
        }

        if (toRemove.Count > 0)
        {
            IOHandle.NoteRects.RemoveRange(0, toRemove.Count - 1);
            IOHandle.NoteRects.RemoveAt(0);
        }
    }

    private static void DrawPlaybackNotes()
    {
        var drawList = ImGui.GetWindowDrawList();

        if (IsPracticeMode)
            MidiPracticeSession.Advance();

        if (MidiPlayer.IsTimerRunning)
        {
            MidiPlayer.Timer += ImGui.GetIO().DeltaTime * 100f * (float)MidiPlayer.Playback.Speed * FallSpeedVal;
        }

        int index = 0;
        var notes = MidiFileData.Notes;
        foreach (Note note in notes)
        {
            var time = (float)note.TimeAs<MetricTimeSpan>(MidiFileData.TempoMap).TotalSeconds * FallSpeedVal;
            var length = (float)note.LengthAs<MetricTimeSpan>(MidiFileData.TempoMap).TotalSeconds * FallSpeedVal;
            var col = GetNoteColor(index);
            
            // color opacity based on note velocity
            if (CoreSettings.UseVelocityAsNoteOpacity && AccessibilityRuntime.Presentation.AllowTransparency)
            {
                col.W = note.Velocity * 1.27f / 161.29f;
                col.W = Math.Clamp(col.W, 0.3f, 1f); // we clamp it so they don't disappear with lower velocities
            }

            float py1;
            float py2;
            if (UpDirection && !IsEditMode)
            {
                py1 = PianoRenderer.P.Y + time * 100 - MidiPlayer.Timer;
                py2 = PianoRenderer.P.Y + time * 100 + length * 100 - MidiPlayer.Timer;

                // skip notes outside of screen to save performance
                if (py1 > PianoRenderer.P.Y || py2 < 0)
                {
                    index++;
                    continue;
                }
            }
            else
            {
                py1 = PianoRenderer.P.Y - time * 100 + MidiPlayer.Timer;
                py2 = PianoRenderer.P.Y - time * 100 + length * 100 + MidiPlayer.Timer;

                py1 -= length * 100;
                py2 -= length * 100;

                if (IsEditMode && !_isProgressBarHovered && !_isProgressBarActive)
                {
                    if (ImGui.GetIO().KeyCtrl && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !_isRectMode)
                    {
                        _rectStart = ImGui.GetMousePos();
                        _isRightRect = false;
                        _isRectMode = true;
                    }

                    if (ImGui.GetIO().KeyCtrl && ImGui.IsMouseDown(ImGuiMouseButton.Right) && !_isRectMode)
                    {
                        _rectStart = ImGui.GetMousePos();
                        _isRightRect = true;
                        _isRectMode = true;
                    }

                    if (_isRectMode)
                    {
                        // only allow rect going top-left
                        if (ImGui.GetMousePos().Y > _rectStart.Y || ImGui.GetMousePos().X > _rectStart.X)
                        {
                            _isRectMode = false;
                        }

                        Vector4 rectCol = _isRightRect ? ThemeManager.RightHandCol : ThemeManager.LeftHandCol;
                        var v3 = new Vector3(rectCol.X, rectCol.Y, rectCol.Z);
                        ImGui.GetWindowDrawList().AddRectFilled(_rectStart, ImGui.GetMousePos(), ImGui.GetColorU32(new Vector4(v3, .005f)));

                        float rpx1;
                        float rpx2;
                        if (note.NoteName.ToString().EndsWith("Sharp"))
                        {
                            rpx1 = PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4;
                            rpx2 = PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4;
                        }
                        else
                        {
                            rpx1 = PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width;
                            rpx2 = PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width;
                        }

                        bool isInside = IsRectInside(_rectStart, ImGui.GetMousePos(), new(rpx1, py1), new(rpx2, py2));
                        if (isInside)
                        {
                            MidiEditing.SetRightHand(index, _isRightRect);
                        }
                    }

                    if ((ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right)) && _isRectMode)
                    {
                        MidiEditing.SaveData();
                        _rectEnd = ImGui.GetMousePos();
                        _isRectMode = false;
                    }

                    if (note.NoteName.ToString().EndsWith("Sharp"))
                    {
                        if (ImGui.IsMouseHoveringRect(new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4, py1),
                            new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4, py2)))
                        {
                            if (ShowTextNotes)
                            {
                                Drawings.NoteTooltip($"Note: {note.NoteName}\nOctave: {note.Octave}\nVelocity: {note.Velocity}" +
                                    $"\nNumber: {note.NoteNumber}\nRight Hand: {LeftRightData.S_IsRightNote[index]}");
                            }

                            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && !_isRectMode)
                            {
                                // set left
                                MidiEditing.SetRightHand(index, false);
                                MidiEditing.SaveData();
                            }
                            else if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && !_isRectMode)
                            {
                                // set right
                                MidiEditing.SetRightHand(index, true);
                                MidiEditing.SaveData();
                            }
                        }
                    }
                    else
                    {
                        if (ImGui.IsMouseHoveringRect(new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width, py1),
                            new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width, py2)))
                        {
                            if (ShowTextNotes)
                            {
                                Drawings.NoteTooltip($"Note: {note.NoteName}\nOctave: {note.Octave}\nVelocity: {note.Velocity}" +
                                    $"\nNumber: {note.NoteNumber}\nRight Hand: {LeftRightData.S_IsRightNote[index]}");
                            }

                            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && !_isRectMode)
                            {
                                // set left
                                MidiEditing.SetRightHand(index, false);
                                MidiEditing.SaveData();
                            }
                            else if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && !_isRectMode)
                            {
                                // set right
                                MidiEditing.SetRightHand(index, true);
                                MidiEditing.SaveData();
                            }
                        }
                    }
                }
                else
                {
                    // Disable rect mode when the progress bar is hovered or active
                    _isRectMode = false;
                }

                // skip notes outside of screen to save performance
                if (py2 < 0 || py1 > PianoRenderer.P.Y)
                {
                    index++;
                    continue;
                }
            }

            if (note.NoteName.ToString().EndsWith("Sharp"))
            {
                if (CoreSettings.NeonFx && AccessibilityRuntime.Presentation.AllowGlow)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float thickness = i * 2;
                        float alpha = 0.2f + (3 - i) * 0.2f;
                        uint color = ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, alpha) * 0.5f * 0.7f);
                        drawList.AddRect(
                            new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4 - 1, py1 - 1),
                            new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4 + 1, py2 + 1),
                            color,
                            CoreSettings.NoteRoundness,
                            0,
                            thickness
                        );
                    }
                }
                else
                {
                    uint color = ImGui.GetColorU32(new Vector4(Vector3.Zero, 1f) * 0.5f);
                    drawList.AddRect(
                        new Vector2(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4 - 1, py1 - 1),
                        new Vector2(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4 + 1, py2 + 1),
                        color,
                        CoreSettings.NoteRoundness,
                        0,
                        1f
                    );
                }

                drawList.AddRectFilled(new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4, py1),
                      new(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4, py2),
                      GetSharpColor(index), CoreSettings.NoteRoundness, ImDrawFlags.RoundCornersAll);

                if (ShowTextNotes)
                {
                    ImGui.PushFont(FontController.Font16_Icon12);
                    string noteInfo = Drawings.GetNoteTextAs(TextType, note);
                    
                    if (TextType == TextTypes.NoteName)
                        noteInfo = noteInfo.Replace("Sharp", "#");
                    var textSize = ImGui.CalcTextSize(noteInfo) / 2;
                    var pos = new Vector2(PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width - textSize.X + 1,
                        py2 - length * 100 / 2 - textSize.Y);

                    drawList.AddText(pos + new Vector2(1), ImGui.GetColorU32(new Vector4(0, 0, 0, 1)), noteInfo);
                    drawList.AddText(pos, ImGui.GetColorU32(Vector4.One), noteInfo);
                    ImGui.PopFont();
                }
                if (IsPracticeMode)
                {
                    DrawPracticeHandMarker(
                        drawList,
                        new Vector2(
                            PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 3 / 4,
                            py1),
                        new Vector2(
                            PianoRenderer.P.X + PianoRenderer.BlackNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width * 5 / 4,
                            py2),
                        LeftRightData.S_IsRightNote[index]);
                }
            }
            else
            {
                if (CoreSettings.NeonFx && AccessibilityRuntime.Presentation.AllowGlow)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float thickness = i * 2;
                        float alpha = 0.2f + (3 - i) * 0.2f;
                        uint color = ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, alpha) * 0.5f);
                        drawList.AddRect(
                            new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width - 1, py1 - 1),
                            new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width + 1, py2 + 1),
                            color,
                            CoreSettings.NoteRoundness,
                            0,
                            thickness
                        );
                    }
                }
                else
                {
                    uint color = ImGui.GetColorU32(new Vector4(Vector3.Zero, 1f) * 0.5f);
                    drawList.AddRect(
                        new Vector2(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width - 1, py1 - 1),
                        new Vector2(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width + 1, py2 + 1),
                        color,
                        CoreSettings.NoteRoundness,
                        0,
                        1f
                    );
                }

                drawList.AddRectFilled(new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width, py1),
                    new(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width, py2),
                    ImGui.GetColorU32(col), CoreSettings.NoteRoundness, ImDrawFlags.RoundCornersAll);

                if (ShowTextNotes)
                {
                    ImGui.PushFont(FontController.Font16_Icon12);
                    string noteInfo = Drawings.GetNoteTextAs(TextType, note);
                    var pos = new Vector2(PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width / 2 - ImGui.CalcTextSize(noteInfo).X / 2,
                        py2 - length * 100 / 2 - ImGui.CalcTextSize(noteInfo).Y / 2);
                    drawList.AddText(pos + new Vector2(1), ImGui.GetColorU32(new Vector4(0, 0, 0, 1)), noteInfo);
                    drawList.AddText(pos, ImGui.GetColorU32(Vector4.One), noteInfo);
                    ImGui.PopFont();
                }
                if (IsPracticeMode)
                {
                    DrawPracticeHandMarker(
                        drawList,
                        new Vector2(
                            PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width,
                            py1),
                        new Vector2(
                            PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(note.NoteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width,
                            py2),
                        LeftRightData.S_IsRightNote[index]);
                }
            }
            index++;
        }
        DrawPracticeTarget();
        DrawPracticeFeedback();
    }

    private static void DrawPracticeHandMarker(
        ImDrawListPtr drawList,
        Vector2 minimum,
        Vector2 maximum,
        bool isRightHand)
    {
        ImGui.PushFont(FontController.Font16_Icon12);
        var label = isRightHand ? "R" : "L";
        var size = ImGui.CalcTextSize(label);
        var position = new Vector2(
            minimum.X + Math.Max(1f, (maximum.X - minimum.X - size.X) / 2),
            minimum.Y + Math.Max(1f, (maximum.Y - minimum.Y - size.Y) / 2));
        drawList.AddText(position + Vector2.One, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)), label);
        drawList.AddText(position, ImGui.GetColorU32(Vector4.One), label);
        ImGui.PopFont();
    }

    private static void DrawPracticeTarget()
    {
        var target = MidiPracticeSession.Snapshot?.Target;
        if (target is null)
            return;

        foreach (var pitch in target.Pitches)
        {
            var radius = ImGuiUtils.FixedSize(new Vector2(8)).X;
            var fill = AccessibilityRuntime.Presentation.UseSystemContrast
                ? AccessibilityRuntime.ContrastPalette.Highlight
                : ThemeManager.RightHandCol;
            var outline = AccessibilityRuntime.Presentation.UseSystemContrast
                ? AccessibilityRuntime.ContrastPalette.WindowText
                : Vector4.One;
            ImGui.GetForegroundDrawList().AddCircleFilled(
                PracticePitchCenter(pitch),
                radius,
                ImGui.GetColorU32(fill));
            ImGui.GetForegroundDrawList().AddCircle(
                PracticePitchCenter(pitch),
                radius,
                ImGui.GetColorU32(outline),
                0,
                Math.Max(2f, FontController.DSF * 2f));
        }
    }

    private static void DrawPracticeFeedback()
    {
        foreach (var pitchFeedback in MidiPracticeSession.LatestFeedback.GroupBy(item => item.Pitch))
        {
            var row = 0;
            foreach (var feedback in pitchFeedback)
            {
                var label = feedback.Judgment switch
                {
                    TimingJudgment.Fantastic => "Fantastic",
                    TimingJudgment.Early => $"Early {Math.Abs(feedback.SignedOffsetMicroseconds ?? 0) / 1_000m:0} ms",
                    TimingJudgment.Late => $"Late {Math.Abs(feedback.SignedOffsetMicroseconds ?? 0) / 1_000m:0} ms",
                    TimingJudgment.Miss => "Miss",
                    TimingJudgment.Extra => "Extra",
                    _ => feedback.Judgment.ToString()
                };
                var center = PracticePitchCenter(feedback.Pitch);
                var position = new Vector2(
                    center.X - ImGui.CalcTextSize(label).X / 2,
                    center.Y - ImGuiUtils.FixedSize(new Vector2(32 + row * 18)).Y);
                var color = feedback.Judgment switch
                {
                    TimingJudgment.Fantastic => new Vector4(0.20f, 0.85f, 0.35f, 1),
                    TimingJudgment.Early or TimingJudgment.Late => new Vector4(1f, 0.72f, 0.15f, 1),
                    _ => new Vector4(0.95f, 0.25f, 0.25f, 1)
                };
                if (AccessibilityRuntime.Presentation.UseSystemContrast)
                    color = AccessibilityRuntime.ContrastPalette.WindowText;
                ImGui.GetForegroundDrawList().AddText(
                    position + Vector2.One * Math.Max(1f, FontController.DSF),
                    ImGui.GetColorU32(AccessibilityRuntime.Presentation.UseSystemContrast
                        ? AccessibilityRuntime.ContrastPalette.Window
                        : new Vector4(0, 0, 0, 1)),
                    label);
                ImGui.GetForegroundDrawList().AddText(position, ImGui.GetColorU32(color), label);
                row++;
            }
        }
    }

    private static Vector2 PracticePitchCenter(byte pitch)
    {
        var noteNumber = (SevenBitNumber)pitch;
        return PianoRenderer.BlackNoteToKey.TryGetValue(noteNumber, out var blackKey)
            ? new Vector2(
                PianoRenderer.P.X + blackKey * PianoRenderer.Width + PianoRenderer.Width * 0.75f + 10,
                PianoRenderer.P.Y + PianoRenderer.Height / 1.7f)
            : new Vector2(
                PianoRenderer.P.X + PianoRenderer.WhiteNoteToKey.GetValueOrDefault(noteNumber, 0) * PianoRenderer.Width + PianoRenderer.Width / 2,
                PianoRenderer.P.Y + PianoRenderer.Height / 1.2f);
    }

    private static void GetPlaybackInputs()
    {
        if (ImGui.GetIO().MouseWheel != 0)
        {
            if (ImGui.GetIO().KeyCtrl)
            {
                float scrollAmount = ImGui.GetIO().MouseWheel * 0.5f;
                float newTime = Math.Clamp(MidiPlayer.Seconds - scrollAmount, 0, (float)MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalSeconds);
                SeekPlaybackTo(newTime);
            }
            else if (!IsPracticeMode)
            {
                float speedDelta = ImGui.GetIO().MouseWheel * 0.25f;
                float newSpeed = (float)(MidiPlayer.Playback.Speed + speedDelta);
                MidiPlayer.Playback.Speed = Math.Clamp(newSpeed, 0.25f, 4);
            }
        }

        var panButton = IsEditMode ? ImGuiMouseButton.Middle : ImGuiMouseButton.Right;
        if (ImGui.IsMouseHoveringRect(Vector2.Zero, new(ImGui.GetIO().DisplaySize.X, PianoRenderer.P.Y)) && ImGui.IsMouseDown(panButton))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
            const float interpolationFactor = 0.05f;
            const float decelerationFactor = 0.75f;
            float mouseDeltaY = ImGui.GetIO().MouseDelta.Y;
            if (UpDirection) mouseDeltaY = -mouseDeltaY;
            _panVelocity = Lerp(_panVelocity, mouseDeltaY, interpolationFactor);
            _panVelocity *= decelerationFactor;
            float targetTime = Math.Clamp(MidiPlayer.Seconds + _panVelocity, 0, (float)MidiPlayer.Playback.GetDuration<MetricTimeSpan>().TotalSeconds);
            var newTime = Lerp(MidiPlayer.Seconds, targetTime, interpolationFactor);
            SeekPlaybackTo(newTime);
        }

        if (IsMappedCommandPressed(PracticeCommand.TogglePlayPause))
        {
            if (IsPracticeMode)
            {
                MidiPracticeSession.TogglePause();
            }
            else
            {
                MidiPlayer.IsTimerRunning = !MidiPlayer.IsTimerRunning;
                if (MidiPlayer.IsTimerRunning)
                    MidiPlayer.Playback.Start();
                else
                    MidiPlayer.Playback.Stop();
            }
        }

        if (IsMappedCommandPressed(PracticeCommand.ToggleDirection) && !IsEditMode)
        {
            SetUpDirection(!UpDirection);
        }

        if (IsMappedCommandPressed(PracticeCommand.ToggleNoteLabels))
        {
            SetTextNotes(!ShowTextNotes);
        }

        if (IsMappedCommandPressed(PracticeCommand.SeekForward))
        {
            float n = ImGui.GetIO().KeyCtrl ? 0.1f : 1f;
            var newTime = Math.Clamp(MidiPlayer.Seconds + n, 0, (float)MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalSeconds);
            SeekPlaybackTo(newTime);
        }

        if (IsMappedCommandPressed(PracticeCommand.SeekBackward))
        {
            float n = ImGui.GetIO().KeyCtrl ? 0.1f : 1f;
            var newTime = Math.Clamp(MidiPlayer.Seconds - n, 0, (float)MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalSeconds);
            SeekPlaybackTo(newTime);
        }
    }

    private static void SeekPlaybackTo(float seconds)
    {
        var microseconds = Math.Max(0, (long)(seconds * 1_000_000));
        if (IsPracticeMode)
        {
            MidiPracticeSession.Seek(ChartTime.FromMicroseconds(microseconds));
            return;
        }

        MidiPlayer.Playback.MoveToTime(new MetricTimeSpan(microseconds));
        MidiPlayer.Seconds = seconds;
        MidiPlayer.Timer = seconds * 100 * FallSpeedVal;
    }

    private static void GetInputs()
    {
        if (CoreSettings.KeyboardInput && !UiOwnsKeyboard())
        {
            VirtualKeyboard.ListenForKeyPresses();
        }

        if (IsMappedCommandPressed(PracticeCommand.ToggleGlow))
        {
            CoreSettings.SetNeonFx(!CoreSettings.NeonFx);
        }

        if (!UiOwnsKeyboard() && ImGui.IsKeyPressed(ImGuiKey.UpArrow, false))
        {
            switch (FallSpeed)
            {
                case FallSpeeds.Slow:
                    SetFallSpeed(FallSpeeds.Default);
                    break;
                case FallSpeeds.Default:
                    SetFallSpeed(FallSpeeds.Fast);
                    break;
                case FallSpeeds.Fast:
                    SetFallSpeed(FallSpeeds.Faster);
                    break;
            }
        }

        if (!UiOwnsKeyboard() && ImGui.IsKeyPressed(ImGuiKey.DownArrow, false))
        {
            switch (FallSpeed)
            {
                case FallSpeeds.Faster:
                    SetFallSpeed(FallSpeeds.Fast);
                    break;
                case FallSpeeds.Fast:
                    SetFallSpeed(FallSpeeds.Default);
                    break;
                case FallSpeeds.Default:
                    SetFallSpeed(FallSpeeds.Slow);
                    break;
            }
        }
    }

    private static bool UiOwnsKeyboard()
    {
        return ImGui.GetIO().WantTextInput ||
               ImGui.IsAnyItemActive() ||
               ImGui.IsAnyItemFocused();
    }

    private static bool IsMappedCommandPressed(PracticeCommand expected)
    {
        var io = ImGui.GetIO();
        var context = new PracticeInputContext(
            io.WantTextInput,
            ImGui.IsAnyItemActive() || ImGui.IsAnyItemFocused(),
            CoreSettings.KeyboardInput,
            _showPracticeTools || ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId));
        foreach (var key in PracticeCommandKeys)
        {
            if (!ImGui.IsKeyPressed(key.ImGuiKey, false))
                continue;

            var stroke = new PracticeKeyStroke(
                key.PracticeKey,
                Control: io.KeyCtrl,
                Shift: io.KeyShift,
                Alt: io.KeyAlt);
            if (PracticeCommandMap.TryMap(context, stroke, out var command) && command == expected)
                return true;
        }

        return false;
    }

    public static void RenderCanvas(bool playMode = false)
    {
        using (AutoFont font22 = new(FontController.GetFontOfSize(22)))
        {
            CanvasPos = ImGui.GetWindowPos();
            RenderGrid();

            if (CoreSettings.FpsCounter)
            {
                var fps = $"{ImGui.GetIO().Framerate:0 FPS}";
                ImGui.GetWindowDrawList().AddText(new(ImGui.GetIO().DisplaySize.X - ImGui.CalcTextSize(fps).X - 5, ImGui.GetContentRegionAvail().Y - 25),
                    ImGui.GetColorU32(Vector4.One), fps);
            }

            if (playMode)
                DrawInputNotes();
            else
                DrawPlaybackNotes();

            DrawPracticeStatus();

            GetInputs();

            var showTopBar = ImGui.IsMouseHoveringRect(Vector2.Zero, new(ImGui.GetIO().DisplaySize.X, 300));
            if (_comboFallSpeed || _comboPlaybackSpeed || _leftHandColorPicker || _rightHandColorPicker || _comboSoundFont || _comboPlugins)
                showTopBar = true;

            if (playMode)
            {
                if (showTopBar || LockTopBar)
                {
                    DrawPlayModeControls();
                    DrawPlayModeRightControls();
                }
            }

            if (!playMode)
            {
                GetPlaybackInputs();

                if (showTopBar || LockTopBar)
                {
                    DrawProgressBar();
                    DrawPlaybackControls();
                    DrawPlaybackRightControls();
                }
            }

            DrawSharedControls(showTopBar, playMode);

            if (IsPracticeMode && _showPracticeTools)
                DrawPracticeTools();
        }
    }

    private static void DrawPracticeStatus()
    {
        if (MidiPracticeSession.Snapshot is not { } snapshot)
            return;

        var lines = new List<string>();
        var mode = MidiPracticeSession.Mode switch
        {
            PracticeMode.WaitForNotes => "Wait for Notes",
            PracticeMode.PlayInTime => "Play in Time",
            PracticeMode.Recital => "Recital",
            _ => "Practice"
        };
        var status = snapshot.State switch
        {
            PracticeSessionState.CountingIn => $"{mode} · Count-in · {snapshot.CountInBeatsRemaining}",
            PracticeSessionState.Running => $"{mode} · Playing",
            PracticeSessionState.WaitingForInput => $"{mode} · Waiting for input",
            PracticeSessionState.LearnerPaused when snapshot.ResumeCountInPending =>
                $"{mode} · Paused · Count-in on resume",
            PracticeSessionState.LearnerPaused => $"{mode} · Paused",
            PracticeSessionState.Completed => $"{mode} · Completed",
            _ => $"{mode} · {snapshot.State}"
        };
        lines.Add(status);

        if (MidiPracticeSession.AccessibilityDescription is { } description)
        {
            if (!string.IsNullOrEmpty(description.TargetText))
                lines.Add(description.TargetText);
            if (!string.IsNullOrEmpty(description.FeedbackText))
                lines.Add(description.FeedbackText);
            if (!string.IsNullOrEmpty(description.NavigationText))
                lines.Add(description.NavigationText);
        }

        if (MidiPracticeSession.LatestResult is { } result)
        {
            var timing = result.Timing is null
                ? "Timing N/A"
                : $"Timing {result.Timing.AverageAbsoluteErrorMicroseconds / 1_000m:0} ms avg";
            var assisted = result.Assisted ? " · Assisted" : string.Empty;
            var summary = $"Completion {result.Completion.Ratio:P1} · " +
                          $"Accuracy {result.Accuracy.RequiredNotesHitRatio:P1} · " +
                          $"{result.Accuracy.ExtraNotes} Extra · {timing}{assisted}";
            lines.Add(summary);

            if (MidiPracticeSession.LatestProgress is { } practiceProgress)
            {
                var progress = practiceProgress.For(
                    result.Setup,
                    result.Timing?.CalibrationRevision ?? 0);
                lines.Add(PracticeProgressSummary(result, progress));
            }
        }

        if (MidiPracticeSession.ProgressWarning is { } warning)
            lines.Add(warning);

        if (MidiPracticeSession.NavigationWarning is { } navigationWarning)
            lines.Add(navigationWarning);

        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, ImGuiUtils.FixedSize(new Vector2(20)).X);
        var width = Math.Max(160f, display.X - margin * 2);
        var compact = display.X < ImGuiUtils.FixedSize(new Vector2(1000)).X;
        var requestedTop = ImGuiUtils.FixedSize(new Vector2(compact ? 185 : 160)).Y;
        var top = Math.Min(requestedTop, display.Y * 0.58f);
        var wrapWidth = Math.Max(100f, width - ImGuiUtils.FixedSize(new Vector2(24)).X);
        var contentHeight = ImGui.GetTextLineHeightWithSpacing() * 2;
        foreach (var line in lines)
        {
            contentHeight += ImGui.CalcTextSize(line, false, wrapWidth).Y +
                             ImGui.GetStyle().ItemSpacing.Y;
        }
        var availableHeight = Math.Max(80f, display.Y - top - margin);
        var height = Math.Min(contentHeight, availableHeight);
        var background = AccessibilityRuntime.Presentation.UseSystemContrast
            ? AccessibilityRuntime.ContrastPalette.Window
            : new Vector4(ThemeManager.MainBgCol.X, ThemeManager.MainBgCol.Y, ThemeManager.MainBgCol.Z, 1f);

        ImGui.SetCursorScreenPos(new Vector2(margin, CanvasPos.Y + top));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, background);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, Math.Max(2f, FontController.DSF * 2f));
        var visible = ImGui.BeginChild(
            "Practice Status",
            new Vector2(width, height),
            ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Border,
            contentHeight > availableHeight ? ImGuiWindowFlags.AlwaysVerticalScrollbar : ImGuiWindowFlags.None);
        ImGui.PopStyleVar();
        if (visible)
        {
            ImGui.SeparatorText("Practice Status");
            foreach (var line in lines)
                ImGui.TextWrapped(line);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static void DrawPracticeTools()
    {
        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, ImGuiUtils.FixedSize(new Vector2(16)).X);
        var maximum = new Vector2(
            Math.Max(220f, display.X - margin * 2),
            Math.Max(220f, display.Y - margin * 2));
        var requested = ImGuiUtils.FixedSize(new Vector2(580, 720));
        ImGui.SetNextWindowSize(
            new Vector2(
                Math.Min(requested.X, maximum.X),
                Math.Min(requested.Y, maximum.Y)),
            ImGuiCond.Always);
        if (!ImGui.Begin(
                "Practice tools",
                ref _showPracticeTools,
                ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false) &&
            !ImGui.GetIO().WantTextInput &&
            !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId))
        {
            _showPracticeTools = false;
            ImGui.End();
            return;
        }

        DrawActivePracticeSetupControls();
        ImGui.Separator();
        DrawPracticeTimingControls();
        ImGui.Separator();
        DrawPracticeLoopControls();
        ImGui.Separator();
        DrawPracticeBookmarkControls();

        var warning = _practiceToolsWarning ?? MidiPracticeSession.NavigationWarning;
        if (warning is not null)
        {
            ImGui.Separator();
            ImGui.TextWrapped(warning);
        }
        ImGui.End();
    }

    private static void DrawActivePracticeSetupControls()
    {
        ImGui.Text("Practice setup");
        if (MidiPracticeSession.Preferences is not { } preferences)
            return;

        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(230)).X);
        if (ImGui.BeginCombo(
                "Mode##ActivePracticeMode",
                PracticeModeLabel(preferences.Mode)))
        {
            foreach (var mode in Enum.GetValues<PracticeMode>())
            {
                if (ImGui.Selectable(PracticeModeLabel(mode), mode == preferences.Mode))
                {
                    _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                        preferences with { Mode = mode });
                }
            }
            ImGui.EndCombo();
        }

        preferences = MidiPracticeSession.Preferences ?? preferences;
        if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(250)).X)
            ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(230)).X);
        if (ImGui.BeginCombo(
                "Required Hands##ActivePracticeHands",
                preferences.RequiredHands.ToString()))
        {
            foreach (var hands in Enum.GetValues<RequiredHands>())
            {
                if (ImGui.Selectable(hands.ToString(), hands == preferences.RequiredHands))
                {
                    _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                        preferences.WithRequiredHands(hands));
                }
            }
            ImGui.EndCombo();
        }

        preferences = MidiPracticeSession.Preferences ?? preferences;
        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(230)).X);
        if (ImGui.BeginCombo(
                "Tempo##ActivePracticeTempo",
                $"{preferences.TempoRatio:0.##}x"))
        {
            foreach (var tempoRatio in new[] { 0.25m, 0.5m, 0.75m, 1m, 1.25m, 1.5m, 2m })
            {
                if (ImGui.Selectable(
                        $"{tempoRatio:0.##}x",
                        tempoRatio == preferences.TempoRatio))
                {
                    _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                        preferences with { TempoRatio = tempoRatio });
                }
            }
            ImGui.EndCombo();
        }

        preferences = MidiPracticeSession.Preferences ?? preferences;
        if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(250)).X)
            ImGui.SameLine();
        ImGui.BeginDisabled(preferences.RequiredHands == RequiredHands.Both);
        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(230)).X);
        if (ImGui.BeginCombo(
                "Accompaniment##ActivePracticeAccompaniment",
                preferences.Accompaniment.ToString()))
        {
            foreach (var accompaniment in Enum.GetValues<Accompaniment>())
            {
                if (ImGui.Selectable(
                        accompaniment.ToString(),
                        accompaniment == preferences.Accompaniment))
                {
                    _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                        preferences with { Accompaniment = accompaniment });
                }
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped("Changing setup or range starts a fresh comparable Practice Session.");
    }

    private static void DrawPracticeTimingControls()
    {
        ImGui.Text("Timing");
        if (MidiPracticeSession.Preferences is not { } preferences)
            return;

        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(180)).X);
        if (ImGui.BeginCombo(
                "Count-in##ActivePracticeCountIn",
                preferences.CountInBeats == 0
                    ? "Off"
                    : $"{preferences.CountInBeats} beats"))
        {
            foreach (var beats in PracticePreferences.SupportedCountInBeats)
            {
                var label = beats == 0 ? "Off" : $"{beats} beats";
                if (ImGui.Selectable(label, beats == preferences.CountInBeats))
                {
                    _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                        preferences with { CountInBeats = beats });
                }
            }
            ImGui.EndCombo();
        }

        preferences = MidiPracticeSession.Preferences ?? preferences;
        var metronomeEnabled = preferences.MetronomeEnabled;
        if (ImGui.Checkbox("Metronome", ref metronomeEnabled))
        {
            _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                preferences with { MetronomeEnabled = metronomeEnabled });
        }

        preferences = MidiPracticeSession.Preferences ?? preferences;
        var countInOnLoopRepeat = preferences.CountInOnLoopRepeat;
        if (ImGui.Checkbox("Count in on every loop pass", ref countInOnLoopRepeat))
        {
            _practiceToolsWarning = MidiPracticeSession.UpdatePreferences(
                preferences with { CountInOnLoopRepeat = countInOnLoopRepeat });
        }
        ImGui.TextWrapped(
            "Count-in clicks always sound. The metronome setting controls only the clicks while the Chart is playing.");
        ImGui.BeginDisabled(!MidiPracticeSession.CanRestartAfterError);
        if (ImGui.Button("Restart after error"))
            _practiceToolsWarning = MidiPracticeSession.RestartAfterError();
        ImGui.EndDisabled();
    }

    private static void DrawPracticeLoopControls()
    {
        ImGui.Text("Loops");
        var navigation = MidiPracticeSession.Navigation;
        var selectedLoop = _editingLoopId is { } selectedId
            ? navigation.Loops.FirstOrDefault(loop => loop.Id == selectedId)
            : null;

        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(280)).X);
        if (ImGui.BeginCombo(
                "Saved loop##PracticeLoop",
                selectedLoop?.Name ?? "New loop"))
        {
            if (ImGui.Selectable("New loop", _editingLoopId is null))
                BeginNewLoopDraft();
            foreach (var loop in navigation.Loops.OrderBy(loop => loop.Range.Start))
            {
                if (ImGui.Selectable(loop.Name, loop.Id == _editingLoopId))
                    EditLoop(loop);
            }
            ImGui.EndCombo();
        }

        ImGui.InputText("Name##PracticeLoopName", ref _practiceLoopName, 80);
        if (ImGui.Button("Mark start at playhead"))
        {
            if (MidiPracticeSession.Snapshot is { } snapshot)
                _loopStart = MidiPracticeSession.SnapToNearestBeat(snapshot.Position);
        }
        if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(180)).X)
            ImGui.SameLine();
        if (ImGui.Button("End after playhead"))
        {
            if (MidiPracticeSession.Snapshot is { } snapshot)
                _loopEnd = MidiPracticeSession.SnapToNextBeatBoundary(snapshot.Position);
        }

        ImGui.Text(
            $"Range: {FormatChartTime(_loopStart)} – {FormatChartTime(_loopEnd)}");
        var validRange = _loopStart is { } start &&
                         _loopEnd is { } end &&
                         end.CompareTo(start) > 0;
        ImGui.BeginDisabled(!validRange);
        if (ImGui.Button(_editingLoopId is null ? "Save loop" : "Save changes"))
        {
            var id = _editingLoopId ?? Guid.NewGuid();
            _practiceToolsWarning = MidiPracticeSession.SaveLoop(
                id,
                _practiceLoopName,
                new PracticeRange(_loopStart!.Value, _loopEnd!.Value));
            if (_practiceToolsWarning is null)
                _editingLoopId = id;
        }
        ImGui.EndDisabled();

        if (selectedLoop is not null)
        {
            if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(150)).X)
                ImGui.SameLine();
            var isActive = MidiPracticeSession.ActiveLoop?.Id == selectedLoop.Id;
            if (ImGui.Button(isActive ? "Disable loop" : "Enable loop"))
            {
                _practiceToolsWarning = MidiPracticeSession.SetActiveLoop(
                    isActive ? null : selectedLoop.Id);
            }
            if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(110)).X)
                ImGui.SameLine();
            if (ImGui.Button("Go to start"))
                MidiPracticeSession.GoToLoopStart(selectedLoop.Id);
            if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(110)).X)
                ImGui.SameLine();
            if (ImGui.Button("Delete loop"))
            {
                _practiceToolsWarning = MidiPracticeSession.DeleteLoop(selectedLoop.Id);
                if (_practiceToolsWarning is null)
                    BeginNewLoopDraft();
            }
        }

        if (!validRange && (_loopStart is not null || _loopEnd is not null))
            ImGui.TextWrapped("A loop end must be after its start. The end uses the next Chart beat boundary.");
        ImGui.TextWrapped("Enabling or editing a loop starts a new comparable Practice Session for that fixed range.");
    }

    private static void DrawPracticeBookmarkControls()
    {
        ImGui.Text("Bookmarks");
        var navigation = MidiPracticeSession.Navigation;
        var selectedBookmark = _editingBookmarkId is { } selectedId
            ? navigation.Bookmarks.FirstOrDefault(bookmark => bookmark.Id == selectedId)
            : null;

        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(280)).X);
        if (ImGui.BeginCombo(
                "Saved bookmark##PracticeBookmark",
                selectedBookmark?.Name ?? "New bookmark"))
        {
            if (ImGui.Selectable("New bookmark", _editingBookmarkId is null))
            {
                _editingBookmarkId = null;
                _practiceBookmarkName = "Bookmark";
            }
            foreach (var bookmark in navigation.Bookmarks.OrderBy(bookmark => bookmark.Position))
            {
                if (ImGui.Selectable(bookmark.Name, bookmark.Id == _editingBookmarkId))
                {
                    _editingBookmarkId = bookmark.Id;
                    _practiceBookmarkName = bookmark.Name;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.InputText("Name##PracticeBookmarkName", ref _practiceBookmarkName, 80);
        if (selectedBookmark is null && ImGui.Button("Save at playhead"))
        {
            if (MidiPracticeSession.Snapshot is { } snapshot)
            {
                var id = Guid.NewGuid();
                _practiceToolsWarning = MidiPracticeSession.SaveBookmark(
                    id,
                    _practiceBookmarkName,
                    snapshot.Position);
                if (_practiceToolsWarning is null)
                    _editingBookmarkId = id;
            }
        }

        if (selectedBookmark is not null)
        {
            if (ImGui.Button("Rename bookmark"))
            {
                _practiceToolsWarning = MidiPracticeSession.SaveBookmark(
                    selectedBookmark.Id,
                    _practiceBookmarkName,
                    selectedBookmark.Position);
            }
            if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(160)).X)
                ImGui.SameLine();
            if (ImGui.Button("Move to playhead"))
            {
                if (MidiPracticeSession.Snapshot is { } snapshot)
                {
                    _practiceToolsWarning = MidiPracticeSession.SaveBookmark(
                        selectedBookmark.Id,
                        _practiceBookmarkName,
                        snapshot.Position);
                }
            }
        }

        ImGui.BeginDisabled(navigation.Bookmarks.Count == 0);
        if (ImGui.Button("Previous bookmark"))
            MidiPracticeSession.GoToBookmark(PracticeNavigationDirection.Previous);
        if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(160)).X)
            ImGui.SameLine();
        if (ImGui.Button("Next bookmark"))
            MidiPracticeSession.GoToBookmark(PracticeNavigationDirection.Next);
        ImGui.EndDisabled();

        if (selectedBookmark is not null)
        {
            if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(150)).X)
                ImGui.SameLine();
            if (ImGui.Button("Go to bookmark"))
                MidiPracticeSession.GoToBookmark(selectedBookmark.Id);
            if (ImGui.GetContentRegionAvail().X >= ImGuiUtils.FixedSize(new Vector2(140)).X)
                ImGui.SameLine();
            if (ImGui.Button("Delete bookmark"))
            {
                _practiceToolsWarning = MidiPracticeSession.DeleteBookmark(selectedBookmark.Id);
                if (_practiceToolsWarning is null)
                {
                    _editingBookmarkId = null;
                    _practiceBookmarkName = "Bookmark";
                }
            }
        }

        ImGui.TextWrapped("Going to a bookmark outside the enabled loop disables the loop and starts an assisted attempt there.");
    }

    private static void BeginNewLoopDraft()
    {
        _editingLoopId = null;
        _practiceLoopName = "Loop";
        _loopStart = null;
        _loopEnd = null;
    }

    private static void EditLoop(PracticeLoop loop)
    {
        _editingLoopId = loop.Id;
        _practiceLoopName = loop.Name;
        _loopStart = loop.Range.Start;
        _loopEnd = loop.Range.End;
    }

    private static string FormatChartTime(ChartTime? time)
    {
        if (time is not { } value)
            return "not set";
        var totalSeconds = value.Microseconds / 1_000_000d;
        return $"{(int)(totalSeconds / 60):00}:{totalSeconds % 60:00.0}";
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

    private static string PracticeProgressSummary(
        PracticeResult result,
        PracticeProgressSnapshot progress)
    {
        var parts = new List<string>();
        if (progress.BestAccuracy is { } accuracyBest)
        {
            parts.Add(
                $"Accuracy PB {accuracyBest.Result.Accuracy.RequiredNotesHitRatio:P1}/" +
                $"{accuracyBest.Result.Accuracy.ExtraNotes} Extra" +
                PersonalBestStatus(result, accuracyBest));
        }
        if (progress.BestTiming is { } timingBest)
        {
            parts.Add(
                $"Timing PB {timingBest.Result.Timing!.AverageAbsoluteErrorMicroseconds / 1_000m:0} ms" +
                PersonalBestStatus(result, timingBest));
        }
        if (progress.FirstCompletion is { } firstCompletion)
            parts.Add($"First completion {firstCompletion.Result.EndedAtUtc.ToLocalTime():d}");
        parts.Add(
            $"Trend A/E/T {progress.RecentTrend.Accuracy}/" +
            $"{progress.RecentTrend.Extras}/{progress.RecentTrend.Timing}");
        return string.Join(" · ", parts);
    }

    private static string PersonalBestStatus(PracticeResult result, PracticePersonalBest best)
    {
        if (best.LatestMatchedAtUtc != result.EndedAtUtc)
            return string.Empty;
        return best.MatchCount == 1 ? " achieved" : $" matched ×{best.MatchCount}";
    }

    private static void DrawProgressBar()
    {
        ImGui.SetNextItemWidth(ImGui.GetIO().DisplaySize.X);

        var background = AccessibilityRuntime.Presentation.UseSystemContrast
            ? AccessibilityRuntime.ContrastPalette.Window
            : ThemeManager.MainBgCol;
        var progress = AccessibilityRuntime.Presentation.UseSystemContrast
            ? AccessibilityRuntime.ContrastPalette.Highlight
            : ThemeManager.RightHandCol;
        var pBarBg = new Vector3(background.X, background.Y, background.Z);
        var oldFrameBg = ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBg];
        var oldFrameBgHovered = ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBgHovered];
        var oldFrameBgActive = ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBgActive];
        var oldSliderGrab = ImGuiTheme.Style.Colors[(int)ImGuiCol.SliderGrab];
        var oldSliderGrabActive = ImGuiTheme.Style.Colors[(int)ImGuiCol.SliderGrabActive];

        var frameAlpha = AccessibilityRuntime.Presentation.AllowTransparency ? 0.8f : 1f;
        ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(pBarBg, frameAlpha);
        ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(pBarBg, frameAlpha);
        ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(pBarBg, frameAlpha);
        ImGuiTheme.Style.Colors[(int)ImGuiCol.SliderGrab] = progress;
        ImGuiTheme.Style.Colors[(int)ImGuiCol.SliderGrabActive] = progress;

        if (ImGui.SliderFloat("##Progress slider", ref MidiPlayer.Seconds, 0, (float)MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalSeconds, "%.1f",
            ImGuiSliderFlags.NoRoundToFormat | ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.NoInput))
        {
            SeekPlaybackTo(MidiPlayer.Seconds);
        }
        _isProgressBarActive = ImGui.IsItemActive();
        _isProgressBarHovered = ImGui.IsItemHovered();
        if (_isProgressBarActive && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        }

        var pBarHeight = ImGui.GetItemRectSize().Y;
        var playbackPercentage = MidiPlayer.Seconds * 100 / (float)MidiFileData.MidiFile.GetDuration<MetricTimeSpan>().TotalSeconds;
        var pBarWidth = ImGui.GetIO().DisplaySize.X * playbackPercentage / 100;
        var v3 = new Vector3(progress.X, progress.Y, progress.Z);
        if (AccessibilityRuntime.Presentation.AllowTransparency)
        {
            ImGui.GetWindowDrawList().AddRectFilled(
                Vector2.Zero,
                new Vector2(pBarWidth, pBarHeight),
                ImGui.GetColorU32(new Vector4(v3, 0.2f)));
        }

        ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBg] = oldFrameBg;
        ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBgHovered] = oldFrameBgHovered;
        ImGuiTheme.Style.Colors[(int)ImGuiCol.FrameBgActive] = oldFrameBgActive;
        ImGuiTheme.Style.Colors[(int)ImGuiCol.SliderGrab] = oldSliderGrab;
        ImGuiTheme.Style.Colors[(int)ImGuiCol.SliderGrabActive] = oldSliderGrabActive;
    }

    private static void DrawPlaybackControls()
    {
        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, ImGuiUtils.FixedSize(new Vector2(24)).X);
        var controlsWidth = Math.Min(
            ImGuiUtils.FixedSize(new Vector2(500)).X,
            Math.Max(200f, display.X - margin * 2));
        var controlsHeight = ImGuiUtils.FixedSize(new Vector2(62)).Y;
        ImGui.SetCursorScreenPos(new Vector2(
            Math.Max(margin, (display.X - controlsWidth) / 2),
            CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
        var visible = ImGui.BeginChild(
            "Player controls",
            new Vector2(controlsWidth, controlsHeight),
            ImGuiChildFlags.None,
            ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (visible)
        {
            var normalText = AccessibilityRuntime.Presentation.UseSystemContrast
                ? AccessibilityRuntime.ContrastPalette.ButtonText
                : Vector4.One;
            var playColor = !MidiPlayer.IsTimerRunning ? normalText : ThemeManager.RightHandCol;
            var buttonSize = new Vector2(
                ImGuiUtils.FixedSize(new Vector2(112)).X,
                ImGuiUtils.FixedSize(new Vector2(50)).Y);

            // PLAY BUTTON
            ImGui.PushFont(FontController.Font16_Icon16);
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = playColor;
            if (ImGui.Button($"{FontAwesome6.Play} Play", buttonSize))
            {
                if (IsPracticeMode)
                    MidiPracticeSession.Resume();
                else
                {
                    MidiPlayer.Playback.Start();
                    MidiPlayer.StartTimer();
                }
            }
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = normalText;
            var pauseColor = MidiPlayer.IsTimerRunning ? normalText : new(0.90f, 0.35f, 0.35f, 1);
            ImGui.SameLine();
            // PAUSE BUTTON
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = pauseColor;
            if (ImGui.Button($"{FontAwesome6.Pause} Pause", buttonSize))
            {
                if (IsPracticeMode)
                    MidiPracticeSession.Pause();
                else
                {
                    MidiPlayer.Playback.Stop();
                    MidiPlayer.IsTimerRunning = false;
                }
            }
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = normalText;
            ImGui.SameLine();
            // STOP BUTTON
            if (ImGui.Button($"{FontAwesome6.Stop} Stop", buttonSize) || IsMappedCommandPressed(PracticeCommand.ClearInput))
            {
                MidiPlayer.SoundFontEngine?.StopAllNote(0);
                if (IsPracticeMode)
                    MidiPracticeSession.Pause();
                else
                {
                    MidiPlayer.Playback.Stop();
                    MidiPlayer.Playback.MoveToStart();
                    MidiPlayer.IsTimerRunning = false;
                    MidiPlayer.Timer = 0;
                }
            }
            ImGui.SameLine();
            // RECORD SCREEN BUTTON
            ImGui.PushStyleColor(ImGuiCol.Text, ScreenRecorder.Status == RecorderStatus.Recording ? new Vector4(0.08f, 0.80f, 0.27f, 1) : normalText);
            if (ImGui.Button($"{FontAwesome6.Video} Record", buttonSize)
                || IsMappedCommandPressed(PracticeCommand.ToggleRecording))
            {
                switch (ScreenRecorder.Status)
                {
                    case RecorderStatus.Idle:
                        ScreenRecorder.StartRecording();
                        if (CoreSettings.VideoRecStartsPlayback)
                        {
                            if (IsPracticeMode)
                                MidiPracticeSession.Resume();
                            else
                            {
                                MidiPlayer.Playback.Start();
                                MidiPlayer.StartTimer();
                            }
                        }
                        break;
                    case RecorderStatus.Recording:
                        ScreenRecorder.EndRecording();
                        MidiPlayer.SoundFontEngine?.StopAllNote(0);
                        if (IsPracticeMode)
                            MidiPracticeSession.Pause();
                        else
                        {
                            MidiPlayer.Playback.Stop();
                            MidiPlayer.Playback.MoveToStart();
                            MidiPlayer.IsTimerRunning = false;
                            MidiPlayer.Timer = 0;
                        }
                        break;
                }
            }
            ImGui.PopStyleColor();

            ImGui.PopFont();
        }
        ImGui.EndChild();
    }

    private static void DrawPlaybackRightControls()
    {
        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, ImGuiUtils.FixedSize(new Vector2(24)).X);
        var panelWidth = Math.Min(
            ImGuiUtils.FixedSize(new Vector2(240)).X,
            Math.Max(160f, display.X - margin * 2));
        var compact = display.X < ImGuiUtils.FixedSize(new Vector2(1000)).X;
        var top = CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(compact ? 120 : 50)).Y;
        var left = Math.Max(margin, display.X - panelWidth - margin);

        ImGui.SetCursorScreenPos(new Vector2(left, top));
        if (ImGui.Button("View options", new Vector2(panelWidth, ImGuiUtils.FixedSize(new Vector2(50)).Y)))
            ImGui.OpenPopup("View options menu");

        var menuOpen = ImGui.BeginPopup("View options menu");
        if (menuOpen)
        {
            var popupWidth = ImGuiUtils.FixedSize(new Vector2(280)).X;
            if (!IsEditMode && ImGui.Button(
                    $"Note direction: {(UpDirection ? "Up" : "Down")}",
                    new Vector2(popupWidth, 0)))
            {
                SetUpDirection(!UpDirection);
            }

            if (ImGui.Button(
                    $"Note labels: {(ShowTextNotes ? "Shown" : "Hidden")}",
                    new Vector2(popupWidth, 0)))
            {
                SetTextNotes(!ShowTextNotes);
            }

            ImGui.SetNextItemWidth(popupWidth);
            if (ImGui.BeginCombo("Label content", TextType.ToString()))
            {
                foreach (var textType in Enum.GetValues<TextTypes>())
                {
                    if (ImGui.Selectable(textType.ToString(), textType == TextType))
                        SetTextType(textType);
                }
                ImGui.EndCombo();
            }

            if (ImGui.Button(
                    $"Top bar: {(LockTopBar ? "Locked" : "Auto-hide")}",
                    new Vector2(popupWidth, 0)))
            {
                SetLockTopBar(!LockTopBar);
            }

            var isFullScreen = Program._window.WindowState == WindowState.BorderlessFullScreen;
            if (ImGui.Button(
                    isFullScreen ? "Exit full screen" : "Enter full screen",
                    new Vector2(popupWidth, 0)))
            {
                Program._window.WindowState = isFullScreen
                    ? WindowState.Normal
                    : WindowState.BorderlessFullScreen;
            }

            ImGui.SetNextItemWidth(popupWidth);
            if (ImGui.BeginCombo("Fall speed", $"{FallSpeed}", ImGuiComboFlags.HeightLarge))
            {
                _comboFallSpeed = true;
                foreach (var speed in Enum.GetValues(typeof(FallSpeeds)))
                {
                    if (ImGui.Selectable(speed.ToString()))
                        SetFallSpeed((FallSpeeds)speed);
                }
                ImGui.EndCombo();
            }
            else
            {
                _comboFallSpeed = false;
            }

            if (!IsPracticeMode)
            {
                ImGui.SetNextItemWidth(popupWidth);
                if (ImGui.BeginCombo("Playback speed", $"{MidiPlayer.Playback.Speed}x", ImGuiComboFlags.HeightLarge))
                {
                    _comboPlaybackSpeed = true;
                    for (float speed = 0.25f; speed <= 4; speed += 0.25f)
                    {
                        if (ImGui.Selectable($"{speed}x"))
                            MidiPlayer.Playback.Speed = speed;
                    }
                    ImGui.EndCombo();
                }
                else
                {
                    _comboPlaybackSpeed = false;
                }
            }
            else if (ImGui.Button("Loops & bookmarks", new Vector2(popupWidth, 0)))
            {
                _showPracticeTools = true;
            }
            ImGui.EndPopup();
        }
        else
        {
            _comboFallSpeed = false;
            _comboPlaybackSpeed = false;
        }
    }

    private static void DrawHandToggleButtons()
    {
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(295)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(110)).Y));
        ImGui.PushStyleColor(ImGuiCol.Button, LeftHandActive ? ImGuiTheme.Button : ImGuiTheme.DarkButton);
        if (ImGui.Button("Left hand", ImGuiUtils.FixedSize(new Vector2(100, 40))))
        {
            LeftHandActive = !LeftHandActive;
        }
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(405)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(110)).Y));
        ImGui.PushStyleColor(ImGuiCol.Button, RightHandActive ? ImGuiTheme.Button : ImGuiTheme.DarkButton);
        if (ImGui.Button("Right hand", ImGuiUtils.FixedSize(new Vector2(100, 40))))
        {
            RightHandActive = !RightHandActive;
        }
        ImGui.PopStyleColor();
        ImGui.PopFont();
    }

    private static void DrawSharedControls(bool showTopBar, bool playMode)
    {
        if (!showTopBar && !LockTopBar)
            return;

        // BACK BUTTON
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.BeginDisabled(ScreenRecorder.Status == RecorderStatus.Recording);
        ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(25)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
        if (ImGui.Button($"{FontAwesome6.ArrowLeftLong} Back", ImGuiUtils.FixedSize(new Vector2(120, 50))) || IsMappedCommandPressed(PracticeCommand.Exit))
        {
            MidiPracticeSession.Deactivate();
            MidiPlayer.Playback?.Stop();
            MidiPlayer.Playback?.MoveToStart();
            MidiPlayer.IsTimerRunning = false;
            MidiPlayer.Timer = 0;
            var route = playMode ? Enums.Windows.Home : Enums.Windows.MidiBrowser;
            WindowsManager.SetWindow(route);
        }
        ImGui.EndDisabled();
        ImGui.PopFont();

        var glowAvailable = AccessibilityRuntime.Presentation.AllowGlow;
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.BeginDisabled(!glowAvailable);
        ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(25)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(110)).Y));
        if (ImGui.Button(
                glowAvailable ? $"Glow: {(CoreSettings.NeonFx ? "On" : "Off")}" : "Glow: Reduced",
                ImGuiUtils.FixedSize(new Vector2(120, 40))))
        {
            CoreSettings.SetNeonFx(!CoreSettings.NeonFx);
        }
        ImGui.EndDisabled();
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(155)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(110)).Y));
        if (ImGui.Button("Hand colors", ImGuiUtils.FixedSize(new Vector2(125, 40))))
            ImGui.OpenPopup("Hand colors menu");
        if (ImGui.BeginPopup("Hand colors menu"))
        {
            ImGui.ColorEdit4("Left hand color", ref ThemeManager.LeftHandCol,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoDragDrop |
                ImGuiColorEditFlags.NoOptions | ImGuiColorEditFlags.NoAlpha);
            ImGui.ColorEdit4("Right hand color", ref ThemeManager.RightHandCol,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoDragDrop |
                ImGuiColorEditFlags.NoOptions | ImGuiColorEditFlags.NoAlpha);
            ImGui.EndPopup();
        }
        _leftHandColorPicker = ImGui.IsPopupOpen("Hand colors menu");
        _rightHandColorPicker = _leftHandColorPicker;

        if (!playMode && !IsPracticeMode)
        {
            DrawHandToggleButtons();
        }

        if (CoreSettings.SoundEngine == SoundEngine.SoundFonts)
        {
            // SOUNDFONTS DROPDOWN LIST
            ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(160)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
            if (ImGui.BeginCombo("##SoundFont", SoundFontPlayer.ActiveSoundFont, ImGuiComboFlags.HeightLargest | ImGuiComboFlags.WidthFitPreview))
            {
                _comboSoundFont = true;
                foreach (var folderPath in SoundFontsPathsManager.SoundFontsPaths)
                {
                    foreach (var soundFontPath in Directory.GetFiles(folderPath).Where(f => Path.GetExtension(f) == ".sf2"))
                    {
                        if (ImGui.Selectable(Path.GetFileNameWithoutExtension(soundFontPath)))
                        {
                            MidiPlayer.SoundFontEngine?.StopAllNote(0);
                            SoundFontPlayer.LoadSoundFont(soundFontPath);
                        }
                    }
                }
                ImGui.EndCombo();
            }
            else
                _comboSoundFont = false;
        }
        else if (CoreSettings.SoundEngine == SoundEngine.Plugins)
        {
            var instrument = VstPlayer.PluginsChain?.PluginInstrument;
            var name = instrument == null ? "No Plugin Instrument" : instrument.PluginName;

            // PLUGINS DROPDOWN LIST
            ImGui.SetCursorScreenPos(new(ImGuiUtils.FixedSize(new Vector2(160)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
            if (ImGui.BeginCombo("##Plugins", name, ImGuiComboFlags.HeightLargest | ImGuiComboFlags.WidthFitPreview))
            {
                _comboPlugins = true;

                ImGui.SeparatorText("Instrument");

                ImGui.Text(name);
                ImGui.SameLine();
                if (ImGui.SmallButton($"{FontAwesome6.ScrewdriverWrench}##tweak_instrument") && instrument is VstPlugin vstInstrument)
                {
                    vstInstrument.OpenPluginWindow();
                }
                ImGui.SameLine();
                if (ImGui.SmallButton($"{FontAwesome6.FolderOpen}##change_instrument"))
                {
                    var dialog = new OpenFileDialog()
                    {
                        Title = "Select a VST2 plugin instrument",
                        Filter = "vst plugin (*.dll)|*.dll"
                    };
                    dialog.ShowOpenFileDialog();

                    if (dialog.Success)
                    {
                        var file = new FileInfo(dialog.Files.First());
                        var plugin = new VstPlugin(file.FullName);
                        if (plugin.PluginType != PluginType.Instrument)
                        {
                            plugin.Dispose();
                            User32.MessageBox(IntPtr.Zero, "Plugin is not an instrument.", "Error Loading Plugin",
                                User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
                        }
                        else
                        {
                            VstPlayer.PluginsChain.AddPlugin(plugin);
                            PluginsPathManager.LoadValidInstrumentPath(file.FullName);
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.SeparatorText("Effects");

                foreach (var effect in VstPlayer.PluginsChain.FxPlugins.ToList())
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(effect.PluginName);
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"{FontAwesome6.ScrewdriverWrench}##tweak_effect{effect.PluginId}") && effect is VstPlugin vstEffect)
                    {
                        vstEffect.OpenPluginWindow();
                    }
                    bool enabled = effect.Enabled;
                    string state = enabled ? "ON" : "OFF";
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"{state}##{effect.PluginId}"))
                    {
                        effect.Enabled = !effect.Enabled;
                    }
                }

                ImGui.EndCombo();
            }
            else
                _comboPlugins = false;
        }

        // SUSTAIN PEDAL BUTTON
        // ImageButton padding according to https://github.com/ocornut/imgui/issues/6901#issuecomment-1749178625
        var imagePadding = ImGui.GetStyle().FramePadding * 2.0f;
        ImGui.SetCursorPos(ImGui.GetWindowSize() - ImGuiUtils.FixedSize(new Vector2(65) + imagePadding));
        if (ImGui.ImageButton("SustainBtn", IOHandle.SustainPedalActive ? Drawings.SustainPedalOn : Drawings.SustainPedalOff, 
                ImGuiUtils.FixedSize(new Vector2(50))))
        {
            IOHandle.OnEventReceived(null, new Melanchall.DryWetMidi.Multimedia.MidiEventReceivedEventArgs(
                new ControlChangeEvent(ControlUtilities.AsSevenBitNumber(ControlName.DamperPedal),
                new SevenBitNumber((byte)(IOHandle.SustainPedalActive ? 0 : 100)))));
            DevicesManager.ODevice?.SendEvent(new ControlChangeEvent(new SevenBitNumber(64), new SevenBitNumber((byte)(IOHandle.SustainPedalActive ? 0 : 100))));
        }
    }

    private static void DrawPlayModeControls()
    {
        ImGui.SetNextWindowPos(new Vector2(ImGui.GetIO().DisplaySize.X / 2 - ImGuiUtils.FixedSize(new Vector2(110)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
        if (ImGui.BeginChild("Player controls", ImGuiUtils.FixedSize(new Vector2(220, 50)), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var recordColor = MidiRecording.IsRecording() ? new Vector4(1, 0, 0, 1) : Vector4.One;

            // RECORD BUTTON
            ImGui.PushFont(FontController.Font16_Icon16);
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = recordColor;
            if (ImGui.Button($"{FontAwesome6.CircleDot}", new(ImGuiUtils.FixedSize(new Vector2(50)).X, ImGui.GetWindowSize().Y)))
            {
                MidiRecording.StartRecording();
            }
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = Vector4.One;
            ImGui.SameLine();
            // STOP BUTTON
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = new(0.70f, 0.22f, 0.22f, 1);
            if (ImGui.Button($"{FontAwesome6.Stop}", new(ImGuiUtils.FixedSize(new Vector2(50)).X, ImGui.GetWindowSize().Y)))
            {
                MidiRecording.StopRecording();
            }
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = Vector4.One;
            ImGui.SameLine();
            // SAVE RECORDING BUTTON
            if (ImGui.Button($"{FontAwesome6.SdCard}", new(ImGuiUtils.FixedSize(new Vector2(50)).X, ImGui.GetWindowSize().Y)))
            {
                MidiRecording.SaveRecordingToFile();
            }
            ImGui.SameLine();
            // RECORD SCREEN BUTTON
            ImGui.PushStyleColor(ImGuiCol.Text, ScreenRecorder.Status == RecorderStatus.Recording ? new Vector4(0.08f, 0.80f, 0.27f, 1) : Vector4.One);
            if (ImGui.Button($"{FontAwesome6.Video}", new(ImGuiUtils.FixedSize(new Vector2(50)).X, ImGui.GetWindowSize().Y))
                || IsMappedCommandPressed(PracticeCommand.ToggleRecording))
            {
                switch (ScreenRecorder.Status)
                {
                    case RecorderStatus.Idle:
                        MidiPlayer.ClearPlayback();
                        ScreenRecorder.StartRecording();
                        break;
                    case RecorderStatus.Recording:
                        ScreenRecorder.EndRecording();
                        break;
                }
            }
            ImGui.PopStyleColor();
            ImGui.PopFont();
            ImGui.EndChild();
        }      
    }

    private static void DrawPlayModeRightControls()
    {
        var icon = LockTopBar ? FontAwesome6.Lock : FontAwesome6.LockOpen;

        // LOCK BUTTON
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.SetCursorScreenPos(new(ImGui.GetIO().DisplaySize.X - ImGuiUtils.FixedSize(new Vector2(280)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
        if (ImGui.Button(icon, ImGuiUtils.FixedSize(new Vector2(50))))
        {
            SetLockTopBar(!LockTopBar);
        }
        ImGui.PopFont();

        if (!MidiRecording.IsRecording())
        {
            // VIEW LAST RECORDING BUTTON
            ImGui.SetCursorScreenPos(new(ImGui.GetIO().DisplaySize.X - ImGuiUtils.FixedSize(new Vector2(220)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
            if (ImGui.Button("View last recording", ImGuiUtils.FixedSize(new Vector2(180, 50))))
            {
                var recordedMidi = MidiRecording.GetRecordedMidi();
                if (recordedMidi != null)
                {
                    LeftRightData.S_IsRightNote.Clear();
                    foreach (var n in recordedMidi.GetNotes())
                    {
                        LeftRightData.S_IsRightNote.Add(true);
                    }
                    MidiFileHandler.LoadMidiFile(recordedMidi);
                    WindowsManager.SetWindow(Enums.Windows.MidiPlayback);
                }
            }

            // FALLSPEED DROPDOWN LIST
            ImGui.SetCursorScreenPos(new(ImGui.GetIO().DisplaySize.X - ImGuiUtils.FixedSize(new Vector2(220)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(110)).Y));
            if (ImGui.BeginCombo("##Fall speed", $"{FallSpeed}",
                ImGuiComboFlags.WidthFitPreview | ImGuiComboFlags.HeightLarge))
            {
                foreach (var speed in Enum.GetValues(typeof(FallSpeeds)))
                {
                    if (ImGui.Selectable(speed.ToString()))
                    {
                        SetFallSpeed((FallSpeeds)speed);
                    }
                }
                ImGui.EndCombo();
            }

            var fullScreenIcon = Program._window.WindowState == WindowState.BorderlessFullScreen ? FontAwesome6.Minimize : FontAwesome6.Expand;

            // FULLSCREEN BUTTON
            ImGui.PushFont(FontController.Font16_Icon16);
            ImGui.SetCursorScreenPos(new(ImGui.GetIO().DisplaySize.X - ImGuiUtils.FixedSize(new Vector2(30)).X, CanvasPos.Y + ImGuiUtils.FixedSize(new Vector2(50)).Y));
            if (ImGui.Button(fullScreenIcon, ImGuiUtils.FixedSize(new Vector2(25))))
            {
                var windowsState = Program._window.WindowState == WindowState.BorderlessFullScreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
                Program._window.WindowState = windowsState;
            }
            ImGui.PopFont();
        }
    }
}
