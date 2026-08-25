using Microsoft.Win32;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Openthesia.Settings;

public sealed record WindowsContrastPalette(
    Vector4 Window,
    Vector4 WindowText,
    Vector4 Highlight,
    Vector4 HighlightText,
    Vector4 Button,
    Vector4 ButtonText)
{
    public static WindowsContrastPalette Default { get; } = new(
        new Vector4(0.12f, 0.16f, 0.22f, 1f),
        Vector4.One,
        new Vector4(0.03f, 0.52f, 0.76f, 1f),
        Vector4.One,
        new Vector4(0.29f, 0.29f, 0.29f, 1f),
        Vector4.One);
}

public sealed record WindowsAccessibilitySnapshot(
    WindowsAccessibilityState State,
    WindowsContrastPalette ContrastPalette);

public static class WindowsAccessibilityAdapter
{
    private const uint SpiGetHighContrast = 0x0042;
    private const uint SpiGetUiEffects = 0x103E;
    private const uint SpiGetClientAreaAnimation = 0x1042;
    private const uint HighContrastOn = 0x00000001;
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    public static void EnablePerMonitorV2()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows versions fall back to the application manifest.
        }
    }

    public static WindowsAccessibilitySnapshot Capture(IntPtr windowHandle)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WindowsAccessibilitySnapshot(
                new WindowsAccessibilityState(1f, 1f, true, true, false),
                WindowsContrastPalette.Default);
        }

        var animationsEnabled = ReadBooleanPreference(SpiGetClientAreaAnimation, fallback: true);
        var advancedEffectsEnabled = ReadBooleanPreference(SpiGetUiEffects, fallback: true);
        var highContrastEnabled = ReadHighContrast();
        var dpi = windowHandle == IntPtr.Zero ? 96u : GetDpiForWindow(windowHandle);
        if (dpi == 0)
            dpi = 96;

        return new WindowsAccessibilitySnapshot(
            new WindowsAccessibilityState(
                DpiScale: dpi / 96f,
                TextScale: ReadTextScale(),
                animationsEnabled,
                advancedEffectsEnabled,
                highContrastEnabled),
            new WindowsContrastPalette(
                ReadSystemColor(5),
                ReadSystemColor(8),
                ReadSystemColor(13),
                ReadSystemColor(14),
                ReadSystemColor(15),
                ReadSystemColor(18)));
    }

    private static float ReadTextScale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
            return key?.GetValue("TextScaleFactor") is int percentage
                ? Math.Clamp(percentage / 100f, 1f, 2.25f)
                : 1f;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 1f;
        }
    }

    private static bool ReadBooleanPreference(uint action, bool fallback)
    {
        var enabled = fallback;
        return SystemParametersInfo(action, 0, ref enabled, 0) ? enabled : fallback;
    }

    private static bool ReadHighContrast()
    {
        var highContrast = new HighContrast
        {
            Size = (uint)Marshal.SizeOf<HighContrast>()
        };
        return SystemParametersInfo(SpiGetHighContrast, highContrast.Size, ref highContrast, 0) &&
               (highContrast.Flags & HighContrastOn) != 0;
    }

    private static Vector4 ReadSystemColor(int index)
    {
        var color = GetSysColor(index);
        return new Vector4(
            (color & 0xff) / 255f,
            ((color >> 8) & 0xff) / 255f,
            ((color >> 16) & 0xff) / 255f,
            1f);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HighContrast
    {
        public uint Size;
        public uint Flags;
        public IntPtr DefaultScheme;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        ref bool value,
        uint update);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        ref HighContrast value,
        uint update);

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int index);
}

public static class AccessibilityRuntime
{
    private static readonly long PollInterval = Math.Max(1, Stopwatch.Frequency / 4);
    private static AccessibilitySettingsStore? _store;
    private static WindowsAccessibilitySnapshot? _snapshot;
    private static long _nextCaptureAt;

    public static AccessibilitySettings Settings { get; private set; } = AccessibilitySettings.Default;
    public static AccessibilityPresentation Presentation { get; private set; } = AccessibilityPolicy.Resolve(
        AccessibilitySettings.Default,
        new WindowsAccessibilityState(1f, 1f, true, true, false));
    public static WindowsContrastPalette ContrastPalette { get; private set; } = WindowsContrastPalette.Default;
    public static string? Warning { get; private set; }

    public static void Initialize(string dataDirectory)
    {
        _store = new AccessibilitySettingsStore(dataDirectory);
        var loaded = _store.Load();
        Settings = loaded.Settings;
        Warning = loaded.Warning;
        _snapshot = null;
        _nextCaptureAt = 0;
    }

    public static bool Update(IntPtr windowHandle)
    {
        var now = Stopwatch.GetTimestamp();
        if (_snapshot is not null && now < _nextCaptureAt)
            return false;

        _nextCaptureAt = now + PollInterval;
        var snapshot = WindowsAccessibilityAdapter.Capture(windowHandle);
        if (snapshot == _snapshot)
            return false;

        _snapshot = snapshot;
        Presentation = AccessibilityPolicy.Resolve(Settings, snapshot.State);
        ContrastPalette = snapshot.ContrastPalette;
        return true;
    }

    public static void SetVisualEffects(VisualEffectsPreference preference)
    {
        Settings = Settings with { VisualEffects = preference };
        if (_snapshot is not null)
            Presentation = AccessibilityPolicy.Resolve(Settings, _snapshot.State);
    }

    public static AccessibilitySettingsSaveResult Save()
    {
        return _store?.Save(Settings) ?? new AccessibilitySettingsSaveResult(
            false,
            "Accessibility settings were not initialized.");
    }
}
