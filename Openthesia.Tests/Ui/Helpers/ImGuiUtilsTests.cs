using System.Numerics;
using ImGuiNET;
using Openthesia.Ui.Helpers;
using Xunit;

namespace Openthesia.Tests.Ui.Helpers;

[Collection("ImGui")]
public sealed class ImGuiUtilsTests
{
    [Theory]
    [InlineData("Completion 100.0% · 13.6% notes hit")]
    [InlineData("MIDI title with %s and %n tokens")]
    public void WrappedUnformattedTextTreatsPercentCharactersAsText(string text)
    {
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(640, 480);
            io.DeltaTime = 1f / 60f;
            io.Fonts.AddFontDefault();
            IntPtr pixels;
            io.Fonts.GetTexDataAsRGBA32(
                out pixels,
                out _,
                out _,
                out _);

            ImGui.NewFrame();
            ImGui.Begin("Wrapped text test");
            ImGuiUtils.TextWrappedUnformatted(text);
            ImGui.End();
            ImGui.Render();
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }
}
