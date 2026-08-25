using Openthesia.Core.Accessibility;
using Xunit;

namespace Openthesia.Tests.Core.Accessibility;

public sealed class AccessibilityTreeTests
{
    [Fact]
    public void StableIdSurvivesImmediateModeFramesWhileStateAndBoundsChange()
    {
        var tree = new AccessibilityTree();
        tree.Update(new AccessibilitySnapshot(
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
                    Value = "Paused",
                    Bounds = new AccessibilityBounds(20, 30, 120, 50),
                    SupportedActions = AccessibilityAction.Invoke
                }
            }), TimeSpan.Zero);
        var stableId = tree.GetStableId("practice.play");

        tree.Update(new AccessibilitySnapshot(
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
                    Value = "Playing",
                    Bounds = new AccessibilityBounds(40, 60, 180, 75),
                    SupportedActions = AccessibilityAction.Invoke
                }
            }), TimeSpan.FromMilliseconds(16));

        Assert.Equal(stableId, tree.GetStableId("practice.play"));
        Assert.Equal("Playing", tree.Current.GetRequired("practice.play").Value);
        Assert.Equal(
            new AccessibilityBounds(40, 60, 180, 75),
            tree.Current.GetRequired("practice.play").Bounds);

        tree.RequestAction("practice.play", AccessibilityAction.Invoke);

        Assert.True(tree.TryTakeAction(out var request));
        Assert.Equal(
            new AccessibilityActionRequest(
                "practice.play",
                AccessibilityAction.Invoke,
                Value: null),
            request);
    }

    [Fact]
    public void PracticeStatusCoalescesRapidPoliteChangesAndKeepsTheLatestValue()
    {
        var tree = new AccessibilityTree(TimeSpan.FromMilliseconds(750));
        tree.Update(StatusFrame("Waiting for input"), TimeSpan.Zero);

        var firstChange = tree.Update(
            StatusFrame("Current target: C4 · Left"),
            TimeSpan.FromMilliseconds(100));
        var floodedChange = tree.Update(
            StatusFrame("Current target: D4 · Left"),
            TimeSpan.FromMilliseconds(200));
        tree.Update(
            StatusFrame("Current target: E4 · Left"),
            TimeSpan.FromMilliseconds(500));
        var coalescedChange = tree.Update(
            StatusFrame("Current target: E4 · Left"),
            TimeSpan.FromMilliseconds(850));

        Assert.Contains(
            firstChange.Events,
            change => change == new AccessibilityEvent(
                "practice.status",
                AccessibilityEventKind.LiveRegionChanged));
        Assert.DoesNotContain(
            floodedChange.Events,
            change => change.Kind == AccessibilityEventKind.LiveRegionChanged);
        Assert.DoesNotContain(
            floodedChange.Events,
            change => change.Kind == AccessibilityEventKind.PropertyChanged &&
                      change.PropertyName == nameof(AccessibilityNode.Value));
        Assert.Contains(
            coalescedChange.Events,
            change => change == new AccessibilityEvent(
                "practice.status",
                AccessibilityEventKind.LiveRegionChanged));
        Assert.Equal(
            "Current target: E4 · Left",
            tree.GetLastAnnouncedValue("practice.status"));
    }

    [Fact]
    public void CoordinatorDispatchesActionsAndFocusByStableSemanticId()
    {
        var invoked = false;
        var coordinator = new AccessibilityCoordinator();
        coordinator.BeginFrame(
            "practice",
            "Practice",
            new AccessibilityBounds(0, 0, 1280, 720));
        coordinator.Register(
            new AccessibilityNode(
                "practice.play",
                "practice",
                AccessibilityRole.Button,
                "Play")
            {
                IsFocusable = true,
                SupportedActions = AccessibilityAction.Invoke |
                                   AccessibilityAction.Focus
            },
            _ => invoked = true);
        coordinator.EndFrame(TimeSpan.Zero);

        coordinator.Tree.RequestAction(
            "practice.play",
            AccessibilityAction.Invoke);
        coordinator.Tree.RequestAction(
            "practice.play",
            AccessibilityAction.Focus);
        var completed = new List<AccessibilityActionRequest>();
        coordinator.DispatchActions(completed.Add);

        Assert.True(invoked);
        Assert.Equal(
            new[]
            {
                new AccessibilityActionRequest(
                    "practice.play",
                    AccessibilityAction.Invoke,
                    Value: null)
            },
            completed);
        Assert.True(coordinator.ConsumeFocusRequest("practice.play"));
        Assert.False(coordinator.ConsumeFocusRequest("practice.play"));
    }

    [Fact]
    public void SelectionEventIsProducedOnlyAfterTheNewSelectionIsObservable()
    {
        var tree = new AccessibilityTree();
        tree.Update(SelectionFrame(recitalSelected: false), TimeSpan.Zero);

        var unchanged = tree.Update(
            SelectionFrame(recitalSelected: false),
            TimeSpan.FromMilliseconds(16));
        var changed = tree.Update(
            SelectionFrame(recitalSelected: true),
            TimeSpan.FromMilliseconds(32));

        Assert.DoesNotContain(
            unchanged.Events,
            change => change.Kind == AccessibilityEventKind.ElementSelected);
        Assert.Contains(
            changed.Events,
            change => change == new AccessibilityEvent(
                "practice.mode.recital",
                AccessibilityEventKind.ElementSelected));
        Assert.True(tree.Current.GetRequired("practice.mode.recital").IsSelected);
    }

    private static AccessibilitySnapshot StatusFrame(string value)
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
                    Value = value,
                    LiveSetting = AccessibilityLiveSetting.Polite
                }
            });
    }

    private static AccessibilitySnapshot SelectionFrame(bool recitalSelected)
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
                    "practice.mode.recital",
                    "practice",
                    AccessibilityRole.ListItem,
                    "Recital")
                {
                    IsSelected = recitalSelected,
                    SupportedActions = AccessibilityAction.Select
                }
            });
    }
}
