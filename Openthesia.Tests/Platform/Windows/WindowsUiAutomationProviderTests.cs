using Openthesia.Core.Accessibility;
using Openthesia.Platform.Windows;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using Xunit;

namespace Openthesia.Tests.Platform.Windows;

public sealed class WindowsUiAutomationProviderTests
{
    [Fact]
    public void AttachedProviderCanBeDisposedWithoutThrowing()
    {
        var tree = new AccessibilityTree();
        tree.Update(Frame("Paused"), TimeSpan.Zero);
        var windowHandle = CreateTestWindow();

        WindowsUiAutomationProvider? provider = null;
        try
        {
            provider = new WindowsUiAutomationProvider(windowHandle, tree);

            provider.Dispose();
        }
        finally
        {
            provider?.Dispose();
            DestroyWindow(windowHandle);
        }
    }

    [Fact]
    public void DestroyingAttachedWindowDoesNotThrow()
    {
        var tree = new AccessibilityTree();
        tree.Update(Frame("Paused"), TimeSpan.Zero);
        var windowHandle = CreateTestWindow();
        var provider = new WindowsUiAutomationProvider(windowHandle, tree);

        try
        {
            Assert.True(DestroyWindow(windowHandle));
        }
        finally
        {
            provider.Dispose();
            DestroyWindow(windowHandle);
        }
    }

    private static IntPtr CreateTestWindow()
    {
        var windowHandle = CreateWindowEx(
            0,
            "STATIC",
            "Openthesia UI Automation test",
            0,
            0,
            0,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, windowHandle);
        return windowHandle;
    }

    [Fact]
    public void ButtonMapsToNamedInvokeProviderAndQueuesItsDomainAction()
    {
        var tree = new AccessibilityTree();
        tree.Update(Frame("Paused"), TimeSpan.Zero);
        using var provider = new WindowsUiAutomationProvider(IntPtr.Zero, tree);
        var first = provider.GetProviderForTesting("practice.play");

        Assert.Equal(
            ControlType.Button.Id,
            first.GetPropertyValue(AutomationElementIdentifiers.ControlTypeProperty.Id));
        Assert.Equal(
            "Play",
            first.GetPropertyValue(AutomationElementIdentifiers.NameProperty.Id));
        Assert.Equal(
            "Paused",
            first.GetPropertyValue(AutomationElementIdentifiers.ItemStatusProperty.Id));

        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(
            first.GetPatternProvider(InvokePatternIdentifiers.Pattern.Id));
        invoke.Invoke();

        Assert.True(tree.TryTakeAction(out var request));
        Assert.Equal(
            new AccessibilityActionRequest(
                "practice.play",
                AccessibilityAction.Invoke,
                Value: null),
            request);

        tree.Update(Frame("Playing"), TimeSpan.FromMilliseconds(16));

        Assert.Same(first, provider.GetProviderForTesting("practice.play"));
        Assert.Equal(
            "Playing",
            first.GetPropertyValue(AutomationElementIdentifiers.ItemStatusProperty.Id));
    }

    [Fact]
    public void StatefulControlsExposeTheirUiaPatternsAndQueueTypedActions()
    {
        var tree = new AccessibilityTree();
        tree.Update(StatefulFrame(), TimeSpan.Zero);
        using var provider = new WindowsUiAutomationProvider(IntPtr.Zero, tree);

        Assert.Equal(
            ControlType.CheckBox.Id,
            provider.GetProviderForTesting("practice.metronome")
                .GetPropertyValue(AutomationElementIdentifiers.ControlTypeProperty.Id));
        var toggle = Assert.IsAssignableFrom<IToggleProvider>(
            provider.GetProviderForTesting("practice.metronome")
                .GetPatternProvider(TogglePatternIdentifiers.Pattern.Id));
        Assert.Equal(ToggleState.On, toggle.ToggleState);
        toggle.Toggle();

        Assert.Equal(
            ControlType.Edit.Id,
            provider.GetProviderForTesting("practice.loop.name")
                .GetPropertyValue(AutomationElementIdentifiers.ControlTypeProperty.Id));
        var value = Assert.IsAssignableFrom<IValueProvider>(
            provider.GetProviderForTesting("practice.loop.name")
                .GetPatternProvider(ValuePatternIdentifiers.Pattern.Id));
        Assert.Equal("Verse", value.Value);
        value.SetValue("Chorus");

        Assert.Equal(
            ControlType.Slider.Id,
            provider.GetProviderForTesting("practice.tempo")
                .GetPropertyValue(AutomationElementIdentifiers.ControlTypeProperty.Id));
        var range = Assert.IsAssignableFrom<IRangeValueProvider>(
            provider.GetProviderForTesting("practice.tempo")
                .GetPatternProvider(RangeValuePatternIdentifiers.Pattern.Id));
        Assert.Equal(1.25d, range.Value);
        Assert.Equal(0.25d, range.Minimum);
        Assert.Equal(2d, range.Maximum);
        range.SetValue(1.5d);

        Assert.Equal(
            ControlType.ComboBox.Id,
            provider.GetProviderForTesting("practice.mode")
                .GetPropertyValue(AutomationElementIdentifiers.ControlTypeProperty.Id));
        var selection = Assert.IsAssignableFrom<ISelectionProvider>(
            provider.GetProviderForTesting("practice.mode")
                .GetPatternProvider(SelectionPatternIdentifiers.Pattern.Id));
        Assert.False(selection.CanSelectMultiple);
        Assert.True(selection.IsSelectionRequired);
        Assert.Single(selection.GetSelection());

        var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            provider.GetProviderForTesting("practice.mode")
                .GetPatternProvider(ExpandCollapsePatternIdentifiers.Pattern.Id));
        Assert.Equal(ExpandCollapseState.Collapsed, expandCollapse.ExpandCollapseState);
        expandCollapse.Expand();

        Assert.Equal(
            ControlType.ListItem.Id,
            provider.GetProviderForTesting("practice.mode.recital")
                .GetPropertyValue(AutomationElementIdentifiers.ControlTypeProperty.Id));
        var item = Assert.IsAssignableFrom<ISelectionItemProvider>(
            provider.GetProviderForTesting("practice.mode.recital")
                .GetPatternProvider(SelectionItemPatternIdentifiers.Pattern.Id));
        Assert.False(item.IsSelected);
        Assert.Throws<InvalidOperationException>(item.AddToSelection);
        item.Select();

        Assert.Equal(
            new[]
            {
                new AccessibilityActionRequest(
                    "practice.metronome",
                    AccessibilityAction.Toggle,
                    Value: null),
                new AccessibilityActionRequest(
                    "practice.loop.name",
                    AccessibilityAction.SetValue,
                    "Chorus"),
                new AccessibilityActionRequest(
                    "practice.tempo",
                    AccessibilityAction.SetValue,
                    "1.5"),
                new AccessibilityActionRequest(
                    "practice.mode",
                    AccessibilityAction.Expand,
                    Value: null),
                new AccessibilityActionRequest(
                    "practice.mode.recital",
                    AccessibilityAction.Select,
                    Value: null)
            },
            TakeAllActions(tree));
    }

    [Fact]
    public void ProviderPublishesInvokeImmediatelyAndSelectionAfterStateChanges()
    {
        var coordinator = new AccessibilityCoordinator();
        var recitalSelected = false;
        AccessibilityTreeUpdate RenderFrame(TimeSpan timestamp)
        {
            coordinator.BeginFrame(
                "practice",
                "Practice",
                AccessibilityBounds.Empty);
            coordinator.Register(
                new AccessibilityNode(
                    "practice.play",
                    "practice",
                    AccessibilityRole.Button,
                    "Play")
                {
                    SupportedActions = AccessibilityAction.Invoke
                },
                _ => { });
            coordinator.Register(new AccessibilityNode(
                "practice.mode",
                "practice",
                AccessibilityRole.ComboBox,
                "Practice Mode"));
            coordinator.Register(
                new AccessibilityNode(
                    "practice.mode.recital",
                    "practice.mode",
                    AccessibilityRole.ListItem,
                    "Recital")
                {
                    IsSelected = recitalSelected,
                    SupportedActions = AccessibilityAction.Select
                },
                _ => recitalSelected = true);
            return coordinator.EndFrame(timestamp);
        }

        RenderFrame(TimeSpan.Zero);
        var sink = new RecordingEventSink();
        using var provider = new WindowsUiAutomationProvider(
            IntPtr.Zero,
            coordinator.Tree,
            sink);

        coordinator.Tree.RequestAction(
            "practice.play",
            AccessibilityAction.Invoke);
        coordinator.Tree.RequestAction(
            "practice.mode.recital",
            AccessibilityAction.Select);
        coordinator.DispatchActions(provider.NotifyActionCompleted);

        Assert.True(recitalSelected);
        Assert.Contains(
            sink.AutomationEvents,
            entry => entry.Event == InvokePatternIdentifiers.InvokedEvent);
        Assert.DoesNotContain(
            sink.AutomationEvents,
            entry => entry.Event == SelectionItemPatternIdentifiers.ElementSelectedEvent);

        var update = RenderFrame(TimeSpan.FromMilliseconds(16));
        provider.Publish(update);

        Assert.Contains(
            sink.AutomationEvents,
            entry => entry.Event == SelectionItemPatternIdentifiers.ElementSelectedEvent);
    }

    [Fact]
    public void ProviderKeepsFragmentIdentityAndReportsFocusAndValueChanges()
    {
        var tree = new AccessibilityTree();
        tree.Update(FocusFrame(isFocused: false, "Paused"), TimeSpan.Zero);
        var sink = new RecordingEventSink();
        using var provider = new WindowsUiAutomationProvider(IntPtr.Zero, tree, sink);
        var play = Assert.IsAssignableFrom<IRawElementProviderFragment>(
            provider.GetProviderForTesting("practice.play"));
        var runtimeId = play.GetRuntimeId();

        var update = tree.Update(
            FocusFrame(isFocused: true, "Playing", includeStatus: true),
            TimeSpan.FromMilliseconds(16));
        provider.Publish(update);

        Assert.Contains(
            update.Events,
            change => change == new AccessibilityEvent(
                "practice.play",
                AccessibilityEventKind.FocusChanged));
        Assert.Contains(
            update.Events,
            change => change == new AccessibilityEvent(
                "practice",
                AccessibilityEventKind.StructureChanged));
        Assert.Contains(
            update.Events,
            change => change == new AccessibilityEvent(
                "practice.play",
                AccessibilityEventKind.PropertyChanged,
                nameof(AccessibilityNode.Value),
                "Paused",
                "Playing"));
        Assert.Same(play, provider.GetProviderForTesting("practice.play"));
        Assert.Equal(runtimeId, play.GetRuntimeId());
        Assert.Same(provider.Root, play.Navigate(NavigateDirection.Parent));
        Assert.Same(play, provider.Root.GetFocus());
        Assert.Equal(new System.Windows.Rect(20, 30, 120, 50), play.BoundingRectangle);
        Assert.Contains(
            sink.AutomationEvents,
            entry => entry.Event == AutomationElementIdentifiers.AutomationFocusChangedEvent);
        Assert.Single(sink.StructureEvents);
        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == AutomationElementIdentifiers.ItemStatusProperty &&
                     Equals(entry.Args.OldValue, "Paused") &&
                     Equals(entry.Args.NewValue, "Playing"));
    }

    [Fact]
    public void RemovedElementProviderReportsElementNotAvailable()
    {
        var tree = new AccessibilityTree();
        tree.Update(StatefulFrame(), TimeSpan.Zero);
        using var provider = new WindowsUiAutomationProvider(IntPtr.Zero, tree);
        var play = Assert.IsAssignableFrom<IRawElementProviderFragment>(
            provider.GetProviderForTesting("practice.play"));
        var recital = Assert.IsAssignableFrom<ISelectionItemProvider>(
            provider.GetProviderForTesting("practice.mode.recital")
                .GetPatternProvider(SelectionItemPatternIdentifiers.Pattern.Id));

        tree.Update(
            new AccessibilitySnapshot(
                new AccessibilityNode(
                    "practice",
                    ParentId: null,
                    AccessibilityRole.Window,
                    "Practice"),
                Array.Empty<AccessibilityNode>()),
            TimeSpan.FromMilliseconds(16));

        Assert.Throws<ElementNotAvailableException>(() =>
            play.GetPropertyValue(AutomationElementIdentifiers.NameProperty.Id));
        Assert.Throws<ElementNotAvailableException>(() => play.Navigate(
            NavigateDirection.Parent));
        Assert.Throws<ElementNotAvailableException>(
            recital.RemoveFromSelection);
    }

    [Fact]
    public void ProviderPublishesLiveSettingChangesWithoutAValueChange()
    {
        var tree = new AccessibilityTree();
        tree.Update(
            LiveStatusFrame(AccessibilityLiveSetting.Polite),
            TimeSpan.Zero);
        var sink = new RecordingEventSink();
        using var provider = new WindowsUiAutomationProvider(
            IntPtr.Zero,
            tree,
            sink);

        var update = tree.Update(
            LiveStatusFrame(AccessibilityLiveSetting.Assertive),
            TimeSpan.FromMilliseconds(16));
        provider.Publish(update);

        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == AutomationElementIdentifiers.LiveSettingProperty &&
                     Equals(entry.Args.OldValue, AutomationLiveSetting.Polite) &&
                     Equals(entry.Args.NewValue, AutomationLiveSetting.Assertive));

        sink.PropertyEvents.Clear();
        update = tree.Update(
            LiveStatusFrame(AccessibilityLiveSetting.Polite),
            TimeSpan.FromMilliseconds(32));
        provider.Publish(update);

        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == AutomationElementIdentifiers.LiveSettingProperty &&
                     Equals(entry.Args.OldValue, AutomationLiveSetting.Assertive) &&
                     Equals(entry.Args.NewValue, AutomationLiveSetting.Polite));
    }

    [Fact]
    public void ProviderQueriesUseConsistentSnapshotsDuringConcurrentPublication()
    {
        var tree = new AccessibilityTree();
        tree.Update(Frame("Paused"), TimeSpan.Zero);
        using var provider = new WindowsUiAutomationProvider(IntPtr.Zero, tree);
        var play = Assert.IsAssignableFrom<IRawElementProviderFragment>(
            provider.GetProviderForTesting("practice.play"));
        var unexpected = new ConcurrentQueue<Exception>();

        Parallel.Invoke(
            () =>
            {
                for (var index = 1; index <= 250; index++)
                {
                    tree.Update(
                        index % 2 == 0
                            ? Frame("Playing")
                            : new AccessibilitySnapshot(
                                new AccessibilityNode(
                                    "practice",
                                    ParentId: null,
                                    AccessibilityRole.Window,
                                    "Practice"),
                                Array.Empty<AccessibilityNode>()),
                        TimeSpan.FromMilliseconds(index));
                }
            },
            () =>
            {
                for (var index = 0; index < 250; index++)
                {
                    try
                    {
                        _ = play.GetPropertyValue(
                            AutomationElementIdentifiers.NameProperty.Id);
                        _ = play.Navigate(NavigateDirection.Parent);
                    }
                    catch (ElementNotAvailableException)
                    {
                        // The element can legitimately be absent in that published frame.
                    }
                    catch (Exception exception)
                    {
                        unexpected.Enqueue(exception);
                    }
                }
            });

        Assert.Empty(unexpected);
    }

    [Fact]
    public void ProviderPublishesPatternAndLayoutSpecificPropertyChanges()
    {
        var tree = new AccessibilityTree();
        tree.Update(StatefulFrame(), TimeSpan.Zero);
        var sink = new RecordingEventSink();
        using var provider = new WindowsUiAutomationProvider(IntPtr.Zero, tree, sink);

        var update = tree.Update(
            StatefulFrame(changed: true),
            TimeSpan.FromMilliseconds(16));
        provider.Publish(update);

        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == ValuePatternIdentifiers.ValueProperty &&
                     Equals(entry.Args.OldValue, "Verse") &&
                     Equals(entry.Args.NewValue, "Chorus"));
        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == RangeValuePatternIdentifiers.ValueProperty &&
                     Equals(entry.Args.OldValue, 1.25d) &&
                     Equals(entry.Args.NewValue, 1.5d));
        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty &&
                     Equals(entry.Args.OldValue, ExpandCollapseState.Collapsed) &&
                     Equals(entry.Args.NewValue, ExpandCollapseState.Expanded));
        Assert.Contains(
            sink.PropertyEvents,
            entry => entry.Args.Property == AutomationElementIdentifiers.BoundingRectangleProperty &&
                     entry.Args.OldValue is double[] oldBounds &&
                     oldBounds.SequenceEqual(new double[] { 0, 0, 0, 0 }) &&
                     entry.Args.NewValue is double[] newBounds &&
                     newBounds.SequenceEqual(new double[] { 10, 20, 240, 40 }));
    }

    private static AccessibilitySnapshot Frame(string value)
    {
        return new AccessibilitySnapshot(
            new AccessibilityNode(
                "practice",
                ParentId: null,
                AccessibilityRole.Window,
                "Practice"),
            new[]
            {
                new AccessibilityNode(
                    "practice.play",
                    "practice",
                    AccessibilityRole.Button,
                    "Play")
                {
                    Value = value,
                    IsEnabled = true,
                    IsFocusable = true,
                    SupportedActions = AccessibilityAction.Invoke |
                                       AccessibilityAction.Focus
                }
            });
    }

    private static AccessibilitySnapshot StatefulFrame(bool changed = false)
    {
        return new AccessibilitySnapshot(
            new AccessibilityNode(
                "practice",
                ParentId: null,
                AccessibilityRole.Window,
                "Practice"),
            new[]
            {
                new AccessibilityNode(
                    "practice.play",
                    "practice",
                    AccessibilityRole.Button,
                    "Play")
                {
                    SupportedActions = AccessibilityAction.Invoke
                },
                new AccessibilityNode(
                    "practice.metronome",
                    "practice",
                    AccessibilityRole.CheckBox,
                    "Metronome")
                {
                    ToggleState = changed
                        ? AccessibilityToggleState.Off
                        : AccessibilityToggleState.On,
                    SupportedActions = AccessibilityAction.Toggle
                },
                new AccessibilityNode(
                    "practice.loop.name",
                    "practice",
                    AccessibilityRole.Edit,
                    "Loop name")
                {
                    Value = changed ? "Chorus" : "Verse",
                    Bounds = changed
                        ? new AccessibilityBounds(10, 20, 240, 40)
                        : AccessibilityBounds.Empty,
                    SupportedActions = AccessibilityAction.SetValue
                },
                new AccessibilityNode(
                    "practice.tempo",
                    "practice",
                    AccessibilityRole.Slider,
                    "Tempo")
                {
                    Value = changed ? "1.5x" : "1.25x",
                    NumericValue = changed ? 1.5d : 1.25d,
                    Minimum = 0.25d,
                    Maximum = 2d,
                    SmallChange = 0.25d,
                    SupportedActions = AccessibilityAction.SetValue |
                                       AccessibilityAction.Increment |
                                       AccessibilityAction.Decrement
                },
                new AccessibilityNode(
                    "practice.mode",
                    "practice",
                    AccessibilityRole.ComboBox,
                    "Practice Mode")
                {
                    Value = changed ? "Recital" : "Wait for Notes",
                    IsExpanded = changed,
                    SupportedActions = AccessibilityAction.Expand |
                                       AccessibilityAction.Collapse |
                                       AccessibilityAction.Focus
                },
                new AccessibilityNode(
                    "practice.mode.wait",
                    "practice.mode",
                    AccessibilityRole.ListItem,
                    "Wait for Notes")
                {
                    IsSelected = !changed,
                    SupportedActions = AccessibilityAction.Select
                },
                new AccessibilityNode(
                    "practice.mode.recital",
                    "practice.mode",
                    AccessibilityRole.ListItem,
                    "Recital")
                {
                    IsSelected = changed,
                    SupportedActions = AccessibilityAction.Select
                }
            });
    }

    private static AccessibilitySnapshot FocusFrame(
        bool isFocused,
        string value,
        bool includeStatus = false)
    {
        var nodes = new List<AccessibilityNode>
        {
            new(
                "practice.play",
                "practice",
                AccessibilityRole.Button,
                "Play")
            {
                Value = value,
                Bounds = new AccessibilityBounds(20, 30, 120, 50),
                IsFocusable = true,
                IsFocused = isFocused,
                SupportedActions = AccessibilityAction.Invoke |
                                   AccessibilityAction.Focus
            }
        };
        if (includeStatus)
        {
            nodes.Add(new AccessibilityNode(
                "practice.status",
                "practice",
                AccessibilityRole.Status,
                "Practice Status")
            {
                Value = "Playing"
            });
        }

        return new AccessibilitySnapshot(
            new AccessibilityNode(
                "practice",
                ParentId: null,
                AccessibilityRole.Window,
                "Practice"),
            nodes);
    }

    private static AccessibilitySnapshot LiveStatusFrame(
        AccessibilityLiveSetting liveSetting)
    {
        return new AccessibilitySnapshot(
            new AccessibilityNode(
                "practice",
                ParentId: null,
                AccessibilityRole.Window,
                "Practice"),
            new[]
            {
                new AccessibilityNode(
                    "practice.status",
                    "practice",
                    AccessibilityRole.Status,
                    "Practice Status")
                {
                    Value = "Practice complete",
                    LiveSetting = liveSetting
                }
            });
    }

    private static IReadOnlyList<AccessibilityActionRequest> TakeAllActions(
        AccessibilityTree tree)
    {
        var actions = new List<AccessibilityActionRequest>();
        while (tree.TryTakeAction(out var action))
            actions.Add(action!);
        return actions;
    }

    private sealed class RecordingEventSink : WindowsUiAutomationProvider.IAutomationEventSink
    {
        public bool ClientsAreListening => true;
        public List<(AutomationEvent Event, AutomationEventArgs Args)> AutomationEvents { get; } = new();
        public List<(IRawElementProviderSimple Provider, AutomationPropertyChangedEventArgs Args)> PropertyEvents { get; } = new();
        public List<(IRawElementProviderSimple Provider, StructureChangedEventArgs Args)> StructureEvents { get; } = new();

        public void RaiseAutomationEvent(
            AutomationEvent automationEvent,
            IRawElementProviderSimple provider,
            AutomationEventArgs eventArgs)
        {
            AutomationEvents.Add((automationEvent, eventArgs));
        }

        public void RaiseAutomationPropertyChangedEvent(
            IRawElementProviderSimple provider,
            AutomationPropertyChangedEventArgs eventArgs)
        {
            PropertyEvents.Add((provider, eventArgs));
        }

        public void RaiseStructureChangedEvent(
            IRawElementProviderSimple provider,
            StructureChangedEventArgs eventArgs)
        {
            StructureEvents.Add((provider, eventArgs));
        }
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parentWindow,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr windowHandle);
}
