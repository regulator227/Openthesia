using Openthesia.Core.Accessibility;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Provider;

namespace Openthesia.Platform.Windows;

public sealed class WindowsUiAutomationProvider : IDisposable
{
    private const uint WmGetObject = 0x003D;
    private const uint WmNcDestroy = 0x0082;
    private const int UiaRootObjectId = -25;
    private static readonly UIntPtr SubclassId = new(0x4F50454E);
    private static readonly SubclassProcedure SubclassCallback = WindowSubclassProcedure;
    private static readonly ConcurrentDictionary<IntPtr, WindowsUiAutomationProvider> AttachedProviders = new();
    private readonly ConcurrentDictionary<string, ElementProvider> _providers =
        new(StringComparer.Ordinal);
    private readonly IAutomationEventSink _eventSink;
    private readonly IntPtr _windowHandle;
    private readonly AccessibilityTree _tree;

    public WindowsUiAutomationProvider(IntPtr windowHandle, AccessibilityTree tree)
        : this(windowHandle, tree, AutomationEventSink.Instance)
    {
    }

    internal WindowsUiAutomationProvider(
        IntPtr windowHandle,
        AccessibilityTree tree,
        IAutomationEventSink eventSink)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(eventSink);
        _windowHandle = windowHandle;
        _tree = tree;
        _eventSink = eventSink;
        Root = new RootProvider(this, tree.Current.Root.Id);
        _providers.TryAdd(tree.Current.Root.Id, Root);
        if (windowHandle != IntPtr.Zero)
            AttachToWindow();
    }

    internal RootProvider Root { get; }

    internal IRawElementProviderSimple GetProviderForTesting(string nodeId)
    {
        return GetProvider(nodeId);
    }

    internal ElementProvider GetProvider(string nodeId)
    {
        return _providers.GetOrAdd(
            nodeId,
            id => new ElementProvider(this, id));
    }

    internal void NotifyActionCompleted(AccessibilityActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_eventSink.ClientsAreListening ||
            !_tree.Current.TryGet(request.NodeId, out var node) ||
            node is null)
        {
            return;
        }

        var automationEvent = request.Action switch
        {
            AccessibilityAction.Invoke => InvokePatternIdentifiers.InvokedEvent,
            _ => null
        };
        if (automationEvent is null)
            return;

        var provider = GetProvider(request.NodeId);
        _eventSink.RaiseAutomationEvent(
            automationEvent,
            provider,
            new AutomationEventArgs(automationEvent));
    }

    public void Publish(AccessibilityTreeUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!_eventSink.ClientsAreListening)
            return;

        foreach (var change in update.Events)
        {
            if (!_tree.Current.TryGet(change.NodeId, out var node) || node is null)
                continue;
            var provider = GetProvider(change.NodeId);
            switch (change.Kind)
            {
                case AccessibilityEventKind.StructureChanged:
                    _eventSink.RaiseStructureChangedEvent(
                        provider,
                        new StructureChangedEventArgs(
                            StructureChangeType.ChildrenInvalidated,
                            provider.GetRuntimeId() ?? Array.Empty<int>()));
                    break;
                case AccessibilityEventKind.FocusChanged:
                    _eventSink.RaiseAutomationEvent(
                        AutomationElementIdentifiers.AutomationFocusChangedEvent,
                        provider,
                        new AutomationEventArgs(
                            AutomationElementIdentifiers.AutomationFocusChangedEvent));
                    break;
                case AccessibilityEventKind.LiveRegionChanged:
                    _eventSink.RaiseAutomationEvent(
                        AutomationElementIdentifiers.LiveRegionChangedEvent,
                        provider,
                        new AutomationEventArgs(
                            AutomationElementIdentifiers.LiveRegionChangedEvent));
                    break;
                case AccessibilityEventKind.ElementSelected:
                    _eventSink.RaiseAutomationEvent(
                        SelectionItemPatternIdentifiers.ElementSelectedEvent,
                        provider,
                        new AutomationEventArgs(
                            SelectionItemPatternIdentifiers.ElementSelectedEvent));
                    break;
                case AccessibilityEventKind.PropertyChanged:
                    var property = AutomationProperty(node, change.PropertyName);
                    if (property is not null)
                    {
                        _eventSink.RaiseAutomationPropertyChangedEvent(
                            provider,
                            new AutomationPropertyChangedEventArgs(
                                property,
                                AutomationValue(node, change.PropertyName, change.OldValue),
                                AutomationValue(node, change.PropertyName, change.NewValue)));
                    }
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero && AttachedProviders.TryRemove(_windowHandle, out _))
        {
            RemoveWindowSubclass(_windowHandle, SubclassCallback, SubclassId);
            ReleaseProviderEventMap(_windowHandle);
        }
        GC.SuppressFinalize(this);
    }

    private void AttachToWindow()
    {
        if (!AttachedProviders.TryAdd(_windowHandle, this))
            throw new InvalidOperationException("A UI Automation provider is already attached to this window.");
        if (SetWindowSubclass(_windowHandle, SubclassCallback, SubclassId, UIntPtr.Zero))
            return;

        AttachedProviders.TryRemove(_windowHandle, out _);
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static IntPtr WindowSubclassProcedure(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData)
    {
        if (!AttachedProviders.TryGetValue(windowHandle, out var owner))
            return DefSubclassProc(windowHandle, message, wParam, lParam);

        if (message == WmGetObject && unchecked((int)lParam.ToInt64()) == UiaRootObjectId)
        {
            return AutomationInteropProvider.ReturnRawElementProvider(
                windowHandle,
                new IntPtr(unchecked((long)wParam.ToUInt64())),
                lParam,
                owner.Root);
        }

        if (message == WmNcDestroy)
        {
            AttachedProviders.TryRemove(windowHandle, out _);
            RemoveWindowSubclass(windowHandle, SubclassCallback, subclassId);
            ReleaseProviderEventMap(windowHandle);
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    internal interface IAutomationEventSink
    {
        bool ClientsAreListening { get; }

        void RaiseAutomationEvent(
            AutomationEvent automationEvent,
            IRawElementProviderSimple provider,
            AutomationEventArgs eventArgs);

        void RaiseAutomationPropertyChangedEvent(
            IRawElementProviderSimple provider,
            AutomationPropertyChangedEventArgs eventArgs);

        void RaiseStructureChangedEvent(
            IRawElementProviderSimple provider,
            StructureChangedEventArgs eventArgs);
    }

    private sealed class AutomationEventSink : IAutomationEventSink
    {
        public static AutomationEventSink Instance { get; } = new();

        public bool ClientsAreListening => AutomationInteropProvider.ClientsAreListening;

        public void RaiseAutomationEvent(
            AutomationEvent automationEvent,
            IRawElementProviderSimple provider,
            AutomationEventArgs eventArgs)
        {
            AutomationInteropProvider.RaiseAutomationEvent(
                automationEvent,
                provider,
                eventArgs);
        }

        public void RaiseAutomationPropertyChangedEvent(
            IRawElementProviderSimple provider,
            AutomationPropertyChangedEventArgs eventArgs)
        {
            AutomationInteropProvider.RaiseAutomationPropertyChangedEvent(provider, eventArgs);
        }

        public void RaiseStructureChangedEvent(
            IRawElementProviderSimple provider,
            StructureChangedEventArgs eventArgs)
        {
            AutomationInteropProvider.RaiseStructureChangedEvent(provider, eventArgs);
        }
    }

    private static AutomationProperty? AutomationProperty(
        AccessibilityNode node,
        string? propertyName)
    {
        return propertyName switch
        {
            nameof(AccessibilityNode.Name) => AutomationElementIdentifiers.NameProperty,
            nameof(AccessibilityNode.Description) => AutomationElementIdentifiers.HelpTextProperty,
            nameof(AccessibilityNode.Value) when node.Role == AccessibilityRole.Edit =>
                ValuePatternIdentifiers.ValueProperty,
            nameof(AccessibilityNode.Value) => AutomationElementIdentifiers.ItemStatusProperty,
            nameof(AccessibilityNode.NumericValue) => RangeValuePatternIdentifiers.ValueProperty,
            nameof(AccessibilityNode.Minimum) => RangeValuePatternIdentifiers.MinimumProperty,
            nameof(AccessibilityNode.Maximum) => RangeValuePatternIdentifiers.MaximumProperty,
            nameof(AccessibilityNode.ToggleState) => TogglePatternIdentifiers.ToggleStateProperty,
            nameof(AccessibilityNode.LiveSetting) => AutomationElementIdentifiers.LiveSettingProperty,
            nameof(AccessibilityNode.IsEnabled) => AutomationElementIdentifiers.IsEnabledProperty,
            nameof(AccessibilityNode.IsFocusable) => AutomationElementIdentifiers.IsKeyboardFocusableProperty,
            nameof(AccessibilityNode.IsOffscreen) => AutomationElementIdentifiers.IsOffscreenProperty,
            nameof(AccessibilityNode.IsSelected) => SelectionItemPatternIdentifiers.IsSelectedProperty,
            nameof(AccessibilityNode.IsExpanded) => ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            nameof(AccessibilityNode.Bounds) => AutomationElementIdentifiers.BoundingRectangleProperty,
            _ => null
        };
    }

    private object? AutomationValue(
        AccessibilityNode node,
        string? propertyName,
        object? value)
    {
        return propertyName switch
        {
            nameof(AccessibilityNode.Description) or nameof(AccessibilityNode.Value) =>
                value ?? string.Empty,
            nameof(AccessibilityNode.NumericValue) or
            nameof(AccessibilityNode.Minimum) or
            nameof(AccessibilityNode.Maximum) => value ?? 0d,
            nameof(AccessibilityNode.ToggleState) => value switch
            {
                AccessibilityToggleState.On => ToggleState.On,
                AccessibilityToggleState.Indeterminate => ToggleState.Indeterminate,
                _ => ToggleState.Off
            },
            nameof(AccessibilityNode.LiveSetting) => value switch
            {
                AccessibilityLiveSetting.Polite => AutomationLiveSetting.Polite,
                AccessibilityLiveSetting.Assertive => AutomationLiveSetting.Assertive,
                _ => AutomationLiveSetting.Off
            },
            nameof(AccessibilityNode.IsExpanded) => value is true
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed,
            nameof(AccessibilityNode.Bounds) when value is AccessibilityBounds bounds =>
                AutomationRectangle(bounds),
            _ => value
        };
    }

    private double[] AutomationRectangle(AccessibilityBounds bounds)
    {
        var rectangle = ScreenRectangle(bounds);
        return new[] { rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height };
    }

    private Rect ScreenRectangle(AccessibilityBounds bounds)
    {
        var origin = ClientToScreen(bounds.X, bounds.Y);
        return new Rect(origin.X, origin.Y, bounds.Width, bounds.Height);
    }

    internal sealed class RootProvider : ElementProvider, IRawElementProviderFragmentRoot
    {
        public RootProvider(WindowsUiAutomationProvider owner, string nodeId)
            : base(owner, nodeId)
        {
        }

        public IRawElementProviderFragment? ElementProviderFromPoint(double x, double y)
        {
            var point = Owner.ScreenToClient(x, y);
            var match = Tree.Current.Nodes
                .Where(node => node.Bounds.Contains(point.X, point.Y))
                .LastOrDefault();
            return match is null ? this : Owner.GetProvider(match.Id);
        }

        public IRawElementProviderFragment? GetFocus()
        {
            var focused = Tree.Current.Nodes.FirstOrDefault(node => node.IsFocused);
            return focused is null ? null : Owner.GetProvider(focused.Id);
        }
    }

    internal class ElementProvider :
        IRawElementProviderSimple,
        IRawElementProviderFragment,
        IInvokeProvider,
        IToggleProvider,
        IValueProvider,
        IRangeValueProvider,
        ISelectionProvider,
        ISelectionItemProvider,
        IExpandCollapseProvider
    {
        private readonly string _nodeId;

        public ElementProvider(WindowsUiAutomationProvider owner, string nodeId)
        {
            Owner = owner;
            _nodeId = nodeId;
        }

        protected WindowsUiAutomationProvider Owner { get; }
        protected AccessibilityTree Tree => Owner._tree;
        private AccessibilityNode Node => GetNode(Tree.Current);
        private bool IsRoot
        {
            get
            {
                var snapshot = Tree.Current;
                _ = GetNode(snapshot);
                return snapshot.Root.Id == _nodeId;
            }
        }

        public ProviderOptions ProviderOptions =>
            ProviderOptions.ServerSideProvider | ProviderOptions.UseComThreading;

        public IRawElementProviderSimple? HostRawElementProvider =>
            IsRoot && Owner._windowHandle != IntPtr.Zero
                ? AutomationInteropProvider.HostProviderFromHandle(Owner._windowHandle)
                : null;

        public object? GetPatternProvider(int patternId)
        {
            var snapshot = Tree.Current;
            var node = GetNode(snapshot);
            if (patternId == InvokePatternIdentifiers.Pattern.Id &&
                Supports(node, AccessibilityAction.Invoke))
            {
                return this;
            }
            if (patternId == TogglePatternIdentifiers.Pattern.Id &&
                Supports(node, AccessibilityAction.Toggle))
            {
                return this;
            }
            if (patternId == ValuePatternIdentifiers.Pattern.Id &&
                node.Role == AccessibilityRole.Edit)
            {
                return this;
            }
            if (patternId == RangeValuePatternIdentifiers.Pattern.Id &&
                node.Role == AccessibilityRole.Slider)
            {
                return this;
            }
            if (patternId == SelectionPatternIdentifiers.Pattern.Id &&
                node.Role is AccessibilityRole.ComboBox or AccessibilityRole.List &&
                snapshot.GetChildren(node.Id).Any(
                    child => (child.SupportedActions & AccessibilityAction.Select) != 0))
            {
                return this;
            }
            if (patternId == SelectionItemPatternIdentifiers.Pattern.Id &&
                node.Role == AccessibilityRole.ListItem &&
                Supports(node, AccessibilityAction.Select))
            {
                return this;
            }
            if (patternId == ExpandCollapsePatternIdentifiers.Pattern.Id &&
                (Supports(node, AccessibilityAction.Expand) ||
                 Supports(node, AccessibilityAction.Collapse)))
            {
                return this;
            }
            return null;
        }

        public object GetPropertyValue(int propertyId)
        {
            var node = Node;
            if (propertyId == AutomationElementIdentifiers.AutomationIdProperty.Id)
                return node.Id;
            if (propertyId == AutomationElementIdentifiers.NameProperty.Id)
                return node.Name;
            if (propertyId == AutomationElementIdentifiers.HelpTextProperty.Id)
                return node.Description ?? string.Empty;
            if (propertyId == AutomationElementIdentifiers.ItemStatusProperty.Id)
                return node.Value ?? string.Empty;
            if (propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id)
                return ControlTypeId(node.Role);
            if (propertyId == AutomationElementIdentifiers.IsControlElementProperty.Id ||
                propertyId == AutomationElementIdentifiers.IsContentElementProperty.Id)
            {
                return true;
            }
            if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id)
                return node.IsEnabled;
            if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)
                return node.IsFocusable;
            if (propertyId == AutomationElementIdentifiers.HasKeyboardFocusProperty.Id)
                return node.IsFocused;
            if (propertyId == AutomationElementIdentifiers.IsOffscreenProperty.Id)
                return node.IsOffscreen || node.Bounds.IsEmpty;
            if (propertyId == AutomationElementIdentifiers.ClassNameProperty.Id)
                return "Openthesia.ImGui";
            if (propertyId == AutomationElementIdentifiers.FrameworkIdProperty.Id)
                return "Dear ImGui";
            if (propertyId == AutomationElementIdentifiers.NativeWindowHandleProperty.Id)
                return IsRoot ? unchecked((int)Owner._windowHandle.ToInt64()) : 0;
            if (propertyId == AutomationElementIdentifiers.LiveSettingProperty.Id)
            {
                return node.LiveSetting switch
                {
                    AccessibilityLiveSetting.Polite => AutomationLiveSetting.Polite,
                    AccessibilityLiveSetting.Assertive => AutomationLiveSetting.Assertive,
                    _ => AutomationLiveSetting.Off
                };
            }

            return null!;
        }

        public Rect BoundingRectangle
        {
            get => Owner.ScreenRectangle(Node.Bounds);
        }

        public IRawElementProviderFragmentRoot FragmentRoot
        {
            get
            {
                _ = Node;
                return Owner.Root;
            }
        }

        public IRawElementProviderFragment? Navigate(NavigateDirection direction)
        {
            var snapshot = Tree.Current;
            var node = GetNode(snapshot);
            if (direction == NavigateDirection.Parent)
            {
                return node.ParentId is null
                    ? null
                    : Owner.GetProvider(node.ParentId);
            }

            var children = snapshot.GetChildren(node.Id);
            if (direction == NavigateDirection.FirstChild)
                return children.Count == 0 ? null : Owner.GetProvider(children[0].Id);
            if (direction == NavigateDirection.LastChild)
                return children.Count == 0 ? null : Owner.GetProvider(children[^1].Id);
            if (node.ParentId is null)
                return null;

            var siblings = snapshot.GetChildren(node.ParentId);
            var index = siblings
                .Select((sibling, siblingIndex) => (sibling, siblingIndex))
                .First(pair => pair.sibling.Id == node.Id)
                .siblingIndex;
            if (direction == NavigateDirection.NextSibling && index + 1 < siblings.Count)
                return Owner.GetProvider(siblings[index + 1].Id);
            if (direction == NavigateDirection.PreviousSibling && index > 0)
                return Owner.GetProvider(siblings[index - 1].Id);
            return null;
        }

        public int[]? GetRuntimeId()
        {
            var snapshot = Tree.Current;
            _ = GetNode(snapshot);
            return snapshot.Root.Id == _nodeId
                ? null
                : new[]
                {
                    AutomationInteropProvider.AppendRuntimeId,
                    Tree.GetStableId(_nodeId)
                };
        }

        public IRawElementProviderSimple[]? GetEmbeddedFragmentRoots()
        {
            return null;
        }

        public void SetFocus()
        {
            RequestAction(AccessibilityAction.Focus);
        }

        public void Invoke()
        {
            RequestAction(AccessibilityAction.Invoke);
        }

        public void Toggle()
        {
            RequestAction(AccessibilityAction.Toggle);
        }

        ToggleState IToggleProvider.ToggleState => Node.ToggleState switch
        {
            AccessibilityToggleState.On => ToggleState.On,
            AccessibilityToggleState.Indeterminate => ToggleState.Indeterminate,
            _ => ToggleState.Off
        };

        bool IValueProvider.IsReadOnly => !Supports(AccessibilityAction.SetValue);
        string IValueProvider.Value => Node.Value ?? string.Empty;

        void IValueProvider.SetValue(string value)
        {
            RequestAction(AccessibilityAction.SetValue, value);
        }

        bool IRangeValueProvider.IsReadOnly => !Supports(AccessibilityAction.SetValue);
        double IRangeValueProvider.Value => Node.NumericValue ?? 0d;
        double IRangeValueProvider.Minimum => Node.Minimum ?? 0d;
        double IRangeValueProvider.Maximum => Node.Maximum ?? 0d;
        double IRangeValueProvider.SmallChange => Node.SmallChange ?? 0d;
        double IRangeValueProvider.LargeChange => Node.SmallChange ?? 0d;

        void IRangeValueProvider.SetValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            RequestAction(
                AccessibilityAction.SetValue,
                value.ToString("G17", CultureInfo.InvariantCulture));
        }

        bool ISelectionProvider.CanSelectMultiple => false;
        bool ISelectionProvider.IsSelectionRequired
        {
            get
            {
                var snapshot = Tree.Current;
                _ = GetNode(snapshot);
                return snapshot.GetChildren(_nodeId).Count > 0;
            }
        }

        IRawElementProviderSimple[] ISelectionProvider.GetSelection()
        {
            var snapshot = Tree.Current;
            _ = GetNode(snapshot);
            return snapshot.GetChildren(_nodeId)
                .Where(child => child.IsSelected)
                .Select(child => (IRawElementProviderSimple)Owner.GetProvider(child.Id))
                .ToArray();
        }

        bool ISelectionItemProvider.IsSelected => Node.IsSelected;
        IRawElementProviderSimple ISelectionItemProvider.SelectionContainer
        {
            get
            {
                var snapshot = Tree.Current;
                var node = GetNode(snapshot);
                return node.ParentId is null
                    ? throw new InvalidOperationException("A selection item needs a selection container.")
                    : Owner.GetProvider(node.ParentId);
            }
        }

        void ISelectionItemProvider.AddToSelection()
        {
            var snapshot = Tree.Current;
            var node = GetNode(snapshot);
            if (node.ParentId is not null &&
                snapshot.GetChildren(node.ParentId).Any(
                    sibling => sibling.Id != node.Id && sibling.IsSelected))
            {
                throw new InvalidOperationException(
                    "This single-selection container already has a selected item. Use Select to replace it.");
            }
            RequestAction(AccessibilityAction.Select);
        }

        void ISelectionItemProvider.RemoveFromSelection()
        {
            _ = Node;
            throw new InvalidOperationException("Openthesia selections require one current item.");
        }

        void ISelectionItemProvider.Select()
        {
            RequestAction(AccessibilityAction.Select);
        }

        ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState =>
            Node.IsExpanded
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed;

        void IExpandCollapseProvider.Expand()
        {
            RequestAction(AccessibilityAction.Expand);
        }

        void IExpandCollapseProvider.Collapse()
        {
            RequestAction(AccessibilityAction.Collapse);
        }

        private bool Supports(AccessibilityAction action)
        {
            return Supports(Node, action);
        }

        private static bool Supports(
            AccessibilityNode node,
            AccessibilityAction action)
        {
            return (node.SupportedActions & action) != 0;
        }

        private AccessibilityNode GetNode(AccessibilitySnapshot snapshot)
        {
            if (!snapshot.TryGet(_nodeId, out var node) || node is null)
                throw new ElementNotAvailableException();
            return node;
        }

        private void RequestAction(
            AccessibilityAction action,
            string? value = null)
        {
            try
            {
                Tree.RequestAction(_nodeId, action, value);
            }
            catch (KeyNotFoundException)
            {
                throw new ElementNotAvailableException();
            }
        }

        private static int ControlTypeId(AccessibilityRole role)
        {
            return role switch
            {
                AccessibilityRole.Window => ControlType.Window.Id,
                AccessibilityRole.Group => ControlType.Group.Id,
                AccessibilityRole.Button => ControlType.Button.Id,
                AccessibilityRole.CheckBox => ControlType.CheckBox.Id,
                AccessibilityRole.ComboBox => ControlType.ComboBox.Id,
                AccessibilityRole.List => ControlType.List.Id,
                AccessibilityRole.ListItem => ControlType.ListItem.Id,
                AccessibilityRole.Edit => ControlType.Edit.Id,
                AccessibilityRole.Slider => ControlType.Slider.Id,
                AccessibilityRole.Status => ControlType.StatusBar.Id,
                _ => ControlType.Text.Id
            };
        }
    }

    private Point ClientToScreen(double x, double y)
    {
        var point = new NativePoint((int)Math.Round(x), (int)Math.Round(y));
        if (_windowHandle != IntPtr.Zero)
            ClientToScreen(_windowHandle, ref point);
        return new Point(point.X, point.Y);
    }

    private Point ScreenToClient(double x, double y)
    {
        var point = new NativePoint((int)Math.Round(x), (int)Math.Round(y));
        if (_windowHandle != IntPtr.Zero)
            ScreenToClient(_windowHandle, ref point);
        return new Point(point.X, point.Y);
    }

    private static void ReleaseProviderEventMap(IntPtr windowHandle)
    {
        // The managed wrapper rejects null, but the native API reserves this call
        // to release raised-event map entries for a detached or destroyed window.
        UiaReturnRawElementProvider(
            windowHandle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr windowHandle, ref NativePoint point);

    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr UiaReturnRawElementProvider(
        IntPtr windowHandle,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr element);

    private delegate IntPtr SubclassProcedure(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr windowHandle,
        SubclassProcedure callback,
        UIntPtr subclassId,
        UIntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr windowHandle,
        SubclassProcedure callback,
        UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);
}
