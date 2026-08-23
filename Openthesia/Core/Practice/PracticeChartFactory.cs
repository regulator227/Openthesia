using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public static class PracticeChartFactory
{
    public static PracticeChart FromMidi(
        ChartId chartId,
        MidiFile midiFile,
        IReadOnlyList<PianoHand> hands)
    {
        ArgumentNullException.ThrowIfNull(chartId);
        ArgumentNullException.ThrowIfNull(midiFile);
        ArgumentNullException.ThrowIfNull(hands);

        var canonicalNotes = ChartPattern.GetCanonicalNotes(midiFile);
        if (canonicalNotes.Count != hands.Count)
            throw new ArgumentException("Hand Assignments must match the Chart's canonical notes.", nameof(hands));

        var tempoMap = midiFile.GetTempoMap();
        var notes = canonicalNotes
            .Select((canonicalNote, index) => new PracticeChartNote(
                Id: index,
                Pitch: canonicalNote.Note.NoteNumber,
                Onset: ToChartTime(canonicalNote.Note.TimeAs<MetricTimeSpan>(tempoMap)),
                Duration: ToChartTime(canonicalNote.Note.LengthAs<MetricTimeSpan>(tempoMap)),
                Hand: hands[index]))
            .ToArray();
        var duration = ToChartTime(midiFile.GetDuration<MetricTimeSpan>());

        return new PracticeChart(chartId, duration, notes);
    }

    private static ChartTime ToChartTime(MetricTimeSpan time)
    {
        return ChartTime.FromMicroseconds(time.TotalMicroseconds);
    }
}
