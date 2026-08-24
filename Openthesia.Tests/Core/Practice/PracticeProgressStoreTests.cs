using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeProgressStoreTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResultsPersistByLearnerAndChartWithComparablePersonalBests()
    {
        var learner = LearnerId.New();
        var otherLearner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('c', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        var first = CreateResult(setup, 8, 10, extras: 1, averageError: 20_000, sequence: 1);
        var best = CreateResult(setup, 9, 10, extras: 2, averageError: 15_000, sequence: 2);

        Assert.True(store.Record(learner, first).Saved);
        Assert.True(store.Record(learner, best).Saved);

        var progress = new PracticeProgressStore(_dataDirectory).Load(learner, chart);
        var snapshot = progress.Progress.For(setup, calibrationRevision: 0);
        Assert.Equal(new[] { first.Id, best.Id }, progress.Progress.Results.Select(result => result.Id));
        Assert.Equal(best.Id, snapshot.BestAccuracy!.Result.Id);
        Assert.Equal(first.Id, snapshot.FirstCompletion!.Result.Id);
        Assert.Empty(store.Load(otherLearner, chart).Progress.Results);
    }

    [Fact]
    public void ExactPersonalBestTiesPreserveFirstAchievementAndTrackMatches()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('d', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        var first = CreateResult(setup, 10, 10, extras: 0, averageError: 20_000, sequence: 1);
        var tie = CreateResult(setup, 10, 10, extras: 0, averageError: 20_000, sequence: 2);

        store.Record(learner, first);
        store.Record(learner, tie);

        var snapshot = store.Load(learner, chart).Progress.For(setup, calibrationRevision: 0);
        Assert.Equal(first.Id, snapshot.BestAccuracy!.Result.Id);
        Assert.Equal(first.EndedAtUtc, snapshot.BestAccuracy.FirstAchievedAtUtc);
        Assert.Equal(tie.EndedAtUtc, snapshot.BestAccuracy.LatestMatchedAtUtc);
        Assert.Equal(2, snapshot.BestAccuracy.MatchCount);
        Assert.Equal(first.Id, snapshot.BestTiming!.Result.Id);
        Assert.Equal(2, snapshot.BestTiming.MatchCount);
    }

    [Fact]
    public void RetainsOneHundredSummariesAndDetailsForFiveLatestComparableResults()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('e', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);

        for (var sequence = 1; sequence <= 105; sequence++)
            store.Record(learner, CreateResult(setup, 10, 10, sequence, sequence * 1_000, sequence));

        var results = store.Load(learner, chart).Progress.Results;
        Assert.Equal(100, results.Count);
        Assert.Equal(6, results[0].Accuracy.ExtraNotes);
        Assert.Empty(results[94].NoteDetails);
        Assert.All(results.Skip(95), result => Assert.NotEmpty(result.NoteDetails));
    }

    [Fact]
    public void CorruptProgressIsPreservedAndNotOverwritten()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('f', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        store.Record(learner, CreateResult(setup, 10, 10, 0, 10_000, 1));
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticeProgress"),
            "*.json",
            SearchOption.AllDirectories).Single();
        File.WriteAllText(path, "not valid JSON");

        var loaded = store.Load(learner, chart);
        var saved = store.Record(learner, CreateResult(setup, 10, 10, 0, 9_000, 2));

        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.NotNull(saved.Warning);
        Assert.Equal("not valid JSON", File.ReadAllText(path));
    }

    [Fact]
    public void StructurallyInvalidProgressIsPreservedAndNotOverwritten()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('8', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        store.Record(learner, CreateResult(setup, 10, 10, 0, 10_000, 1));
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticeProgress"),
            "*.json",
            SearchOption.AllDirectories).Single();
        var invalid =
            $"{{\"Version\":1,\"LearnerId\":\"{learner.Value}\",\"ChartId\":\"{chart.Value}\"," +
            "\"Results\":[null],\"AccuracyBests\":[],\"TimingBests\":[],\"FirstCompletions\":[]}";
        File.WriteAllText(path, invalid);

        var loaded = store.Load(learner, chart);
        var saved = store.Record(learner, CreateResult(setup, 10, 10, 0, 9_000, 2));

        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.NotNull(saved.Warning);
        Assert.Equal(invalid, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("\"RangeStartMicroseconds\": 0", "\"RangeStartMicroseconds\": -1")]
    [InlineData("\"PositionMicroseconds\": 0", "\"PositionMicroseconds\": -1")]
    public void NegativePersistedTimesArePreservedAndNotOverwritten(
        string validFragment,
        string invalidFragment)
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('7', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        store.Record(learner, CreateResult(setup, 10, 10, 0, 10_000, 1));
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticeProgress"),
            "*.json",
            SearchOption.AllDirectories).Single();
        var invalid = File.ReadAllText(path).Replace(validFragment, invalidFragment);
        Assert.NotEqual(File.ReadAllText(path), invalid);
        File.WriteAllText(path, invalid);

        var loaded = store.Load(learner, chart);
        var saved = store.Record(learner, CreateResult(setup, 10, 10, 0, 9_000, 2));

        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.Equal(invalid, File.ReadAllText(path));
    }

    [Fact]
    public void ImpossiblePersistedMetricsArePreservedAndNotOverwritten()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('6', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        store.Record(learner, CreateResult(setup, 10, 10, 0, 10_000, 1));
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticeProgress"),
            "*.json",
            SearchOption.AllDirectories).Single();
        var invalid = File.ReadAllText(path).Replace("\"MatchedNotes\": 10", "\"MatchedNotes\": -1");
        Assert.NotEqual(File.ReadAllText(path), invalid);
        File.WriteAllText(path, invalid);

        var loaded = store.Load(learner, chart);
        var saved = store.Record(learner, CreateResult(setup, 10, 10, 0, 9_000, 2));

        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.Equal(invalid, File.ReadAllText(path));
    }

    [Fact]
    public void MismatchedTimingBestCalibrationIsPreservedAndNotOverwritten()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('5', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        store.Record(learner, CreateResult(setup, 10, 10, 0, 10_000, 1));
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticeProgress"),
            "*.json",
            SearchOption.AllDirectories).Single();
        var original = File.ReadAllText(path);
        var timingBestsStart = original.IndexOf("\"TimingBests\"", StringComparison.Ordinal);
        var revisionStart = original.IndexOf(
            "\"CalibrationRevision\": 0",
            timingBestsStart,
            StringComparison.Ordinal);
        Assert.True(revisionStart > timingBestsStart);
        var invalid = original.Remove(revisionStart, "\"CalibrationRevision\": 0".Length)
            .Insert(revisionStart, "\"CalibrationRevision\": 1");
        File.WriteAllText(path, invalid);

        var loaded = store.Load(learner, chart);
        var saved = store.Record(learner, CreateResult(setup, 10, 10, 0, 9_000, 2));

        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.Equal(invalid, File.ReadAllText(path));
    }

    [Fact]
    public void IneligiblePersistedPersonalBestIsPreservedAndNotOverwritten()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('4', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        store.Record(learner, CreateResult(setup, 10, 10, 0, 10_000, 1));
        var path = Directory.GetFiles(
            Path.Combine(_dataDirectory, "PracticeProgress"),
            "*.json",
            SearchOption.AllDirectories).Single();
        var original = File.ReadAllText(path);
        var accuracyBestsStart = original.IndexOf("\"AccuracyBests\"", StringComparison.Ordinal);
        var assistedStart = original.IndexOf(
            "\"Assisted\": false",
            accuracyBestsStart,
            StringComparison.Ordinal);
        Assert.True(assistedStart > accuracyBestsStart);
        var invalid = original.Remove(assistedStart, "\"Assisted\": false".Length)
            .Insert(assistedStart, "\"Assisted\": true");
        File.WriteAllText(path, invalid);

        var loaded = store.Load(learner, chart);
        var saved = store.Record(learner, CreateResult(setup, 10, 10, 0, 9_000, 2));

        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.Equal(invalid, File.ReadAllText(path));
    }

    [Fact]
    public void IncompleteCompletedResultRemainsHistoryWithoutBecomingPersonalBest()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('3', 64)}");
        var setup = CreateSetup(chart);
        var result = CreateResult(setup, 5, 10, 0, 10_000, 1) with
        {
            Completion = new PracticeCompletion(5, 10)
        };
        var store = new PracticeProgressStore(_dataDirectory);

        var recorded = store.Record(learner, result);
        var loaded = store.Load(learner, chart);
        var snapshot = loaded.Progress.For(setup, calibrationRevision: 0);

        Assert.True(recorded.Saved);
        Assert.Null(loaded.Warning);
        Assert.Equal(result.Id, Assert.Single(loaded.Progress.Results).Id);
        Assert.Null(snapshot.BestAccuracy);
        Assert.Null(snapshot.BestTiming);
        Assert.Null(snapshot.FirstCompletion);
    }

    [Fact]
    public void RecentTrendComparesMediansOfLatestFiveWithPreviousFive()
    {
        var learner = LearnerId.New();
        var chart = ChartId.Parse($"chart-v1-sha256:{new string('9', 64)}");
        var setup = CreateSetup(chart);
        var store = new PracticeProgressStore(_dataDirectory);
        for (var sequence = 1; sequence <= 10; sequence++)
        {
            var recent = sequence > 5;
            store.Record(learner, CreateResult(
                setup,
                hits: 10,
                total: 10,
                extras: recent ? 0 : 2,
                averageError: recent ? 10_000 : 30_000,
                sequence));
        }

        var trend = store.Load(learner, chart).Progress.For(setup, calibrationRevision: 0).RecentTrend;

        Assert.Equal(PracticeTrendDirection.Stable, trend.Accuracy);
        Assert.Equal(PracticeTrendDirection.Improving, trend.Extras);
        Assert.Equal(PracticeTrendDirection.Improving, trend.Timing);
    }

    private static ComparablePracticeSetup CreateSetup(ChartId chart)
    {
        return new ComparablePracticeSetup(
            chart,
            PracticeMode.PlayInTime,
            RequiredHands.Both,
            Accompaniment.Silent,
            TempoRatio: 1m,
            new PracticeRange(ChartTime.Zero, ChartTime.FromMicroseconds(1_000_000)),
            PracticeAssessment.CurrentScoringPolicyVersion);
    }

    private static PracticeResult CreateResult(
        ComparablePracticeSetup setup,
        int hits,
        int total,
        int extras,
        decimal averageError,
        int sequence)
    {
        var ended = DateTimeOffset.Parse("2026-08-23T20:00:00Z").AddMinutes(sequence);
        return new PracticeResult(
            Guid.NewGuid(),
            setup,
            ended.AddMinutes(-1),
            ended,
            PracticeResultOutcome.Completed,
            Assisted: false,
            new PracticeCompletion(total, total),
            new PracticeAccuracy(hits, total, extras, CorrectAttackRatio: null),
            new PracticeTiming(hits, averageError, 0, IsCalibrated: false, CalibrationRevision: 0),
            new[] { new PracticeFeedback(60, ChartTime.Zero, TimingJudgment.Fantastic, 0) });
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
