using NAudio.Wave;
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
        if (drivers.Length > 0)
        {
            // on startup: if last device is still present select it
            if (drivers.Contains(deviceName))
            {
                SelectedAsioDriverName = deviceName;
            }
            // else select the first available
            else
                SelectedAsioDriverName = drivers[0];
        }
    }

    public static bool TryCreateAsioOut(out AsioOut asioOut)
    {
        asioOut = null;
        var drivers = AsioOut.GetDriverNames();
        if (drivers.Length == 0)
        {
            User32.MessageBox(IntPtr.Zero,
                "No ASIO drivers are installed.\n\nAudio will use WaveOut for this session.",
                "ASIO unavailable", User32.MB_FLAGS.MB_ICONWARNING | User32.MB_FLAGS.MB_TOPMOST);
            return false;
        }

        var driverName = drivers.Contains(SelectedAsioDriverName)
            ? SelectedAsioDriverName
            : drivers[0];

        try
        {
            asioOut = new AsioOut(driverName);
            SelectedAsioDriverName = driverName;
            return true;
        }
        catch (Exception ex)
        {
            User32.MessageBox(IntPtr.Zero,
                $"Could not open ASIO driver \"{driverName}\":\n{ex.Message}\n\n" +
                "Make sure your audio interface is connected and powered on.\n" +
                "Audio will use WaveOut for this session.",
                "ASIO unavailable", User32.MB_FLAGS.MB_ICONWARNING | User32.MB_FLAGS.MB_TOPMOST);
            return false;
        }
    }
}
