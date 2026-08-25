using Openthesia.Settings;

namespace Openthesia.Core.Practice;

internal enum LightedKeyboardMessageKind
{
    NoteOn,
    NoteOff
}

internal readonly record struct LightedKeyboardMessage(
    LightedKeyboardMessageKind Kind,
    int MidiChannel,
    byte Pitch,
    byte Velocity);

internal interface ILightedKeyboardOutput
{
    void Send(LightedKeyboardMessage message);
}

internal sealed class LightedKeyboardGuidance
{
    private const byte GuidanceVelocity = 1;
    private readonly ILightedKeyboardOutput _output;
    private int? _litMidiChannel;
    private ChartTime? _litTargetOnset;
    private IReadOnlyList<byte> _litPitches = Array.Empty<byte>();

    internal LightedKeyboardGuidance(ILightedKeyboardOutput output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    internal void Update(
        LightedKeyboardSettings settings,
        PracticeMode mode,
        PracticeTarget? target)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Enabled ||
            settings.MidiChannel is < 1 or > 16 ||
            mode is not (PracticeMode.WaitForNotes or PracticeMode.PlayInTime) ||
            target is null)
        {
            Clear();
            return;
        }

        var targetPitches = target.Pitches.Distinct().OrderBy(pitch => pitch).ToArray();
        if (_litMidiChannel == settings.MidiChannel &&
            _litTargetOnset == target.Onset &&
            _litPitches.SequenceEqual(targetPitches))
        {
            return;
        }

        Clear();
        _litMidiChannel = settings.MidiChannel;
        _litTargetOnset = target.Onset;
        _litPitches = targetPitches;
        foreach (var pitch in _litPitches)
        {
            _output.Send(new LightedKeyboardMessage(
                LightedKeyboardMessageKind.NoteOn,
                settings.MidiChannel,
                pitch,
                GuidanceVelocity));
        }
    }

    internal void Clear()
    {
        if (_litMidiChannel is not { } midiChannel)
            return;

        foreach (var pitch in _litPitches)
        {
            _output.Send(new LightedKeyboardMessage(
                LightedKeyboardMessageKind.NoteOff,
                midiChannel,
                pitch,
                Velocity: 0));
        }

        _litMidiChannel = null;
        _litTargetOnset = null;
        _litPitches = Array.Empty<byte>();
    }
}
