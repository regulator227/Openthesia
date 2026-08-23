using NAudio.Wave;

namespace Openthesia.Core.Audio;

internal readonly struct AudioOutputSession
{
    internal WaveOutEvent? WaveOut { get; }
    internal AsioOut? AsioOut { get; }

    internal AudioOutputSession(WaveOutEvent? waveOut, AsioOut? asioOut)
    {
        WaveOut = waveOut;
        AsioOut = asioOut;
    }

    internal void ChangeWaveOutLatency(int newLatency, ISampleProvider sampleProvider)
    {
        if (WaveOut is null)
            return;

        bool isRunning = WaveOut.PlaybackState == PlaybackState.Playing
            || WaveOut.PlaybackState == PlaybackState.Paused;
        if (isRunning)
        {
            WaveOut.Stop();
        }

        WaveOut.DesiredLatency = newLatency;
        WaveOut.Init(sampleProvider);
        WaveOut.Play();
    }
}
