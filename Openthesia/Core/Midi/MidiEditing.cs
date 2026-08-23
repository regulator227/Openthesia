using System.Xml.Serialization;
using Vanara.PInvoke;
using Openthesia.Core.Songs;
using Openthesia.Settings;

namespace Openthesia.Core.Midi;

public static class MidiEditing
{
    public static void SetRightHand(int noteIndex, bool isRightHand)
    {
        LeftRightData.S_IsRightNote[noteIndex] = isRightHand;
    }

    public static void ReadData()
    {
        if (MidiFileData.Context is not { SourcePath: not null } context)
            return;

        var legacyCandidate = new LegacyHandAssignmentLocator(
            ProgramData.DataPath,
            ProgramData.HandsDataPath,
            MidiPathsManager.MidiPaths).Find(context.SourcePath) with
        {
            CanonicalToLegacyNoteIndices = ChartPattern
                .GetCanonicalNotes(MidiFileData.MidiFile)
                .Select(note => note.SourceIndex)
                .ToArray()
        };
        var result = new ChartHandAssignmentStore(ProgramData.DataPath).Load(
            context.ChartId,
            MidiFileData.Notes.Count(),
            legacyCandidate);
        LeftRightData.S_IsRightNote = result.Hands
            .Select(hand => hand == PianoHand.Right)
            .ToList();
        ShowWarning(result.Warning, "Couldn't read Hand Assignments");
    }

    public static void SaveData()
    {
        if (MidiFileData.Context is not { SourcePath: not null } context)
            return;

        var result = new ChartHandAssignmentStore(ProgramData.DataPath).Save(
            context.ChartId,
            LeftRightData.S_IsRightNote
                .Select(isRight => isRight ? PianoHand.Right : PianoHand.Left)
                .ToArray());
        ShowWarning(result.Warning, "Couldn't save Hand Assignments");
    }

    private static void ShowWarning(string? warning, string title)
    {
        if (warning is null)
            return;

        User32.MessageBox(
            IntPtr.Zero,
            warning,
            title,
            User32.MB_FLAGS.MB_ICONWARNING | User32.MB_FLAGS.MB_TOPMOST);
    }
}
