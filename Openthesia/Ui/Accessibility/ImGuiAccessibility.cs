using ImGuiNET;
using Openthesia.Core.Accessibility;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Openthesia.Ui.Accessibility;

public static class ImGuiAccessibility
{
    private static readonly HashSet<string> ExpandRequests = new(StringComparer.Ordinal);
    private static readonly HashSet<string> CollapseRequests = new(StringComparer.Ordinal);
    private static readonly HashSet<string> RestoreFocusRequests = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ExpandedLastFrame = new(StringComparer.Ordinal);

    public static string StableId(string prefix, string source)
    {
        if (string.IsNullOrEmpty(prefix))
            throw new ArgumentException("A stable ID prefix is required.", nameof(prefix));
        if (string.IsNullOrEmpty(source))
            throw new ArgumentException("A stable ID source is required.", nameof(source));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"{prefix}.{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    public static bool IsComboBoxExpanded(string id)
    {
        return ExpandedLastFrame.Contains(id);
    }

    public static void Button(
        string id,
        string name,
        Action action,
        string? description = null,
        string? value = null,
        bool enabled = true,
        string? parentId = null,
        bool invoked = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (invoked)
        {
            UiAutomationRuntime.NotifyActionCompleted(
                id,
                AccessibilityAction.Invoke);
        }
        RegisterLastItem(
            new AccessibilityNode(
                id,
                parentId ?? UiAutomationRuntime.CurrentScreenId,
                AccessibilityRole.Button,
                name)
            {
                Description = description,
                Value = value,
                IsEnabled = enabled,
                IsFocusable = true,
                SupportedActions = AccessibilityAction.Invoke | AccessibilityAction.Focus
            },
            _ => action());
    }

    public static void Toggle(
        string id,
        string name,
        bool isOn,
        Action toggle,
        string? description = null,
        bool enabled = true,
        string? parentId = null)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        RegisterLastItem(
            new AccessibilityNode(
                id,
                parentId ?? UiAutomationRuntime.CurrentScreenId,
                AccessibilityRole.CheckBox,
                name)
            {
                Description = description,
                Value = isOn ? "On" : "Off",
                ToggleState = isOn ? AccessibilityToggleState.On : AccessibilityToggleState.Off,
                IsEnabled = enabled,
                IsFocusable = true,
                SupportedActions = AccessibilityAction.Toggle | AccessibilityAction.Focus
            },
            _ => toggle());
    }

    public static void Edit(
        string id,
        string name,
        string value,
        Action<string> setValue,
        string? description = null,
        bool enabled = true,
        string? parentId = null)
    {
        ArgumentNullException.ThrowIfNull(setValue);
        RegisterLastItem(
            new AccessibilityNode(
                id,
                parentId ?? UiAutomationRuntime.CurrentScreenId,
                AccessibilityRole.Edit,
                name)
            {
                Description = description,
                Value = value,
                IsEnabled = enabled,
                IsFocusable = true,
                SupportedActions = AccessibilityAction.SetValue | AccessibilityAction.Focus
            },
            request => setValue(request.Value ?? string.Empty));
    }

    public static void Slider(
        string id,
        string name,
        double value,
        double minimum,
        double maximum,
        double smallChange,
        Action<double> setValue,
        string? description = null,
        bool enabled = true,
        string? parentId = null)
    {
        ArgumentNullException.ThrowIfNull(setValue);
        RegisterLastItem(
            new AccessibilityNode(
                id,
                parentId ?? UiAutomationRuntime.CurrentScreenId,
                AccessibilityRole.Slider,
                name)
            {
                Description = description,
                Value = value.ToString("G", CultureInfo.InvariantCulture),
                NumericValue = value,
                Minimum = minimum,
                Maximum = maximum,
                SmallChange = smallChange,
                IsEnabled = enabled,
                IsFocusable = true,
                SupportedActions = AccessibilityAction.SetValue |
                                   AccessibilityAction.Increment |
                                   AccessibilityAction.Decrement |
                                   AccessibilityAction.Focus
            },
            request =>
            {
                if (double.TryParse(
                        request.Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var requestedValue))
                {
                    setValue(Math.Clamp(requestedValue, minimum, maximum));
                }
            });
    }

    public static void ComboBox<T>(
        string id,
        string name,
        T selected,
        IEnumerable<(string Id, string Name, T Value)> options,
        Action<T> select,
        string? description = null,
        bool enabled = true,
        string? parentId = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(select);
        var optionArray = options.ToArray();
        var selectedName = optionArray
            .FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, selected))
            .Name;
        var expandRequested = ExpandRequests.Remove(id);
        var collapseRequested = CollapseRequests.Remove(id);
        var restoreFocus = RestoreFocusRequests.Remove(id);

        ImGui.PushID(id);
        const string popupId = "options";
        var wasOpen = ImGui.IsPopupOpen(popupId);
        if (!wasOpen && ExpandedLastFrame.Remove(id))
            restoreFocus = true;

        ImGui.BeginDisabled(!enabled);
        var preview = selectedName ?? selected.ToString() ?? string.Empty;
        var clicked = ImGui.Button(
            $"{name}: {preview}##combo",
            new Vector2(Math.Max(1f, ImGui.CalcItemWidth()), 0));
        ImGui.EndDisabled();

        if ((clicked && !wasOpen) || expandRequested)
        {
            ImGui.OpenPopup(popupId);
            wasOpen = true;
        }
        else if (clicked && wasOpen)
        {
            collapseRequested = true;
        }

        if (restoreFocus)
            UiAutomationRuntime.Coordinator.RequestFocus(id);

        var isExpanded = wasOpen && !collapseRequested;
        if (isExpanded)
            ExpandedLastFrame.Add(id);
        else
            ExpandedLastFrame.Remove(id);

        RegisterLastItem(
            new AccessibilityNode(
                id,
                parentId ?? UiAutomationRuntime.CurrentScreenId,
                AccessibilityRole.ComboBox,
                name)
            {
                Description = description,
                Value = selectedName ?? selected.ToString(),
                IsEnabled = enabled,
                IsFocusable = true,
                IsExpanded = isExpanded,
                SupportedActions = AccessibilityAction.Expand |
                                   AccessibilityAction.Collapse |
                                   AccessibilityAction.Focus
            },
            request =>
            {
                if (request.Action == AccessibilityAction.Expand)
                    ExpandRequests.Add(id);
                else if (request.Action == AccessibilityAction.Collapse)
                {
                    CollapseRequests.Add(id);
                    RestoreFocusRequests.Add(id);
                }
            });

        var popupVisible = ImGui.BeginPopup(popupId);
        if (popupVisible && collapseRequested)
        {
            ImGui.CloseCurrentPopup();
        }
        else if (popupVisible)
        {
            for (var index = 0; index < optionArray.Length; index++)
            {
                var option = optionArray[index];
                if (expandRequested && index == 0)
                    ImGui.SetKeyboardFocusHere();
                if (ImGui.Selectable(
                        $"{option.Name}##{option.Id}",
                        EqualityComparer<T>.Default.Equals(option.Value, selected)))
                {
                    select(option.Value);
                    ImGui.CloseCurrentPopup();
                    UiAutomationRuntime.Coordinator.RequestFocus(id);
                }
                RegisterLastItem(
                    SelectionItem(option, id, selected, enabled, isOffscreen: false) with
                    {
                        IsFocusable = true,
                        SupportedActions = AccessibilityAction.Select |
                                           AccessibilityAction.Focus
                    },
                    _ =>
                    {
                        select(option.Value);
                        CollapseRequests.Add(id);
                        RestoreFocusRequests.Add(id);
                    });
            }
        }

        if (popupVisible)
            ImGui.EndPopup();

        if (!popupVisible || collapseRequested)
        {
            foreach (var option in optionArray)
            {
                UiAutomationRuntime.Coordinator.Register(
                    SelectionItem(option, id, selected, enabled, isOffscreen: true),
                    _ =>
                    {
                        select(option.Value);
                        RestoreFocusRequests.Add(id);
                    });
            }
        }
        ImGui.PopID();
    }

    private static AccessibilityNode SelectionItem<T>(
        (string Id, string Name, T Value) option,
        string parentId,
        T selected,
        bool enabled,
        bool isOffscreen)
        where T : notnull
    {
        return new AccessibilityNode(
            option.Id,
            parentId,
            AccessibilityRole.ListItem,
            option.Name)
        {
            IsEnabled = enabled,
            IsSelected = EqualityComparer<T>.Default.Equals(option.Value, selected),
            IsOffscreen = isOffscreen,
            SupportedActions = AccessibilityAction.Select
        };
    }

    public static void Text(
        string id,
        string name,
        string value,
        string? description = null,
        AccessibilityLiveSetting liveSetting = AccessibilityLiveSetting.Off,
        string? parentId = null)
    {
        RegisterLastItem(
            new AccessibilityNode(
                id,
                parentId ?? UiAutomationRuntime.CurrentScreenId,
                liveSetting == AccessibilityLiveSetting.Off
                    ? AccessibilityRole.Text
                    : AccessibilityRole.Status,
                name)
            {
                Description = description,
                Value = value,
                LiveSetting = liveSetting
            });
    }

    public static void RegisterLastItem(
        AccessibilityNode node,
        Action<AccessibilityActionRequest>? handler = null)
    {
        var minimum = ImGui.GetItemRectMin();
        var maximum = ImGui.GetItemRectMax();
        var focusRequested = UiAutomationRuntime.Coordinator.ConsumeFocusRequest(node.Id);
        if (focusRequested)
            ImGui.SetKeyboardFocusHere(-1);

        UiAutomationRuntime.Coordinator.Register(
            node with
            {
                Bounds = new AccessibilityBounds(
                    minimum.X,
                    minimum.Y,
                    Math.Max(0, maximum.X - minimum.X),
                    Math.Max(0, maximum.Y - minimum.Y)),
                IsFocused = focusRequested || ImGui.IsItemFocused(),
                IsOffscreen = node.IsOffscreen || !ImGui.IsItemVisible()
            },
            handler);
    }
}
