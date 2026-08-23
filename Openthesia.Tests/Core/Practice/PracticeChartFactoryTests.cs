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

    private static TrackChunk NoteTrack(byte pitch)
    {
        return new TrackChunk(
            new NoteOnEvent((SevenBitNumber)pitch, (SevenBitNumber)100),
            new NoteOffEvent((SevenBitNumber)pitch, (SevenBitNumber)0) { DeltaTime = 480 });
    }
}
