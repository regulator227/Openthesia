using System.Numerics;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Openthesia.Core.Songs;

internal static class ChartPattern
{
    public static IReadOnlyList<CanonicalChartNote> GetCanonicalNotes(MidiFile midiFile)
    {
        var tempoMap = midiFile.GetTempoMap();
        return midiFile.GetNotes()
            .Select((note, sourceIndex) => new CanonicalChartNote(
                note,
                sourceIndex,
                note.TimeAs<MusicalTimeSpan>(tempoMap),
                note.LengthAs<MusicalTimeSpan>(tempoMap)))
            .OrderBy(note => note.Onset, MusicalTimeSpanComparer.Instance)
            .ThenBy(note => note.Note.NoteNumber)
            .ThenBy(note => note.Duration, MusicalTimeSpanComparer.Instance)
            .ThenBy(note => note.SourceIndex)
            .ToArray();
    }

    private sealed class MusicalTimeSpanComparer : IComparer<MusicalTimeSpan>
    {
        public static MusicalTimeSpanComparer Instance { get; } = new();

        public int Compare(MusicalTimeSpan? left, MusicalTimeSpan? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;

            return ((BigInteger)left.Numerator * right.Denominator)
                .CompareTo((BigInteger)right.Numerator * left.Denominator);
        }
    }
}

internal sealed record CanonicalChartNote(
    Note Note,
    int SourceIndex,
    MusicalTimeSpan Onset,
    MusicalTimeSpan Duration);
