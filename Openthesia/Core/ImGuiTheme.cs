using ImGuiNET;
using Openthesia.Settings;
using System.Numerics;
using Openthesia.Ui.Helpers;

namespace Openthesia.Core;

public static class ImGuiTheme
{
    public static ImGuiStylePtr Style;
    public static Vector4 Button = new Vector4(0.29f, 0.29f, 0.29f, .9f);
    public static Vector4 ButtonHovered = new Vector4(0.29f, 0.29f, 0.29f, .9f) * 1.2f;
    public static Vector4 ButtonActive = new Vector4(0.29f, 0.29f, 0.29f, .9f) * 1.5f;
    public static Vector4 DarkButton = ImGuiUtils.DarkenColor(Button, 0.5f);

    public static Vector4 HtmlToVec4(string htmlColor, float alpha = 1f)
    {
        if (htmlColor == null || htmlColor.Length != 7 || htmlColor[0] != '#')
            throw new ArgumentException("Invalid HTML color code");

        int r = Convert.ToInt32(htmlColor.Substring(1, 2), 16);
        int g = Convert.ToInt32(htmlColor.Substring(3, 2), 16);
        int b = Convert.ToInt32(htmlColor.Substring(5, 2), 16);

        return new Vector4(r / 255f, g / 255f, b / 255f, alpha);
    }

    public static void PushTheme()
    {
        Style = ImGui.GetStyle();
        Style.FrameRounding = 4 * FontController.DSF;
        Style.FramePadding = ImGuiUtils.FixedSize(new Vector2(5, 7));
        Style.ItemSpacing = ImGuiUtils.FixedSize(new Vector2(8, 6));
        Style.TouchExtraPadding = ImGuiUtils.FixedSize(new Vector2(2));
        Style.FrameBorderSize = Math.Max(1f, FontController.DSF);
        Style.WindowPadding = Vector2.Zero;
        Style.Colors[(int)ImGuiCol.NavHighlight] = HtmlToVec4("#7DD3FC");

        if (AccessibilityRuntime.Presentation.UseSystemContrast)
        {
            var palette = AccessibilityRuntime.ContrastPalette;
            var interactive = AccessibilityPolicy.ContrastRatio(palette.WindowText, palette.Highlight) >= 4.5d
                ? palette.Highlight
                : palette.Window;
            var focus = AccessibilityPolicy.ContrastRatio(palette.Window, palette.Highlight) >= 3d
                ? palette.Highlight
                : palette.WindowText;
            Style.Colors[(int)ImGuiCol.Text] = palette.WindowText;
            Style.Colors[(int)ImGuiCol.MenuBarBg] = palette.Window;
            Style.Colors[(int)ImGuiCol.WindowBg] = palette.Window;
            Style.Colors[(int)ImGuiCol.ChildBg] = palette.Window;
            Style.Colors[(int)ImGuiCol.Button] = palette.Window;
            Style.Colors[(int)ImGuiCol.ButtonHovered] = interactive;
            Style.Colors[(int)ImGuiCol.ButtonActive] = interactive;
            Style.Colors[(int)ImGuiCol.TitleBgActive] = interactive;
            Style.Colors[(int)ImGuiCol.FrameBg] = palette.Window;
            Style.Colors[(int)ImGuiCol.FrameBgHovered] = interactive;
            Style.Colors[(int)ImGuiCol.FrameBgActive] = interactive;
            Style.Colors[(int)ImGuiCol.Header] = palette.Window;
            Style.Colors[(int)ImGuiCol.HeaderHovered] = interactive;
            Style.Colors[(int)ImGuiCol.HeaderActive] = interactive;
            Style.Colors[(int)ImGuiCol.PopupBg] = palette.Window;
            Style.Colors[(int)ImGuiCol.TableRowBg] = palette.Window;
            Style.Colors[(int)ImGuiCol.TableRowBgAlt] = palette.Window;
            Style.Colors[(int)ImGuiCol.NavHighlight] = focus;
            Style.Colors[(int)ImGuiCol.Border] = palette.WindowText;
            Style.Colors[(int)ImGuiCol.CheckMark] = palette.WindowText;
            Style.Colors[(int)ImGuiCol.SliderGrab] = palette.WindowText;
            Style.Colors[(int)ImGuiCol.SliderGrabActive] = palette.WindowText;
            Style.FrameBorderSize = Math.Max(2f, FontController.DSF * 2f);
        }
        else
        {
            var window = Opaque(ThemeManager.MainBgCol);
            var text = ReadableText(window);
            var button = AccessibleBackground(Button, text);
            Style.Colors[(int)ImGuiCol.MenuBarBg] = HtmlToVec4("#1F2937");
            Style.Colors[(int)ImGuiCol.Text] = text;
            Style.Colors[(int)ImGuiCol.WindowBg] = window;
            Style.Colors[(int)ImGuiCol.ChildBg] = window;
            Style.Colors[(int)ImGuiCol.Button] = button;
            Style.Colors[(int)ImGuiCol.ButtonHovered] = AccessibleBackground(ButtonHovered, text);
            Style.Colors[(int)ImGuiCol.ButtonActive] = AccessibleBackground(ButtonActive, text);
            Style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(1, 0, 0, 1);
            Style.Colors[(int)ImGuiCol.FrameBg] = button;
            Style.Colors[(int)ImGuiCol.FrameBgHovered] = AccessibleBackground(ButtonHovered, text);
            Style.Colors[(int)ImGuiCol.FrameBgActive] = AccessibleBackground(ButtonActive, text);
            Style.Colors[(int)ImGuiCol.Header] = button;
            Style.Colors[(int)ImGuiCol.HeaderHovered] = AccessibleBackground(ButtonHovered, text);
            Style.Colors[(int)ImGuiCol.HeaderActive] = AccessibleBackground(ButtonActive, text);
            Style.Colors[(int)ImGuiCol.PopupBg] = window;
            Style.Colors[(int)ImGuiCol.TableRowBg] = window;
            Style.Colors[(int)ImGuiCol.TableRowBgAlt] = button;
            Style.Colors[(int)ImGuiCol.NavHighlight] = text;
            Style.Colors[(int)ImGuiCol.Border] = text;
            Style.Colors[(int)ImGuiCol.CheckMark] = text;
            Style.Colors[(int)ImGuiCol.SliderGrab] = text;
            Style.Colors[(int)ImGuiCol.SliderGrabActive] = text;
        }

        Style.PopupRounding = 5 * FontController.DSF;
        Style.CellPadding = ImGuiUtils.FixedSize(new Vector2(10));
        Style.ScrollbarRounding = 0;
    }

    public static void PushButton(Vector4 col, Vector4 hCol, Vector4 aCol)
    {
        if (AccessibilityRuntime.Presentation.UseSystemContrast)
        {
            var palette = AccessibilityRuntime.ContrastPalette;
            var interactive = AccessibilityPolicy.ContrastRatio(palette.WindowText, palette.Highlight) >= 4.5d
                ? palette.Highlight
                : palette.Window;
            Style.Colors[(int)ImGuiCol.Button] = palette.Window;
            Style.Colors[(int)ImGuiCol.ButtonHovered] = interactive;
            Style.Colors[(int)ImGuiCol.ButtonActive] = interactive;
            return;
        }

        var text = Style.Colors[(int)ImGuiCol.Text];
        Style.Colors[(int)ImGuiCol.Button] = AccessibleBackground(col, text);
        Style.Colors[(int)ImGuiCol.ButtonHovered] = AccessibleBackground(hCol, text);
        Style.Colors[(int)ImGuiCol.ButtonActive] = AccessibleBackground(aCol, text);
    }

    public static void PopButton()
    {
        if (AccessibilityRuntime.Presentation.UseSystemContrast)
        {
            var palette = AccessibilityRuntime.ContrastPalette;
            var interactive = AccessibilityPolicy.ContrastRatio(palette.WindowText, palette.Highlight) >= 4.5d
                ? palette.Highlight
                : palette.Window;
            Style.Colors[(int)ImGuiCol.Button] = palette.Window;
            Style.Colors[(int)ImGuiCol.ButtonHovered] = interactive;
            Style.Colors[(int)ImGuiCol.ButtonActive] = interactive;
        }
        else
        {
            var text = Style.Colors[(int)ImGuiCol.Text];
            Style.Colors[(int)ImGuiCol.Button] = AccessibleBackground(Button, text);
            Style.Colors[(int)ImGuiCol.ButtonHovered] = AccessibleBackground(ButtonHovered, text);
            Style.Colors[(int)ImGuiCol.ButtonActive] = AccessibleBackground(ButtonActive, text);
        }
    }

    internal static Vector4 ReadableText(Vector4 background)
    {
        return AccessibilityPolicy.ContrastRatio(background, Vector4.One) >=
               AccessibilityPolicy.ContrastRatio(background, new Vector4(0, 0, 0, 1))
            ? Vector4.One
            : new Vector4(0, 0, 0, 1);
    }

    private static Vector4 AccessibleBackground(Vector4 requested, Vector4 text)
    {
        var result = Opaque(requested);
        var towards = text.X + text.Y + text.Z > 1.5f
            ? new Vector4(0, 0, 0, 1)
            : Vector4.One;
        for (var attempt = 0;
             attempt < 12 && AccessibilityPolicy.ContrastRatio(result, text) < 4.5d;
             attempt++)
        {
            result = Vector4.Lerp(result, towards, 0.15f);
            result.W = 1f;
        }
        return result;
    }

    private static Vector4 Opaque(Vector4 color)
    {
        return new Vector4(color.X, color.Y, color.Z, 1f);
    }
}
