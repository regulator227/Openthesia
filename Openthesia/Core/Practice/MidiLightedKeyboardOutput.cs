using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Openthesia.Settings;

namespace Openthesia.Core.Practice;

internal sealed class MidiLightedKeyboardOutput : ILightedKeyboardOutput
{
    public void Send(LightedKeyboardMessage message)
    {
        var midiEvent = CreateMidiEvent(message);
        DevicesManager.ODevice?.SendEvent(midiEvent);
    }

    private static MidiEvent CreateMidiEvent(LightedKeyboardMessage message)
    {
        var noteNumber = (SevenBitNumber)message.Pitch;
        var velocity = (SevenBitNumber)message.Velocity;
        var channel = (FourBitNumber)(byte)(message.MidiChannel - 1);

        return message.Kind switch
        {
            LightedKeyboardMessageKind.NoteOn => new NoteOnEvent(noteNumber, velocity)
            {
                Channel = channel
            },
            LightedKeyboardMessageKind.NoteOff => new NoteOffEvent(noteNumber, velocity)
            {
                Channel = channel
            },
            _ => throw new ArgumentOutOfRangeException(nameof(message))
        };
    }
}
