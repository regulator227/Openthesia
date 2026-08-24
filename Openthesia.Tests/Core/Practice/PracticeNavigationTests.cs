using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeNavigationTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PracticeRangesIncludeTheStartAndExcludeTheEnd()
    {
        var range = new PracticeRange(
            ChartTime.FromMicroseconds(500_000),
            ChartTime.FromMicroseconds(1_000_000));

        Assert.True(range.Contains(ChartTime.FromMicroseconds(500_000)));
        Assert.True(range.Contains(ChartTime.FromMicroseconds(999_999)));
        Assert.False(range.Contains(ChartTime.FromMicroseconds(1_000_000)));
    }

    [Fact]
    public void NavigationPersistsByLearnerAndChartIdentity()
    {
        var learner = LearnerId.New();
        var otherLearner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('a', 64)}");
        var duration = ChartTime.FromMicroseconds(4_000_000);
        var loopId = Guid.NewGuid();
        var bookmarkId = Guid.NewGuid();
        var navigation = PracticeNavigation.Empty
            .SaveLoop(
                loopId,
                "Chorus",
                new PracticeRange(
                    ChartTime.FromMicroseconds(1_000_000),
                    ChartTime.FromMicroseconds(2_000_000)))
            .SaveBookmark(
                bookmarkId,
                "Coda",
                ChartTime.FromMicroseconds(3_000_000));
        var store = new PracticeNavigationStore(_dataDirectory);

        var saved = store.Save(learner, chart, duration, navigation);
        var loaded = new PracticeNavigationStore(_dataDirectory).Load(learner, chart, duration);
        var unrelated = store.Load(otherLearner, chart, duration);

        Assert.True(saved.Saved);
        Assert.Null(saved.Warning);
        Assert.Equal(navigation, loaded.Navigation);
        Assert.Null(loaded.Warning);
        Assert.Equal(PracticeNavigation.Empty, unrelated.Navigation);
    }

    [Fact]
    public void EditingWaypointsKeepsStableIdentityAndNormalizesNames()
    {
        var loopId = Guid.NewGuid();
        var bookmarkId = Guid.NewGuid();
        var firstRange = new PracticeRange(
            ChartTime.FromMicroseconds(1_000_000),
            ChartTime.FromMicroseconds(2_000_000));
        var editedRange = new PracticeRange(
            ChartTime.FromMicroseconds(1_500_000),
            ChartTime.FromMicroseconds(2_500_000));

        var navigation = PracticeNavigation.Empty
            .SaveLoop(loopId, "  Verse  ", firstRange)
            .SaveBookmark(bookmarkId, "  Entry  ", ChartTime.FromMicroseconds(500_000))
            .SaveLoop(loopId, "Verse pickup", editedRange)
            .SaveBookmark(bookmarkId, "", ChartTime.FromMicroseconds(750_000));

        Assert.Equal(
            new PracticeLoop(loopId, "Verse pickup", editedRange),
            Assert.Single(navigation.Loops));
        Assert.Equal(
            new PracticeBookmark(bookmarkId, "Bookmark", ChartTime.FromMicroseconds(750_000)),
            Assert.Single(navigation.Bookmarks));
    }

    [Fact]
    public void PreviousAndNextBookmarksUseThePlayheadAndWrap()
    {
        var verse = new PracticeBookmark(
            Guid.NewGuid(),
            "Verse",
            ChartTime.FromMicroseconds(1_000_000));
        var coda = new PracticeBookmark(
            Guid.NewGuid(),
            "Coda",
            ChartTime.FromMicroseconds(3_000_000));
        var navigation = PracticeNavigation.Empty
            .SaveBookmark(coda.Id, coda.Name, coda.Position)
            .SaveBookmark(verse.Id, verse.Name, verse.Position);

        Assert.Equal(verse, navigation.FindBookmark(
            ChartTime.FromMicroseconds(500_000),
            PracticeNavigationDirection.Next));
        Assert.Equal(coda, navigation.FindBookmark(
            ChartTime.FromMicroseconds(2_000_000),
            PracticeNavigationDirection.Next));
        Assert.Equal(verse, navigation.FindBookmark(
            ChartTime.FromMicroseconds(2_000_000),
            PracticeNavigationDirection.Previous));
        Assert.Equal(coda, navigation.FindBookmark(
            ChartTime.FromMicroseconds(500_000),
            PracticeNavigationDirection.Previous));
    }

    [Fact]
    public void FinalBeatLoopCanEndAtTheChartBoundary()
    {
        var duration = ChartTime.FromMicroseconds(4_000_000);
        var navigation = PracticeNavigation.Empty.SaveLoop(
            Guid.NewGuid(),
            "Final beat",
            new PracticeRange(
                ChartTime.FromMicroseconds(3_500_000),
                duration));
        var store = new PracticeNavigationStore(_dataDirectory);

        var result = store.Save(
            LearnerId.New(),
            ChartId.Parse($"chart-v1-sha256:{new string('b', 64)}"),
            duration,
            navigation);

        Assert.True(result.Saved);
    }

    [Fact]
    public void InvalidNavigationIsNotSaved()
    {
        var duration = ChartTime.FromMicroseconds(4_000_000);
        var navigation = PracticeNavigation.Empty.SaveLoop(
            Guid.NewGuid(),
            "Past the end",
            new PracticeRange(
                ChartTime.FromMicroseconds(3_500_000),
                ChartTime.FromMicroseconds(4_500_000)));

        var result = new PracticeNavigationStore(_dataDirectory).Save(
            LearnerId.New(),
            ChartId.Parse($"chart-v1-sha256:{new string('c', 64)}"),
            duration,
            navigation);

        Assert.False(result.Saved);
        Assert.NotNull(result.Warning);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
