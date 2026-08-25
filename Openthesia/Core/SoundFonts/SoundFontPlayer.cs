using MeltySynth;
using NAudio.Wave;
using Openthesia.Core.Audio;
using Openthesia.Core.Midi;
using Openthesia.Enums;
using Openthesia.Settings;
using Vanara.PInvoke;

namespace Openthesia.Core.SoundFonts;

public class SoundFontPlayer
{
    private Synthesizer _synthesizer;
    public Synthesizer Synthesizer => _synthesizer;

    private MidiSampleProvider _midiSampleProvider;
    public MidiSampleProvider MidiSampleProvider => _midiSampleProvider;

    private AudioOutputSession _output;
    public WaveOutEvent? WaveOut => _output.WaveOut;
    public AsioOut? AsioOut => _output.AsioOut;

    private static string _activeSoundFont = string.Empty;
    public static string ActiveSoundFont => _activeSoundFont;
    private static string _activeSoundFontPath = string.Empty;
    public static string ActiveSoundFontPath => _activeSoundFontPath;

    // stores loaded soundfonts and their path
    private static Dictionary<string, SoundFont> _soundFontsPool = new();

    public SoundFontPlayer(string soundFontPath, int sampleRate = 44100)
    {
        // if not loaded in memory load and store the soundfont
        if (!_soundFontsPool.ContainsKey(soundFontPath))
        {
            var soundFont = new SoundFont(soundFontPath);
            LoadSynthesizer(soundFont, sampleRate);
            _soundFontsPool.TryAdd(soundFontPath, soundFont);
        }
        else
        {
            // load the already in memory soundfont
            if (_soundFontsPool.TryGetValue(soundFontPath, out SoundFont? soundFont))
            {
                LoadSynthesizer(soundFont, sampleRate);
            }
        }
    }

    private void LoadSynthesizer(SoundFont soundFont, int sampleRate)
    {
        var settings = new SynthesizerSettings(sampleRate);
        settings.MaximumPolyphony = 256;
        _synthesizer = new Synthesizer(soundFont, settings);

        _midiSampleProvider = new MidiSampleProvider(_synthesizer);

        _output = AudioDriverManager.StartSelectedOutput(_midiSampleProvider, _output);
    }

    public static void Initialize()
    {
        string basePath = Path.GetDirectoryName(Environment.ProcessPath);

        string defaultSoundFontPath = Path.Combine(basePath, "SoundFonts\\SalamanderGrandPiano.sf2");
        if (File.Exists(defaultSoundFontPath))
        {
            // load default sound font
            LoadSoundFont(defaultSoundFontPath);
        }
        else
        {
            // load first available if default is missing or nothing
            var soundFonts = Directory.GetFiles(Path.Combine(basePath, "SoundFonts")).Where(f => Path.GetExtension(f) == ".sf2");
            if (soundFonts.Any())
            {
                if (File.Exists(soundFonts.ElementAt(0)))
                {
                    LoadSoundFont(soundFonts.ElementAt(0));
                }
            }
        }
    }

    public void ChangeLatency(int newLatency)
    {
        _output.ChangeWaveOutLatency(newLatency, _midiSampleProvider);
    }

    public static void LoadSoundFont(string soundFontPath, int sampleRate = 44100)
    {
        MidiPlayer.SoundFontEngine = new SoundFontPlayer(soundFontPath, sampleRate);
        _activeSoundFont = Path.GetFileNameWithoutExtension(soundFontPath);
        _activeSoundFontPath = Path.GetFullPath(soundFontPath);
    }

    public void PlayNote(int channel, int noteNumber, int velocity)
    {
        if (CoreSettings.SoundEngine != SoundEngine.SoundFonts)
            return;

        _synthesizer.NoteOn(channel, noteNumber, velocity);
    }

    public void StopNote(int channel, int noteNumber)
    {
        if (CoreSettings.SoundEngine != SoundEngine.SoundFonts)
            return;

        _synthesizer.NoteOff(channel, noteNumber);
    }

    public void StopAllNote(int channel)
    {
        if (CoreSettings.SoundEngine != SoundEngine.SoundFonts)
            return;

        _synthesizer.NoteOffAll(channel, false);
    }

    public void Dispose()
    {
        MidiPlayer.SoundFontEngine?.WaveOut?.Dispose();
        MidiPlayer.SoundFontEngine?.AsioOut?.Dispose();
    }
}
