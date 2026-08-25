using Melanchall.DryWetMidi.Multimedia;
using Openthesia.Core;
using Openthesia.Core.Practice;

namespace Openthesia.Settings;

public static class DevicesManager
{
    public static InputDevice IDevice { get; private set; }
    public static OutputDevice ODevice { get; private set; }

    public static void SetInputDevice(int deviceIndex)
    {
        if (IDevice != null)
        {
            ReleaseInputDevice();
        }

        IDevice = InputDevice.GetByIndex(deviceIndex);
        IDevice.EventReceived += IOHandle.OnEventReceived;
        IDevice.StartEventsListening();
    }

    public static void SetInputDevice(string deviceName)
    {
        if (IDevice != null)
        {
            ReleaseInputDevice();
        }

        List<string> deviceNames = new();
        foreach (var iDevice in InputDevice.GetAll())
        {
            deviceNames.Add(iDevice.Name);
        }

        if (!deviceNames.Contains(deviceName))
            return;

        IDevice = InputDevice.GetByName(deviceName);
        if (IDevice != null)
        {
            IDevice.EventReceived += IOHandle.OnEventReceived;
            IDevice.StartEventsListening();
        }
    }

    public static void ReleaseInputDevice()
    {
        IDevice?.Dispose();
        IDevice = null;
    }

    public static void SetOutputDevice(int deviceIndex)
    {
        ReplaceOutputDevice(OutputDevice.GetByIndex(deviceIndex));
    }

    public static void SetOutputDevice(string deviceName)
    {
        List<string> deviceNames = new();
        foreach (var oDevice in OutputDevice.GetAll())
        {
            deviceNames.Add(oDevice.Name);
        }

        if (!deviceNames.Contains(deviceName))
        {
            ReplaceOutputDevice(null);
            return;
        }

        ReplaceOutputDevice(OutputDevice.GetByName(deviceName));
    }

    public static void ReleaseOutputDevice()
    {
        ReplaceOutputDevice(null);
    }

    private static void ReplaceOutputDevice(OutputDevice? nextOutputDevice)
    {
        MidiPracticeSession.ReconfigureLightedKeyboardOutput(
            () =>
            {
                var previousOutputDevice = ODevice;
                ODevice = null;
                try
                {
                    previousOutputDevice?.Dispose();
                    if (nextOutputDevice is null)
                        return;

                    nextOutputDevice.EventSent += IOHandle.OnEventSent;
                    nextOutputDevice.PrepareForEventsSending();
                    ODevice = nextOutputDevice;
                }
                catch
                {
                    nextOutputDevice?.Dispose();
                    throw;
                }
            },
            refreshAfterChange: nextOutputDevice is not null);
    }
}
