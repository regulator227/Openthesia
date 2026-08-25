using Openthesia.Ui;
using Xunit;

namespace Openthesia.Tests.Ui;

public sealed class ScreenCanvasAccessibilityTests
{
    [Fact]
    public void SoundFontSelectionUsesCatalogCasingOnWindows()
    {
        const string catalogPath = @"C:\SoundFonts\Piano.sf2";

        var selectedPath = ScreenCanvas.ResolveSoundFontSelection(
            @"c:\soundfonts\PIANO.sf2",
            new[] { catalogPath });

        Assert.Equal(catalogPath, selectedPath);
    }
}
