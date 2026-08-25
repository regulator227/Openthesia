namespace Openthesia.Core.Accessibility;

public sealed class AccessibilityCoordinator
{
    public const string ApplicationRootId = "application";

    private readonly Dictionary<string, Action<AccessibilityActionRequest>> _handlers =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _focusRequests = new(StringComparer.Ordinal);
    private List<AccessibilityNode>? _frameNodes;
    private Dictionary<string, Action<AccessibilityActionRequest>>? _frameHandlers;
    private AccessibilityBounds _frameBounds;

    public AccessibilityCoordinator()
    {
        Tree = new AccessibilityTree();
        Tree.Update(
            new AccessibilitySnapshot(
                ApplicationRoot(AccessibilityBounds.Empty),
                Array.Empty<AccessibilityNode>()),
            TimeSpan.Zero);
    }

    public AccessibilityTree Tree { get; }
    public string? CurrentScreenId { get; private set; }

    public void BeginFrame(
        string screenId,
        string screenName,
        AccessibilityBounds bounds)
    {
        if (_frameNodes is not null)
            throw new InvalidOperationException("The previous accessibility frame was not ended.");
        if (string.IsNullOrWhiteSpace(screenId))
            throw new ArgumentException("A stable screen identifier is required.", nameof(screenId));
        if (string.IsNullOrWhiteSpace(screenName))
            throw new ArgumentException("A semantic screen name is required.", nameof(screenName));

        CurrentScreenId = screenId;
        _frameBounds = bounds;
        _frameNodes = new List<AccessibilityNode>
        {
            new(screenId, ApplicationRootId, AccessibilityRole.Group, screenName)
            {
                Bounds = bounds
            }
        };
        _frameHandlers = new Dictionary<string, Action<AccessibilityActionRequest>>(
            StringComparer.Ordinal);
    }

    public void Register(
        AccessibilityNode node,
        Action<AccessibilityActionRequest>? handler = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_frameNodes is null || _frameHandlers is null)
            throw new InvalidOperationException("Begin an accessibility frame before registering controls.");
        if (node.Id is ApplicationRootId || node.Id == CurrentScreenId)
            throw new ArgumentException("The frame already owns its root nodes.", nameof(node));

        var interactiveActions = node.SupportedActions & ~AccessibilityAction.Focus;
        if (interactiveActions != AccessibilityAction.None && handler is null)
        {
            throw new ArgumentException(
                $"Interactive accessibility node '{node.Id}' needs an action handler.",
                nameof(handler));
        }

        _frameNodes.Add(node);
        if (handler is not null)
            _frameHandlers.Add(node.Id, handler);
    }

    public AccessibilityTreeUpdate EndFrame(TimeSpan timestamp)
    {
        if (_frameNodes is null || _frameHandlers is null)
            throw new InvalidOperationException("No accessibility frame is active.");

        var update = Tree.Update(
            new AccessibilitySnapshot(
                ApplicationRoot(_frameBounds),
                _frameNodes),
            timestamp);
        _handlers.Clear();
        foreach (var handler in _frameHandlers)
            _handlers.Add(handler.Key, handler.Value);
        _frameNodes = null;
        _frameHandlers = null;
        return update;
    }

    public void DispatchActions(
        Action<AccessibilityActionRequest>? actionCompleted = null)
    {
        while (Tree.TryTakeAction(out var request))
        {
            if (request!.Action == AccessibilityAction.Focus)
            {
                _focusRequests.Add(request.NodeId);
                continue;
            }

            if (_handlers.TryGetValue(request.NodeId, out var handler))
            {
                handler(request);
                actionCompleted?.Invoke(request);
            }
        }
    }

    public bool ConsumeFocusRequest(string nodeId)
    {
        return _focusRequests.Remove(nodeId);
    }

    public void RequestFocus(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("A stable accessibility node identifier is required.", nameof(nodeId));
        _focusRequests.Add(nodeId);
    }

    private static AccessibilityNode ApplicationRoot(AccessibilityBounds bounds)
    {
        return new AccessibilityNode(
            ApplicationRootId,
            ParentId: null,
            AccessibilityRole.Window,
            "Openthesia")
        {
            Bounds = bounds
        };
    }
}
