using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Openthesia.Core.Accessibility;

public enum AccessibilityRole
{
    Window,
    Group,
    Button,
    CheckBox,
    ComboBox,
    List,
    ListItem,
    Edit,
    Slider,
    Text,
    Status
}

[Flags]
public enum AccessibilityAction
{
    None = 0,
    Invoke = 1 << 0,
    Toggle = 1 << 1,
    Select = 1 << 2,
    SetValue = 1 << 3,
    Increment = 1 << 4,
    Decrement = 1 << 5,
    Expand = 1 << 6,
    Collapse = 1 << 7,
    Focus = 1 << 8
}

public enum AccessibilityToggleState
{
    Off,
    On,
    Indeterminate
}

public enum AccessibilityLiveSetting
{
    Off,
    Polite,
    Assertive
}

public readonly record struct AccessibilityBounds(
    double X,
    double Y,
    double Width,
    double Height)
{
    public static AccessibilityBounds Empty { get; } = new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(double x, double y)
    {
        return !IsEmpty &&
               x >= X &&
               x <= X + Width &&
               y >= Y &&
               y <= Y + Height;
    }
}

public sealed record AccessibilityNode(
    string Id,
    string? ParentId,
    AccessibilityRole Role,
    string Name)
{
    public string? Description { get; init; }
    public string? Value { get; init; }
    public AccessibilityBounds Bounds { get; init; }
    public AccessibilityAction SupportedActions { get; init; }
    public AccessibilityToggleState ToggleState { get; init; }
    public AccessibilityLiveSetting LiveSetting { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsFocusable { get; init; }
    public bool IsFocused { get; init; }
    public bool IsSelected { get; init; }
    public bool IsOffscreen { get; init; }
    public bool IsExpanded { get; init; }
    public double? NumericValue { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public double? SmallChange { get; init; }
}

public sealed class AccessibilitySnapshot
{
    private readonly IReadOnlyDictionary<string, AccessibilityNode> _byId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AccessibilityNode>> _children;

    public AccessibilitySnapshot(
        AccessibilityNode root,
        IEnumerable<AccessibilityNode> descendants)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(descendants);
        if (root.ParentId is not null)
            throw new ArgumentException("The accessibility root cannot have a parent.", nameof(root));

        var nodes = new[] { root }.Concat(descendants).ToArray();
        if (nodes.Any(node => string.IsNullOrWhiteSpace(node.Id)))
            throw new ArgumentException("Every accessibility node needs a stable identifier.", nameof(descendants));
        if (nodes.Any(node => string.IsNullOrWhiteSpace(node.Name)))
            throw new ArgumentException("Every accessibility node needs a semantic name.", nameof(descendants));

        var byId = new Dictionary<string, AccessibilityNode>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!byId.TryAdd(node.Id, node))
                throw new ArgumentException($"Duplicate accessibility identifier '{node.Id}'.", nameof(descendants));
        }

        foreach (var node in nodes.Skip(1))
        {
            if (node.ParentId is null || !byId.ContainsKey(node.ParentId))
            {
                throw new ArgumentException(
                    $"Accessibility node '{node.Id}' must name an existing parent.",
                    nameof(descendants));
            }
        }

        var children = nodes
            .Where(node => node.ParentId is not null)
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AccessibilityNode>)group.ToArray(),
                StringComparer.Ordinal);
        Root = root;
        Nodes = nodes;
        _byId = new ReadOnlyDictionary<string, AccessibilityNode>(byId);
        _children = new ReadOnlyDictionary<string, IReadOnlyList<AccessibilityNode>>(children);
    }

    public AccessibilityNode Root { get; }
    public IReadOnlyList<AccessibilityNode> Nodes { get; }

    public AccessibilityNode GetRequired(string id)
    {
        return _byId.TryGetValue(id, out var node)
            ? node
            : throw new KeyNotFoundException($"Accessibility node '{id}' is not in the current frame.");
    }

    public bool TryGet(string id, out AccessibilityNode? node)
    {
        return _byId.TryGetValue(id, out node);
    }

    public IReadOnlyList<AccessibilityNode> GetChildren(string id)
    {
        return _children.TryGetValue(id, out var children)
            ? children
            : Array.Empty<AccessibilityNode>();
    }
}

public sealed record AccessibilityActionRequest(
    string NodeId,
    AccessibilityAction Action,
    string? Value);

public enum AccessibilityEventKind
{
    StructureChanged,
    PropertyChanged,
    FocusChanged,
    LiveRegionChanged,
    ElementSelected
}

public sealed record AccessibilityEvent(
    string NodeId,
    AccessibilityEventKind Kind,
    string? PropertyName = null,
    object? OldValue = null,
    object? NewValue = null);

public sealed record AccessibilityTreeUpdate(
    IReadOnlyList<AccessibilityEvent> Events);

public sealed class AccessibilityTree
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _stableIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _lastAnnouncedValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimeSpan> _lastLiveEventsAt = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<AccessibilityActionRequest> _actions = new();
    private readonly TimeSpan _liveRegionThrottle;
    private int _nextStableId = 1;
    private AccessibilitySnapshot? _current;

    public AccessibilityTree(TimeSpan? liveRegionThrottle = null)
    {
        _liveRegionThrottle = liveRegionThrottle ?? TimeSpan.FromMilliseconds(750);
        if (_liveRegionThrottle < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(liveRegionThrottle));
    }

    public AccessibilitySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current ?? throw new InvalidOperationException(
                    "The accessibility tree has not received its first frame.");
            }
        }
    }

    public AccessibilityTreeUpdate Update(AccessibilitySnapshot snapshot, TimeSpan timestamp)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (timestamp < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timestamp));

        lock (_gate)
        {
            var events = new List<AccessibilityEvent>();
            foreach (var node in snapshot.Nodes)
            {
                if (!_stableIds.ContainsKey(node.Id))
                    _stableIds.Add(node.Id, _nextStableId++);
            }

            if (_current is null)
            {
                foreach (var liveNode in snapshot.Nodes.Where(
                             node => node.LiveSetting != AccessibilityLiveSetting.Off))
                {
                    _lastAnnouncedValues[liveNode.Id] = liveNode.Value;
                }
            }
            else
            {
                var liveEvents = CollectLiveRegionEvents(snapshot, timestamp);
                var announcedLiveNodes = liveEvents
                    .Select(change => change.NodeId)
                    .ToHashSet(StringComparer.Ordinal);
                AddStructuralEvent(_current, snapshot, events);
                AddPropertyEvents(
                    _current,
                    snapshot,
                    announcedLiveNodes,
                    events);
                AddFocusEvent(_current, snapshot, events);
                foreach (var liveEvent in liveEvents)
                    events.Add(liveEvent);
            }

            _current = snapshot;
            return new AccessibilityTreeUpdate(events);
        }
    }

    public int GetStableId(string nodeId)
    {
        RequireNodeId(nodeId);
        lock (_gate)
        {
            return _stableIds.TryGetValue(nodeId, out var stableId)
                ? stableId
                : throw new KeyNotFoundException(
                    $"Accessibility node '{nodeId}' has not appeared in a frame.");
        }
    }

    public void RequestAction(
        string nodeId,
        AccessibilityAction action,
        string? value = null)
    {
        RequireNodeId(nodeId);
        if (action == AccessibilityAction.None || !IsSingleAction(action))
            throw new ArgumentOutOfRangeException(nameof(action), "Request one accessibility action at a time.");

        lock (_gate)
        {
            var node = Current.GetRequired(nodeId);
            if (!node.IsEnabled)
                throw new InvalidOperationException($"Accessibility node '{nodeId}' is disabled.");
            if ((node.SupportedActions & action) == 0)
            {
                throw new InvalidOperationException(
                    $"Accessibility node '{nodeId}' does not support {action}.");
            }
        }

        _actions.Enqueue(new AccessibilityActionRequest(nodeId, action, value));
    }

    public bool TryTakeAction(out AccessibilityActionRequest? request)
    {
        return _actions.TryDequeue(out request);
    }

    public string? GetLastAnnouncedValue(string nodeId)
    {
        RequireNodeId(nodeId);
        lock (_gate)
        {
            return _lastAnnouncedValues.TryGetValue(nodeId, out var value)
                ? value
                : throw new KeyNotFoundException(
                    $"Accessibility live region '{nodeId}' has not appeared in a frame.");
        }
    }

    private static void AddStructuralEvent(
        AccessibilitySnapshot previous,
        AccessibilitySnapshot current,
        ICollection<AccessibilityEvent> events)
    {
        if (!previous.Nodes.Select(node => node.Id).SequenceEqual(
                current.Nodes.Select(node => node.Id),
                StringComparer.Ordinal))
        {
            events.Add(new AccessibilityEvent(
                current.Root.Id,
                AccessibilityEventKind.StructureChanged));
        }
    }

    private static void AddPropertyEvents(
        AccessibilitySnapshot previous,
        AccessibilitySnapshot current,
        IReadOnlySet<string> announcedLiveNodes,
        ICollection<AccessibilityEvent> events)
    {
        foreach (var node in current.Nodes)
        {
            if (!previous.TryGet(node.Id, out var oldNode) || oldNode is null)
                continue;

            if (oldNode.Name != node.Name)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.Name), oldNode.Name, node.Name));
            if (oldNode.Description != node.Description)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.Description), oldNode.Description, node.Description));
            if (oldNode.Value != node.Value &&
                (node.LiveSetting == AccessibilityLiveSetting.Off ||
                 announcedLiveNodes.Contains(node.Id)))
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.Value), oldNode.Value, node.Value));
            if (oldNode.ToggleState != node.ToggleState)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.ToggleState), oldNode.ToggleState, node.ToggleState));
            if (oldNode.LiveSetting != node.LiveSetting)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.LiveSetting), oldNode.LiveSetting, node.LiveSetting));
            if (oldNode.IsEnabled != node.IsEnabled)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.IsEnabled), oldNode.IsEnabled, node.IsEnabled));
            if (oldNode.IsSelected != node.IsSelected)
            {
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.IsSelected), oldNode.IsSelected, node.IsSelected));
                if (!oldNode.IsSelected && node.IsSelected)
                {
                    events.Add(new AccessibilityEvent(
                        node.Id,
                        AccessibilityEventKind.ElementSelected));
                }
            }
            if (oldNode.NumericValue != node.NumericValue)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.NumericValue), oldNode.NumericValue, node.NumericValue));
            if (oldNode.Minimum != node.Minimum)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.Minimum), oldNode.Minimum, node.Minimum));
            if (oldNode.Maximum != node.Maximum)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.Maximum), oldNode.Maximum, node.Maximum));
            if (oldNode.IsFocusable != node.IsFocusable)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.IsFocusable), oldNode.IsFocusable, node.IsFocusable));
            if (oldNode.IsOffscreen != node.IsOffscreen)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.IsOffscreen), oldNode.IsOffscreen, node.IsOffscreen));
            if (oldNode.IsExpanded != node.IsExpanded)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.IsExpanded), oldNode.IsExpanded, node.IsExpanded));
            if (oldNode.Bounds != node.Bounds)
                events.Add(new AccessibilityEvent(node.Id, AccessibilityEventKind.PropertyChanged, nameof(node.Bounds), oldNode.Bounds, node.Bounds));
        }
    }

    private static void AddFocusEvent(
        AccessibilitySnapshot previous,
        AccessibilitySnapshot current,
        ICollection<AccessibilityEvent> events)
    {
        var previousFocus = previous.Nodes.FirstOrDefault(node => node.IsFocused)?.Id;
        var currentFocus = current.Nodes.FirstOrDefault(node => node.IsFocused)?.Id;
        if (currentFocus != previousFocus && currentFocus is not null)
        {
            events.Add(new AccessibilityEvent(
                currentFocus,
                AccessibilityEventKind.FocusChanged));
        }
    }

    private IReadOnlyList<AccessibilityEvent> CollectLiveRegionEvents(
        AccessibilitySnapshot snapshot,
        TimeSpan timestamp)
    {
        var events = new List<AccessibilityEvent>();
        foreach (var node in snapshot.Nodes.Where(
                     node => node.LiveSetting != AccessibilityLiveSetting.Off))
        {
            if (!_lastAnnouncedValues.TryGetValue(node.Id, out var announcedValue))
            {
                _lastAnnouncedValues[node.Id] = node.Value;
                continue;
            }
            if (announcedValue == node.Value)
                continue;

            var canAnnounce = node.LiveSetting == AccessibilityLiveSetting.Assertive ||
                              !_lastLiveEventsAt.TryGetValue(node.Id, out var lastEventAt) ||
                              timestamp - lastEventAt >= _liveRegionThrottle;
            if (!canAnnounce)
                continue;

            _lastAnnouncedValues[node.Id] = node.Value;
            _lastLiveEventsAt[node.Id] = timestamp;
            events.Add(new AccessibilityEvent(
                node.Id,
                AccessibilityEventKind.LiveRegionChanged));
        }
        return events;
    }

    private static bool IsSingleAction(AccessibilityAction action)
    {
        var value = (int)action;
        return (value & (value - 1)) == 0;
    }

    private static void RequireNodeId(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("A stable accessibility node identifier is required.", nameof(nodeId));
    }
}
