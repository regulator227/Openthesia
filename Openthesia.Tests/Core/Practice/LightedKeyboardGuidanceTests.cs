using Openthesia.Core.Practice;
using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class LightedKeyboardGuidanceTests
{
    [Fact]
    public void WaitForNotesShowsTheNextPracticeTargetOnTheConfiguredMidiChannel()
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
    public void ChangingPracticeTargetClearsOldLightsBeforeShowingTheNewPracticeTarget()
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
    public void RepeatingTheSamePracticeTargetDoesNotResendLightMessages()
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
    public void ResetBeforeAttachingOutputAllowsTheCurrentPracticeTargetToBeSent()
    {
        var output = new AttachableLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60 });
        guidance.Update(settings, PracticeMode.WaitForNotes, target);

        guidance.Clear();
        output.Attached = true;
        guidance.Update(settings, PracticeMode.WaitForNotes, target);

        Assert.Equal(
            new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 60, 1),
            Assert.Single(output.Messages));
    }

    [Fact]
    public void ClearContinuesAfterAnOutputFailureAndResetsThePracticeTarget()
    {
        var output = new RecordingLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60, 64 });
        guidance.Update(settings, PracticeMode.WaitForNotes, target);
        output.Messages.Clear();
        output.FailNextSend = true;

        guidance.Clear();
        guidance.Update(settings, PracticeMode.WaitForNotes, target);

        Assert.Equal(
            new[]
            {
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 64, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 60, 1),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 64, 1)
            },
            output.Messages);
    }

    [Fact]
    public void FailedNoteOnClearsPartialLightsAndRetriesThePracticeTarget()
    {
        var output = new FailSecondSendLightedKeyboardOutput();
        var guidance = new LightedKeyboardGuidance(output);
        var settings = new LightedKeyboardSettings(Enabled: true, MidiChannel: 4);
        var target = new PracticeTarget(ChartTime.Zero, new byte[] { 60, 64 });

        guidance.Update(settings, PracticeMode.WaitForNotes, target);
        guidance.Update(settings, PracticeMode.WaitForNotes, target);

        Assert.Equal(
            new[]
            {
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 60, 1),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 60, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOff, 4, 64, 0),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 60, 1),
                new LightedKeyboardMessage(LightedKeyboardMessageKind.NoteOn, 4, 64, 1)
            },
            output.Messages);
    }

    [Fact]
    public void PlayInTimeShowsTheNextPracticeTarget()
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
    public void RecitalClearsCurrentLightsWithoutShowingAnotherPracticeTarget()
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
        public bool FailNextSend { get; set; }

        public void Send(LightedKeyboardMessage message)
        {
            if (FailNextSend)
            {
                FailNextSend = false;
                throw new InvalidOperationException("The output device is unavailable.");
            }

            Messages.Add(message);
        }
    }

    private sealed class AttachableLightedKeyboardOutput : ILightedKeyboardOutput
    {
        public bool Attached { get; set; }
        public List<LightedKeyboardMessage> Messages { get; } = new();

        public void Send(LightedKeyboardMessage message)
        {
            if (Attached)
                Messages.Add(message);
        }
    }

    private sealed class FailSecondSendLightedKeyboardOutput : ILightedKeyboardOutput
    {
        private int _sendCount;

        public List<LightedKeyboardMessage> Messages { get; } = new();

        public void Send(LightedKeyboardMessage message)
        {
            _sendCount++;
            if (_sendCount == 2)
                throw new InvalidOperationException("The output device disconnected.");

            Messages.Add(message);
        }
    }
}
