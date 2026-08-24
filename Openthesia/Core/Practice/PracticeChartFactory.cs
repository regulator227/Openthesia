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
        var beats = CreateBeats(midiFile, tempoMap, duration);

        return new PracticeChart(chartId, duration, notes, beats);
    }

    private static IReadOnlyList<PracticeBeat> CreateBeats(
        MidiFile midiFile,
        TempoMap tempoMap,
        ChartTime duration)
    {
        if (midiFile.TimeDivision is not TicksPerQuarterNoteTimeDivision timeDivision)
            return Array.Empty<PracticeBeat>();

        var durationTicks = midiFile.GetDuration<MidiTimeSpan>().TimeSpan;
        var ticksPerBeat = timeDivision.TicksPerQuarterNote;
        var beats = new List<PracticeBeat>();
        var beatNumber = 0;
        for (long ticks = 0; ticks <= durationTicks; ticks += ticksPerBeat)
        {
            var position = ToChartTime(TimeConverter.ConvertTo<MetricTimeSpan>(
                (ITimeSpan)new MidiTimeSpan(ticks),
                tempoMap));
            if (position.CompareTo(duration) > 0)
                break;
            beats.Add(new PracticeBeat(position, IsDownbeat: beatNumber % 4 == 0));
            beatNumber++;
        }

        if (beats.Count == 0 || beats[^1].Position != duration)
            beats.Add(new PracticeBeat(duration, IsDownbeat: beatNumber % 4 == 0));
        return beats;
    }

    private static ChartTime ToChartTime(MetricTimeSpan time)
    {
        return ChartTime.FromMicroseconds(time.TotalMicroseconds);
    }
}
