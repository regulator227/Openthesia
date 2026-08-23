using NAudio.Wave;
using Openthesia.Core.Audio;
using Openthesia.Enums;
using Vanara.PInvoke;

namespace Openthesia.Settings;

public static class AudioDriverManager
{
    public static string SelectedAsioDriverName { get; private set; } = string.Empty;
    public static AudioDriverTypes AudioDriverType { get; private set; } = AudioDriverTypes.WaveOut;

    public static void SetAudioDriverType(AudioDriverTypes driverType)
    {
        AudioDriverType = driverType;
    }

    public static void SetAsioDriverDevice(string deviceName)
    {
        var drivers = AsioOut.GetDriverNames();
        var driverName = SelectAsioDriver(drivers, deviceName);
        if (driverName is not null)
        {
            SelectedAsioDriverName = driverName;
        }
    }

    internal static string? SelectAsioDriver(IReadOnlyCollection<string> drivers, string selectedDriverName)
    {
        return drivers.Contains(selectedDriverName)
            ? selectedDriverName
            : drivers.FirstOrDefault();
    }

    internal static AudioOutputSession StartSelectedOutput(
        ISampleProvider sampleProvider,
        AudioOutputSession currentOutput)
    {
        if (AudioDriverType == AudioDriverTypes.WaveOut)
        {
            currentOutput.AsioOut?.Stop();
            currentOutput.AsioOut?.Dispose();
            return new AudioOutputSession(StartWaveOut(sampleProvider), null);
        }

        if (AudioDriverType == AudioDriverTypes.ASIO)
        {
            currentOutput.WaveOut?.Stop();
            currentOutput.WaveOut?.Dispose();

            if (TryStartAsioOut(sampleProvider, out var asioOut))
            {
                return new AudioOutputSession(null, asioOut);
            }

            return new AudioOutputSession(StartWaveOut(sampleProvider), null);
        }

        return currentOutput;
    }

    private static WaveOutEvent StartWaveOut(ISampleProvider sampleProvider)
    {
        var waveOut = new WaveOutEvent
        {
            DesiredLatency = CoreSettings.WaveOutLatency
        };
        waveOut.Init(sampleProvider);
        waveOut.Play();
        return waveOut;
    }

    private static bool TryStartAsioOut(ISampleProvider sampleProvider, out AsioOut asioOut)
    {
        asioOut = null;

        string[] drivers;
        try
        {
            drivers = AsioOut.GetDriverNames();
        }
        catch (Exception ex)
        {
            ShowAsioUnavailableWarning($"Could not query installed ASIO drivers:\n{ex.Message}");
            return false;
        }

        var driverName = SelectAsioDriver(drivers, SelectedAsioDriverName);
        if (driverName is null)
        {
            ShowAsioUnavailableWarning("No ASIO drivers are installed.");
            return false;
        }

        if (AudioOutputStartup.TryStart(
                () => new AsioOut(driverName),
                output => output.Init(sampleProvider),
                output => output.Play(),
                output => output.Dispose(),
                out asioOut,
                out var error))
        {
            SelectedAsioDriverName = driverName;
            return true;
        }

        ShowAsioUnavailableWarning(
            $"Could not start ASIO driver \"{driverName}\":\n{error!.Message}\n\n" +
            "Make sure your audio interface is connected and powered on.");
        return false;
    }

    private static void ShowAsioUnavailableWarning(string message)
    {
        User32.MessageBox(IntPtr.Zero,
            $"{message}\n\nAudio will use WaveOut for this session.",
            "ASIO unavailable", User32.MB_FLAGS.MB_ICONWARNING | User32.MB_FLAGS.MB_TOPMOST);
    }
}
