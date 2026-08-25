using Melanchall.DryWetMidi.Multimedia;
using Openthesia.Core;
using Openthesia.Core.Practice;

namespace Openthesia.Settings;

public static class DevicesManager
{
    private const long DeviceCatalogRefreshMilliseconds = 1000;
    private static readonly DisposableDeviceCatalog<InputDevice> InputCatalog = new(
        () => InputDevice.GetAll().ToArray(),
        device => device.Name);
    private static readonly DisposableDeviceCatalog<OutputDevice> OutputCatalog = new(
        () => OutputDevice.GetAll().ToArray(),
        device => device.Name);
    private static readonly object InputCatalogGate = new();
    private static readonly object OutputCatalogGate = new();
    private static IReadOnlyList<MidiDeviceDescriptor> _inputDescriptors =
        Array.Empty<MidiDeviceDescriptor>();
    private static IReadOnlyList<MidiDeviceDescriptor> _outputDescriptors =
        Array.Empty<MidiDeviceDescriptor>();
    private static long _inputDescriptorsAt;
    private static long _outputDescriptorsAt;

    public static InputDevice? IDevice { get; private set; }
    public static OutputDevice? ODevice { get; private set; }
    public static string? ActiveInputDeviceToken { get; private set; }
    public static string? ActiveOutputDeviceToken { get; private set; }
    public static string? ActiveInputDeviceName { get; private set; }
    public static string? ActiveOutputDeviceName { get; private set; }

    internal static IReadOnlyList<MidiDeviceDescriptor> GetInputDeviceDescriptors()
    {
        lock (InputCatalogGate)
        {
            var now = Environment.TickCount64;
            if (_inputDescriptorsAt == 0 ||
                now - _inputDescriptorsAt >= DeviceCatalogRefreshMilliseconds)
            {
                _inputDescriptors = InputCatalog.Describe();
                _inputDescriptorsAt = now;
            }
            return _inputDescriptors;
        }
    }

    internal static IReadOnlyList<MidiDeviceDescriptor> GetOutputDeviceDescriptors()
    {
        lock (OutputCatalogGate)
        {
            var now = Environment.TickCount64;
            if (_outputDescriptorsAt == 0 ||
                now - _outputDescriptorsAt >= DeviceCatalogRefreshMilliseconds)
            {
                _outputDescriptors = OutputCatalog.Describe();
                _outputDescriptorsAt = now;
            }
            return _outputDescriptors;
        }
    }

    public static void SetInputDevice(int deviceIndex)
    {
        var devices = GetInputDeviceDescriptors();
        if (deviceIndex < 0 || deviceIndex >= devices.Count)
            throw new ArgumentOutOfRangeException(nameof(deviceIndex));
        if (!TrySetInputDevice(devices[deviceIndex].Token))
            ReleaseInputDevice();
    }

    public static void SetInputDevice(string deviceName)
    {
        var descriptor = GetInputDeviceDescriptors()
            .FirstOrDefault(device => device.Name == deviceName);
        if (descriptor is null || !TrySetInputDevice(descriptor.Token))
            ReleaseInputDevice();
    }

    public static bool TrySetInputDevice(string deviceToken)
    {
        var nextInputDevice = InputCatalog.Take(deviceToken);
        if (nextInputDevice is null)
            return false;

        ReleaseInputDevice();
        try
        {
            nextInputDevice.EventReceived += IOHandle.OnEventReceived;
            nextInputDevice.StartEventsListening();
            IDevice = nextInputDevice;
            ActiveInputDeviceToken = deviceToken;
            ActiveInputDeviceName = nextInputDevice.Name;
            return true;
        }
        catch
        {
            nextInputDevice.Dispose();
            throw;
        }
    }

    public static void ReleaseInputDevice()
    {
        IDevice?.Dispose();
        IDevice = null;
        ActiveInputDeviceToken = null;
        ActiveInputDeviceName = null;
    }

    public static void SetOutputDevice(int deviceIndex)
    {
        var devices = GetOutputDeviceDescriptors();
        if (deviceIndex < 0 || deviceIndex >= devices.Count)
            throw new ArgumentOutOfRangeException(nameof(deviceIndex));
        if (!TrySetOutputDevice(devices[deviceIndex].Token))
            ReleaseOutputDevice();
    }

    public static void SetOutputDevice(string deviceName)
    {
        var descriptor = GetOutputDeviceDescriptors()
            .FirstOrDefault(device => device.Name == deviceName);
        if (descriptor is null || !TrySetOutputDevice(descriptor.Token))
            ReleaseOutputDevice();
    }

    public static bool TrySetOutputDevice(string deviceToken)
    {
        var nextOutputDevice = OutputCatalog.Take(deviceToken);
        if (nextOutputDevice is null)
            return false;

        ReplaceOutputDevice(nextOutputDevice, deviceToken, nextOutputDevice.Name);
        return true;
    }

    public static void ReleaseOutputDevice()
    {
        ReplaceOutputDevice(null);
    }

    private static void ReplaceOutputDevice(
        OutputDevice? nextOutputDevice,
        string? nextOutputDeviceToken = null,
        string? nextOutputDeviceName = null)
    {
        MidiPracticeSession.ReconfigureLightedKeyboardOutput(
            () =>
            {
                var previousOutputDevice = ODevice;
                ODevice = null;
                ActiveOutputDeviceToken = null;
                ActiveOutputDeviceName = null;
                try
                {
                    previousOutputDevice?.Dispose();
                    if (nextOutputDevice is null)
                        return;

                    nextOutputDevice.EventSent += IOHandle.OnEventSent;
                    nextOutputDevice.PrepareForEventsSending();
                    ODevice = nextOutputDevice;
                    ActiveOutputDeviceToken = nextOutputDeviceToken;
                    ActiveOutputDeviceName = nextOutputDeviceName;
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
