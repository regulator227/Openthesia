using Openthesia.Core.Practice;
using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class LightedKeyboardGuidanceTests
{
    [Fact]
    public void WaitForNotesShowsTheNextTargetOnTheConfiguredMidiChannel()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60, 64 });

        guidance.Update(settings, PracticeMode.WaitForNotes, target);

        Assert.Equal(
            new[]
            {
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 60, 1),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 64, 1)
            },
            output.Messages);
    }

    [Fact]
    public void ChangingTargetClearsOldLightsBeforeShowingTheNewTarget()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        guidance.Update(
            settings,
            PracticeMode.WaitForNotes,
            new PracticeTarget(ChartTime.Zero, new byte[] { 60, 64 }));
        output.Messages.Clear();

        guidance.Update(
            settings,
            PracticeMode.WaitForNotes,
            new PracticeTarget(
                ChartTime.FromMicroseconds(500_000),
                new byte[] { 64, 67 }));

        Assert.Equal(
            new[]
            {
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 60, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 64, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 64, 1),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 67, 1)
            },
            output.Messages);
    }

    [Fact]
    public void RepeatingTheSameTargetDoesNotResendLightMessages()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 64, 60, 64 });
        guidance.Update(settings, PracticeMode.WaitForNotes, target);
        output.Messages.Clear();

        guidance.Update(settings, PracticeMode.WaitForNotes, target);

        Assert.Empty(output.Messages);
    }

    [Fact]
    public void DisablingGuidanceClearsEveryCurrentLight()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        guidance.Update(
            new LightedKeyboardSettings(Enabled: true, MidiChannel: 4),
            PracticeMode.WaitForNotes,
            new PracticeTarget(ChartTime.Zero, new byte[] { 60, 64 }));
        output.Messages.Clear();

        guidance.Update(
            new LightedKeyboardSettings(Enabled: false, MidiChannel: 4),
            PracticeMode.WaitForNotes,
            new PracticeTarget(ChartTime.Zero, new byte[] { 60, 64 }));

        Assert.Equal(
            new[]
            {
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 60, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 64, 0)
            },
            output.Messages);
    }

    [Fact]
    public void ChangingChannelClearsTheOldChannelBeforeLightingTheNewChannel()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60 });
        guidance.Update(
            new LightedKeyboardSettings(Enabled: true, MidiChannel: 4),
            PracticeMode.WaitForNotes,
            target);
        output.Messages.Clear();

        guidance.Update(
            new LightedKeyboardSettings(Enabled: true, MidiChannel: 5),
            PracticeMode.WaitForNotes,
            target);

        Assert.Equal(
            new[]
            {
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 60, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 5, 60, 1)
            },
            output.Messages);
    }

    [Fact]
    public void PlayInTimeShowsTheNextTarget()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);

        guidance.Update(
            new LightedKeyboardSettings(Enabled: true, MidiChannel: 1),
            PracticeMode.PlayInTime,
            new PracticeTarget(ChartTime.Zero, new byte[] { 60 }));

        Assert.Equal(
            new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 1, 60, 1),
            Assert.Single(output.Messages));
    }

    [Fact]
    public void RecitalClearsCurrentLightsWithoutShowingAnotherTarget()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60 });
        guidance.Update(settings, PracticeMode.WaitForNotes, target);
        output.Messages.Clear();

        guidance.Update(settings, PracticeMode.Recital, target);

        Assert.Equal(
            new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 60, 0),
            Assert.Single(output.Messages));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void InvalidMidiChannelDoesNotSendGuidance(int midiChannel)
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);

        guidance.Update(
            new LightedKeyboardSettings(Enabled: true, midiChannel),
            PracticeMode.WaitForNotes,
            new PracticeTarget(ChartTime.Zero, new byte[] { 60 }));

        Assert.Empty(output.Messages);
    }

    private sealed class RecordingLightedKeyboardOutput : ILightedKeyboardOutput
    {
        public List<LightedKeyboardMessage> Messages { get; } = new();

        public void Send(LightedKeyboardMessage message)
        {
            Messages.Add(message);
        }
    }
}
