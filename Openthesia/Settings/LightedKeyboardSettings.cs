namespace Openthesia.Settings;

internal sealed record LightedKeyboardSettings(
    bool Enabled,
    int MidiChannel)
{
    internal static LightedKeyboardSettings Default { get; } = new(
        Enabled: false,
        MidiChannel: 1);

    internal static LightedKeyboardSettings FromDeviceSettings(
        bool enabled,
        int midiChannel)
    {
        return midiChannel is >= 1 and <= 16
            ? new LightedKeyboardSettings(enabled, midiChannel)
            : Default;
    }
}
