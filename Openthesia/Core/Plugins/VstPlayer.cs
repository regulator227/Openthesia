using Melanchall.DryWetMidi.Multimedia;
using NAudio.Mixer;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Openthesia.Core.Audio;
using Openthesia.Core.SoundFonts;
using Openthesia.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Openthesia.Core.Plugins;

public static class VstPlayer
{
    private static MixingSampleProvider _mixingSampleProvider;

    private static AudioOutputSession _output;
    public static WaveOutEvent? WaveOut => _output.WaveOut;
    public static AsioOut? AsioOut => _output.AsioOut;

    public static PluginsChain? PluginsChain { get; private set; }

    public static void Initialize()
    {
        var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(CoreSettings.SampleRate, 2))
        {
            ReadFully = true
        };

        _mixingSampleProvider = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(CoreSettings.SampleRate, 2))
        {
            ReadFully = true
        };

        PluginsChain = new PluginsChain(mixer);
        _mixingSampleProvider.AddMixerInput(PluginsChain);

        _output = AudioDriverManager.StartSelectedOutput(_mixingSampleProvider, _output);
    }

    public static void ChangeLatency(int newLatency)
    {
        _output.ChangeWaveOutLatency(newLatency, _mixingSampleProvider);
    }
}
