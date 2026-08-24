using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeChartFactoryTests
{
    [Fact]
    public void MidiNotesBecomeStableMetricPracticeNotesWithAssignedHands()
    {
        var midi = new MidiFile(
            NoteTrack(pitch: 72),
            NoteTrack(pitch: 60))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };
        var chartId = ChartIdentity.FromMidi(midi);

        var chart = PracticeChartFactory.FromMidi(
            chartId,
            midi,
            new[] { PianoHand.Left, PianoHand.Right });

        Assert.Equal(chartId, chart.Id);
        Assert.Equal(ChartTime.FromMicroseconds(500_000), chart.Duration);
        Assert.Collection(
            chart.Notes,
            note => Assert.Equal(
                new PracticeChartNote(
                    0,
                    60,
                    ChartTime.Zero,
                    ChartTime.FromMicroseconds(500_000),
                    PianoHand.Left),
                note),
            note => Assert.Equal(
                new PracticeChartNote(
                    1,
                    72,
                    ChartTime.Zero,
                    ChartTime.FromMicroseconds(500_000),
                    PianoHand.Right),
                note));
    }

    [Fact]
    public void PracticeBeatsFollowTempoChanges()
    {
        var track = new TrackChunk(
            new SetTempoEvent(500_000),
            new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100),
            new SetTempoEvent(1_000_000) { DeltaTime = 480 },
            new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { DeltaTime = 480 });
        var midi = new MidiFile(track)
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };

        var chart = PracticeChartFactory.FromMidi(
            ChartIdentity.FromMidi(midi),
            midi,
            new[] { PianoHand.Right });

        Assert.Equal(
            new[]
            {
                new PracticeBeat(ChartTime.Zero, IsDownbeat: true),
                new PracticeBeat(ChartTime.FromMicroseconds(500_000), IsDownbeat: false),
                new PracticeBeat(ChartTime.FromMicroseconds(1_500_000), IsDownbeat: false)
            },
            chart.Beats);
    }

    private static TrackChunk NoteTrack(byte pitch)
    {
        return new TrackChunk(
            new NoteOnEvent((SevenBitNumber)pitch, (SevenBitNumber)100),
            new NoteOffEvent((SevenBitNumber)pitch, (SevenBitNumber)0) { DeltaTime = 480 });
    }
}
