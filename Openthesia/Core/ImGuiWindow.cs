using ImGuiNET;
using Openthesia.Ui.Accessibility;
using System.Numerics;

namespace Openthesia.Core;

public abstract class ImGuiWindow
{
    protected ImGuiIOPtr _io = ImGui.GetIO();

    /// <summary>
    /// ImGui window id
    /// </summary>
    protected string _id = string.Empty;

    /// <summary>
    /// ImGui window state
    /// </summary>
    protected bool _active;
    private bool _focusOnActivation = true;

    /// <summary>
    /// ImGui window flags
    /// </summary>
    protected ImGuiWindowFlags _windowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize;

    /// <summary>
    /// True if window fills screen content
    /// </summary>
    protected bool _isMainWindow = true;

    /// <summary>
    /// Timer utility
    /// </summary>
    protected float _timer = 0f;

    public string GetId()
    {
        return _id;
    }

    public ref bool Active()
    {
        return ref _active;
    }

    public void SetActive(bool active)
    {
        if (active && !_active)
            _focusOnActivation = true;
        _active = active;
    }

    /// <summary>
    /// Window rendering
    /// </summary>
    public void RenderWindow()
    {
        UiAutomationRuntime.BeginFrame(_id, _io.DisplaySize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        var visible = ImGui.Begin(_id, ref _active, _windowFlags);
        ImGui.PopStyleVar();
        if (visible)
        {
            if (_isMainWindow)
            {
                ImGui.SetWindowPos(Vector2.Zero);
                ImGui.SetWindowSize(_io.DisplaySize);
            }

            if (_focusOnActivation)
            {
                ImGui.SetWindowFocus();
                ImGui.SetKeyboardFocusHere();
                _focusOnActivation = false;
            }

            _timer += _io.DeltaTime; // update window related timer
            OnImGui();
        }
        ImGui.End();
        UiAutomationRuntime.EndFrame();
    }

    /// <summary>
    /// Window content rendering
    /// </summary>
    protected abstract void OnImGui();
}
