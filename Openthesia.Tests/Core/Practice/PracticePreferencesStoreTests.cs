using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticePreferencesStoreTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PreferencesPersistByLearnerAndChartIdentity()
    {
        var learner = LearnerId.New();
        var otherLearner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('a', 64)}");
        var preferences = new PracticePreferences(
            PracticeMode.WaitForNotes,
            RequiredHands.Right,
            Accompaniment.Automatic,
            TempoRatio: 0.75m);
        var store = new PracticePreferencesStore(_dataDirectory);

        var saved = store.Save(learner, chart, preferences);
        var loaded = new PracticePreferencesStore(_dataDirectory).Load(learner, chart);
        var unrelated = store.Load(otherLearner, chart);

        Assert.True(saved.Saved);
        Assert.Null(saved.Warning);
        Assert.Equal(preferences, loaded.Preferences);
        Assert.Null(loaded.Warning);
        Assert.Equal(PracticePreferences.Default, unrelated.Preferences);
    }

    [Fact]
    public void CorruptPreferencesArePreservedAndNotOverwritten()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('b', 64)}");
        var store = new PracticePreferencesStore(_dataDirectory);
        store.Save(learner, chart, PracticePreferences.Default);
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticePreferences"),
            "*.json",
            SearchOption.AllDirectories).Single();
        File.WriteAllText(path, "not valid JSON");

        var loaded = store.Load(learner, chart);
        var saved = store.Save(
            learner,
            chart,
            PracticePreferences.Default.WithRequiredHands(RequiredHands.Left));

        Assert.Equal(PracticePreferences.Default, loaded.Preferences);
        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.NotNull(saved.Warning);
        Assert.Equal("not valid JSON", File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
