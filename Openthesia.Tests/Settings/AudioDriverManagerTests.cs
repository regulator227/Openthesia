using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Settings;

public sealed class AudioDriverManagerTests
{
    [Fact]
    public void KeepsSavedDriverWhenItIsInstalled()
    {
        var selected = AudioDriverManager.SelectAsioDriver(
            new[] { "First ASIO", "Saved ASIO" },
            "Saved ASIO");

        Assert.Equal("Saved ASIO", selected);
    }

    [Fact]
    public void SelectsFirstInstalledDriverWhenSavedDriverIsStale()
    {
        var selected = AudioDriverManager.SelectAsioDriver(
            new[] { "First ASIO", "Second ASIO" },
            "Missing ASIO");

        Assert.Equal("First ASIO", selected);
    }

    [Fact]
    public void SelectsNoDriverWhenNoneAreInstalled()
    {
        var selected = AudioDriverManager.SelectAsioDriver(
            Array.Empty<string>(),
            "Missing ASIO");

        Assert.Null(selected);
    }
}
