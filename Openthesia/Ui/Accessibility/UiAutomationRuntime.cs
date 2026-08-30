using Openthesia.Core.Accessibility;
using Openthesia.Core.Practice;
using Openthesia.Platform.Windows;
using System.Diagnostics;
using System.Numerics;

namespace Openthesia.Ui.Accessibility;

public static class UiAutomationRuntime
{
    private static readonly AccessibilityCoordinator CoordinatorInstance = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static WindowsUiAutomationProvider? _windowsProvider;

    public static AccessibilityCoordinator Coordinator => CoordinatorInstance;
    public static string CurrentScreenId => Coordinator.CurrentScreenId ??
        throw new InvalidOperationException("No UI Automation frame is active.");

    public static void Initialize(IntPtr windowHandle)
    {
        _windowsProvider?.Dispose();
        _windowsProvider = new WindowsUiAutomationProvider(windowHandle, Coordinator.Tree);
    }

    public static void DispatchActions()
    {
        Coordinator.DispatchActions(
            request => _windowsProvider?.NotifyActionCompleted(request));
    }

    public static void NotifyActionCompleted(
        string nodeId,
        AccessibilityAction action)
    {
        _windowsProvider?.NotifyActionCompleted(
            new AccessibilityActionRequest(nodeId, action, Value: null));
    }

    public static void BeginFrame(string windowId, Vector2 displaySize)
    {
        var (screenId, screenName) = ScreenIdentity(windowId);
        Coordinator.BeginFrame(
            screenId,
            screenName,
            new AccessibilityBounds(0, 0, displaySize.X, displaySize.Y));
    }

    public static void EndFrame()
    {
        var update = Coordinator.EndFrame(Clock.Elapsed);
        _windowsProvider?.Publish(update);
    }

    public static void Shutdown()
    {
        _windowsProvider?.Dispose();
        _windowsProvider = null;
    }

    private static (string Id, string Name) ScreenIdentity(string windowId)
    {
        return windowId switch
        {
            nameof(Enums.Windows.Home) => ("home", "Home"),
            nameof(Enums.Windows.MidiBrowser) => ("midi-source-browser", "MIDI Source selection"),
            nameof(Enums.Windows.ModeSelection) => ("practice-setup", "Practice setup"),
            nameof(Enums.Windows.MidiPlayback) when MidiPracticeSession.IsActive =>
                ("practice", "Practice"),
            nameof(Enums.Windows.MidiPlayback) =>
                ("performance-visualization", "Performance Visualization"),
            nameof(Enums.Windows.PlayMode) => ("play-mode", "Play Mode"),
            nameof(Enums.Windows.Settings) => ("device-settings", "Device Settings"),
            _ => ($"screen.{windowId}", windowId)
        };
    }
}
