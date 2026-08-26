using Openthesia.Core;
using Xunit;

namespace Openthesia.Tests.Core;

[Collection("ImGui")]
public sealed class ImGuiFontAtlasTests
{
    [Fact]
    public void SupportedCombinedScaleDoesNotGrowBeyondTheKnownSafeAtlas()
    {
        var safe = ImGuiController.BuildFontAtlasForTesting(2f);
        var combined = ImGuiController.BuildFontAtlasForTesting(4.5f);

        Assert.True(combined.Width <= safe.Width);
        Assert.True(combined.Height <= safe.Height);
        Assert.Equal(2f, combined.Plan.AtlasScale);
        Assert.Equal(2.25f, combined.Plan.GlobalScale);
        Assert.Equal(4.5f, combined.Plan.UiScale);
    }
}
