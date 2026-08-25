using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Plugins;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using System.Diagnostics;
using System.Numerics;

namespace Openthesia.Ui.Windows;

public class HomeWindow : ImGuiWindow
{
    private const string _title = "OPENTHESIA";
    private Vector2 _logoSize = new(250, 250);
    private Vector2 _titleShadowOffset = new(3);
    private Vector2 _buttonsShadowOffset = new(4);
    private Vector2 _buttonsSize = new(300, 50);
    private uint _titleShadowColor = ImGui.GetColorU32(new Vector4(0.13f, 0.83f, 0.93f, 0.5f));
    private bool _isPlayMidiHovered;
    private bool _isPlayModeHovered;
    private bool _isSettingsHovered;
    private bool _isExitHovered;

    public HomeWindow()
    {
        _id = Enums.Windows.Home.ToString();
        _active = true;
    }

    private void DrawTitle()
    {
        float alpha = AccessibilityRuntime.Presentation.AllowDecorativeMotion &&
                      AccessibilityRuntime.Presentation.AllowTransparency
            ? 0.5f * (1.0f + MathF.Sin(2.0f * MathF.PI * _timer))
            : 1f;
        if (_timer >= 1f)
            _timer -= 1f;

        using (AutoFont titleFont = new(FontController.Title))
        {
            var textPos = new Vector2(ImGui.GetIO().DisplaySize.X / 2 - ImGui.CalcTextSize(_title).X / 2, ImGui.GetIO().DisplaySize.Y / 10);
            if (AccessibilityRuntime.Presentation.AllowTransparency)
            {
                ImGui.SetCursorPos(textPos + _titleShadowOffset);
                ImGui.GetWindowDrawList().AddText(textPos + _titleShadowOffset, _titleShadowColor, _title);
            }
            ImGui.SetCursorPos(textPos);
            var titleColor = AccessibilityRuntime.Presentation.UseSystemContrast
                ? AccessibilityRuntime.ContrastPalette.WindowText
                : new Vector4(1, 1, 1, alpha);
            ImGui.TextColored(titleColor, _title);
        }
    }

    private void DrawLogo()
    {
        ImGui.SetCursorPos(ImGui.GetIO().DisplaySize / 2 - ImGuiUtils.FixedSize(new Vector2(_logoSize.X / 2, 300)));
        ImGui.Image(ProgramData.LogoImage, ImGuiUtils.FixedSize(_logoSize));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left, false))
            {
                Process.Start(new ProcessStartInfo("https://openthesia.pages.dev/") { UseShellExecute = true });
            }
        }
    }

    private void DrawButton(string label, (string idle, string hover, string active) htmlColor, ref bool btnHoverRef, Action onClick)
    {
        var drawList = ImGui.GetWindowDrawList();
        ImGuiTheme.PushButton(
            ImGuiTheme.HtmlToVec4(htmlColor.idle),
            ImGuiTheme.HtmlToVec4(htmlColor.hover),
            ImGuiTheme.HtmlToVec4(htmlColor.active));

        if (btnHoverRef)
        {
            // Draw shadow rectangle
            Vector2 buttonPosScreen = ImGui.GetCursorScreenPos();
            Vector2 shadowPosScreen = buttonPosScreen + _buttonsShadowOffset;
            drawList.AddRectFilled(shadowPosScreen, shadowPosScreen + ImGuiUtils.FixedSize(_buttonsSize),
                ImGui.GetColorU32(ImGuiTheme.HtmlToVec4(htmlColor.idle)), 5.0f);
        }

        if (ImGui.Button(label, ImGuiUtils.FixedSize(_buttonsSize)))
            onClick.Invoke();

        btnHoverRef = ImGui.IsItemHovered();
        ImGuiTheme.PopButton();
    }

    private void RenderButtonsContainer()
    {
        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, Math.Min(ImGuiUtils.FixedSize(new Vector2(24)).X, display.X * 0.04f));
        var showBranding = display.Y >= ImGuiUtils.FixedSize(new Vector2(700)).Y;
        var top = showBranding ? display.Y / 2 : margin;
        var width = Math.Min(
            ImGuiUtils.FixedSize(new Vector2(400)).X,
            Math.Max(200f, display.X - margin * 2));
        var height = Math.Max(160f, display.Y - top - margin);
        ImGui.SetCursorScreenPos(new Vector2((display.X - width) / 2, top));
        if (ImGui.BeginChild(
                "Buttons container",
                new Vector2(width, height),
                ImGuiChildFlags.AlwaysUseWindowPadding,
                ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            _buttonsSize = new Vector2(
                Math.Max(100f, ImGui.GetContentRegionAvail().X),
                ImGuiUtils.FixedSize(new Vector2(50)).Y);
            DrawButton("PLAY MIDI FILE", ("#31CB15", "#20870E", "#31CB15"), ref _isPlayMidiHovered, () =>
            {
                WindowsManager.SetWindow(Enums.Windows.MidiBrowser);
            });

            ImGuiUtils.Spacing(2);

            DrawButton("PLAY MODE", ("#0EA5E9", "#096E9B", "#0EA5E9"), ref _isPlayModeHovered, () =>
            {
                WindowsManager.SetWindow(Enums.Windows.PlayMode);
            });

            ImGuiUtils.Spacing(2);

            DrawButton("SETTINGS", ("#464748", "#2E2F30", "#464748"), ref _isSettingsHovered, () =>
            {
                WindowsManager.SetWindow(Enums.Windows.Settings);
            });

            ImGuiUtils.Spacing(2);

            DrawButton("EXIT", ("#B33838", "#772525", "#B33838"), ref _isExitHovered, () =>
            {
                Application.AppInstance.Quit();
            });
        }
        ImGui.EndChild();
    }

    protected override void OnImGui()
    {
        using (AutoFont font22 = new(FontController.GetFontOfSize(22)))
        {
            if (CoreSettings.AnimatedBackground && AccessibilityRuntime.Presentation.AllowDecorativeMotion)
                Drawings.RenderMatrixBackground();

            if (_io.DisplaySize.Y >= ImGuiUtils.FixedSize(new Vector2(700)).Y)
            {
                DrawTitle();
                DrawLogo();
            }
            RenderButtonsContainer();
        }
    }
}
