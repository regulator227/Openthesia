using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Songs;

public sealed class ChartIdentityTests
{
    [Fact]
    public void EquivalentLearningPatternsHaveSameChartIdentity()
    {
        var first = CreateMidi(
            ticksPerQuarterNote: 480,
            noteLength: 480,
            channel: 0,
            velocity: 100,
            trackName: "Original title");
        var equivalent = CreateMidi(
            ticksPerQuarterNote: 960,
            noteLength: 960,
            channel: 4,
            velocity: 45,
            trackName: "Renamed chart");

        var firstId = ChartIdentity.FromMidi(first);
        var equivalentId = ChartIdentity.FromMidi(equivalent);

        Assert.Equal(firstId, equivalentId);
        Assert.StartsWith("chart-v1-sha256:", firstId.Value);
    }

    [Fact]
    public void RedundantTempoEventsDoNotChangeChartIdentity()
    {
        var original = CreateMidi(480, 480, 0, 100, "Song");
        var redundantTempoTrack = new TrackChunk(
            new SetTempoEvent(500_000) { DeltaTime = 240 });
        var withRedundantTempo = CreateMidi(480, 480, 0, 100, "Song");
        withRedundantTempo.Chunks.Add(redundantTempoTrack);

        Assert.Equal(
            ChartIdentity.FromMidi(original),
            ChartIdentity.FromMidi(withRedundantTempo));
    }

    [Fact]
    public void CanonicalNoteOrderDoesNotDependOnTrackOrder()
    {
        var lowTrack = CreateNoteTrack(60);
        var highTrack = CreateNoteTrack(72);
        var first = new MidiFile(lowTrack, highTrack);
        var reversed = new MidiFile(CreateNoteTrack(72), CreateNoteTrack(60));

        Assert.Equal(
            new byte[] { 60, 72 },
            ChartPattern.GetCanonicalNotes(first)
                .Select(note => (byte)note.Note.NoteNumber));
        Assert.Equal(
            ChartIdentity.FromMidi(first),
            ChartIdentity.FromMidi(reversed));
    }

    private static TrackChunk CreateNoteTrack(byte pitch)
    {
        return new TrackChunk(
            new NoteOnEvent((SevenBitNumber)pitch, (SevenBitNumber)100),
            new NoteOffEvent((SevenBitNumber)pitch, (SevenBitNumber)0) { DeltaTime = 480 });
    }

    private static MidiFile CreateMidi(
        short ticksPerQuarterNote,
        long noteLength,
        byte channel,
        byte velocity,
        string trackName)
    {
        var noteOn = new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)velocity)
        {
            Channel = (FourBitNumber)channel
        };
        var noteOff = new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0)
        {
            Channel = (FourBitNumber)channel,
            DeltaTime = noteLength
        };
        var track = new TrackChunk(
            new SequenceTrackNameEvent(trackName),
            new SetTempoEvent(500_000),
            new ProgramChangeEvent((SevenBitNumber)12) { Channel = (FourBitNumber)channel },
            noteOn,
            noteOff);

        return new MidiFile(track)
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(ticksPerQuarterNote)
        };
    }
}
