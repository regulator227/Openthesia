using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Songs;

public sealed class SongCatalogTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FirstSourceCreatesDurableSongAndChart()
    {
        var chartId = ChartIdFor('a');
        var sourcePath = Path.Combine(_dataDirectory, "Moonlight Sonata.mid");

        var first = new SongCatalog(_dataDirectory)
            .ResolveMidiSource(sourcePath, chartId);
        var reloaded = new SongCatalog(_dataDirectory)
            .ResolveMidiSource(sourcePath, chartId);

        Assert.Equal(first.Song.Id, reloaded.Song.Id);
        Assert.Equal("Moonlight Sonata", reloaded.Song.Title);
        Assert.Equal(chartId, reloaded.Chart.Id);
        Assert.Equal(Path.GetFullPath(sourcePath), reloaded.Source.Path);
        Assert.DoesNotContain(
            Path.GetFileName(sourcePath),
            File.ReadAllText(Path.Combine(_dataDirectory, "SongCatalog.json")));
        Assert.Contains(
            Path.GetFileName(sourcePath),
            File.ReadAllText(Path.Combine(_dataDirectory, "MidiSources.json")));
    }

    [Fact]
    public void ChangedKnownSourceCreatesAnotherChartForSameSong()
    {
        var sourcePath = Path.Combine(_dataDirectory, "Etude.mid");
        var catalog = new SongCatalog(_dataDirectory);
        var original = catalog.ResolveMidiSource(
            sourcePath,
            ChartIdFor('b'));

        var changed = catalog.ResolveMidiSource(
            sourcePath,
            ChartIdFor('c'));

        Assert.Equal(original.Song.Id, changed.Song.Id);
        Assert.Equal(
            new[] { original.Chart.Id, changed.Chart.Id },
            changed.Song.ChartIds);
    }

    [Fact]
    public void EquivalentPatternFromDifferentSourceUsesSameChartAndSong()
    {
        var chartId = ChartIdFor('d');
        var catalog = new SongCatalog(_dataDirectory);
        var original = catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "Original name.mid"),
            chartId);

        var copy = catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "Renamed copy.mid"),
            chartId);

        Assert.Equal(original.Song.Id, copy.Song.Id);
        Assert.Equal("Original name", copy.Song.Title);
        Assert.Equal(2, catalog.GetKnownSourcePaths().Count);
    }

    [Fact]
    public void DifferentPathAndPatternCreateAnotherSong()
    {
        var catalog = new SongCatalog(_dataDirectory);
        var original = catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "First.mid"),
            ChartIdFor('4'));

        var unrelated = catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "Second.mid"),
            ChartIdFor('5'));

        Assert.NotEqual(original.Song.Id, unrelated.Song.Id);
    }

    [Fact]
    public void CorruptCatalogIsPreservedAndNotOverwritten()
    {
        Directory.CreateDirectory(_dataDirectory);
        var catalogPath = Path.Combine(_dataDirectory, "SongCatalog.json");
        File.WriteAllText(catalogPath, "not valid JSON");
        var catalog = new SongCatalog(_dataDirectory);

        Assert.ThrowsAny<Exception>(() => catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "Prelude.mid"),
            ChartIdFor('e')));
        Assert.Equal("not valid JSON", File.ReadAllText(catalogPath));
    }

    [Fact]
    public void CatalogWithoutVersionIsPreservedAndRejected()
    {
        Directory.CreateDirectory(_dataDirectory);
        var catalogPath = Path.Combine(_dataDirectory, "SongCatalog.json");
        const string unversioned = "{\"Songs\":[],\"Charts\":[]}";
        File.WriteAllText(catalogPath, unversioned);

        Assert.ThrowsAny<Exception>(() => new SongCatalog(_dataDirectory).ResolveMidiSource(
            Path.Combine(_dataDirectory, "Prelude.mid"),
            ChartIdFor('f')));
        Assert.Equal(unversioned, File.ReadAllText(catalogPath));
    }

    [Fact]
    public void MidiSourcesWithoutVersionArePreservedAndRejected()
    {
        var catalog = new SongCatalog(_dataDirectory);
        catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "Prelude.mid"),
            ChartIdFor('9'));
        var sourcesPath = Path.Combine(_dataDirectory, "MidiSources.json");
        const string unversioned = "{\"Sources\":[]}";
        File.WriteAllText(sourcesPath, unversioned);

        Assert.ThrowsAny<Exception>(() => catalog.ResolveMidiSource(
            Path.Combine(_dataDirectory, "Etude.mid"),
            ChartIdFor('a')));
        Assert.Equal(unversioned, File.ReadAllText(sourcesPath));
    }

    [Fact]
    public void InvalidChartIdentityIsRejected()
    {
        Assert.Throws<FormatException>(() => ChartId.Parse("chart-v1-sha256:not-a-hash"));
    }

    private static ChartId ChartIdFor(char hexadecimalDigit)
    {
        return ChartId.Parse($"chart-v1-sha256:{new string(hexadecimalDigit, 64)}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
