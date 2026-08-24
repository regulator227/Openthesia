using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public sealed record PracticePersonalBest(
    PracticeResult Result,
    DateTimeOffset FirstAchievedAtUtc,
    DateTimeOffset LatestMatchedAtUtc,
    int MatchCount);

public sealed record PracticeFirstCompletion(PracticeResult Result);

public enum PracticeTrendDirection
{
    NotEnoughData,
    Improving,
    Stable,
    Declining
}

public sealed record PracticeRecentTrend(
    PracticeTrendDirection Accuracy,
    PracticeTrendDirection Extras,
    PracticeTrendDirection Timing);

public sealed record PracticeProgressSnapshot(
    PracticePersonalBest? BestAccuracy,
    PracticePersonalBest? BestTiming,
    PracticeFirstCompletion? FirstCompletion,
    PracticeRecentTrend RecentTrend);

public sealed class PracticeProgress
{
    private readonly IReadOnlyList<StoredBest> _accuracyBests;
    private readonly IReadOnlyList<StoredBest> _timingBests;
    private readonly IReadOnlyList<StoredCompletion> _firstCompletions;

    internal PracticeProgress(
        IReadOnlyList<PracticeResult> results,
        IReadOnlyList<StoredBest> accuracyBests,
        IReadOnlyList<StoredBest> timingBests,
        IReadOnlyList<StoredCompletion> firstCompletions)
    {
        Results = results;
        _accuracyBests = accuracyBests;
        _timingBests = timingBests;
        _firstCompletions = firstCompletions;
    }

    public IReadOnlyList<PracticeResult> Results { get; }
    internal IReadOnlyList<StoredBest> AccuracyBests => _accuracyBests;
    internal IReadOnlyList<StoredBest> TimingBests => _timingBests;
    internal IReadOnlyList<StoredCompletion> FirstCompletions => _firstCompletions;

    public PracticeProgressSnapshot For(
        ComparablePracticeSetup setup,
        int calibrationRevision)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var accuracy = _accuracyBests.SingleOrDefault(best => best.Setup == setup);
        var timing = _timingBests.SingleOrDefault(
            best => best.Setup == setup && best.CalibrationRevision == calibrationRevision);
        var completion = _firstCompletions.SingleOrDefault(item => item.Setup == setup);
        var eligible = Results
            .Where(result =>
                result.Setup == setup &&
                result.IsEligible &&
                result.Completion.Ratio == 1m)
            .OrderBy(result => result.EndedAtUtc)
            .ToArray();
        var timed = eligible
            .Where(result => result.Accuracy.RequiredNotesHitRatio == 1m)
            .Where(result => result.Timing?.CalibrationRevision == calibrationRevision)
            .ToArray();
        return new PracticeProgressSnapshot(
            accuracy?.Best,
            timing?.Best,
            completion is null ? null : new PracticeFirstCompletion(completion.Result),
            new PracticeRecentTrend(
                Direction(
                    eligible.Select(result => result.Accuracy.RequiredNotesHitRatio).ToArray(),
                    stableThreshold: 0.01m,
                    higherIsBetter: true),
                Direction(
                    eligible.Select(result => (decimal)result.Accuracy.ExtraNotes).ToArray(),
                    stableThreshold: 1m,
                    higherIsBetter: false),
                Direction(
                    timed.Select(result => result.Timing!.AverageAbsoluteErrorMicroseconds).ToArray(),
                    stableThreshold: 5_000m,
                    higherIsBetter: false)));
    }

    private static PracticeTrendDirection Direction(
        IReadOnlyList<decimal> values,
        decimal stableThreshold,
        bool higherIsBetter)
    {
        if (values.Count < 10)
            return PracticeTrendDirection.NotEnoughData;

        var previousMedian = Median(values.Skip(values.Count - 10).Take(5));
        var latestMedian = Median(values.Skip(values.Count - 5));
        var difference = latestMedian - previousMedian;
        if (Math.Abs(difference) < stableThreshold)
            return PracticeTrendDirection.Stable;
        var improved = higherIsBetter ? difference > 0 : difference < 0;
        return improved ? PracticeTrendDirection.Improving : PracticeTrendDirection.Declining;
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return ordered[ordered.Length / 2];
    }

    internal static PracticeProgress Empty { get; } = new(
        Array.Empty<PracticeResult>(),
        Array.Empty<StoredBest>(),
        Array.Empty<StoredBest>(),
        Array.Empty<StoredCompletion>());

    internal sealed record StoredBest(
        ComparablePracticeSetup Setup,
        int? CalibrationRevision,
        PracticePersonalBest Best);

    internal sealed record StoredCompletion(
        ComparablePracticeSetup Setup,
        PracticeResult Result);
}

public sealed record PracticeProgressLoadResult(
    PracticeProgress Progress,
    string? Warning);

public sealed record PracticeProgressRecordResult(
    PracticeProgress Progress,
    bool Saved,
    string? Warning);

public sealed class PracticeProgressStore
{
    private const int SchemaVersion = 1;
    private const int ResultRetention = 100;
    private const int DetailRetentionPerSetup = 5;
    private readonly string _progressDirectory;

    public PracticeProgressStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        _progressDirectory = Path.Combine(Path.GetFullPath(dataDirectory), "PracticeProgress");
    }

    public PracticeProgressLoadResult Load(LearnerId learnerId, ChartId chartId)
    {
        ArgumentNullException.ThrowIfNull(chartId);
        var path = GetProgressPath(learnerId, chartId);
        if (!File.Exists(path))
            return new PracticeProgressLoadResult(PracticeProgress.Empty, Warning: null);

        try
        {
            return new PracticeProgressLoadResult(
                ToProgress(ReadDocument(path, learnerId, chartId)),
                Warning: null);
        }
        catch (Exception exception) when (JsonFile.IsDataFailure(exception) || exception is FormatException)
        {
            return new PracticeProgressLoadResult(
                PracticeProgress.Empty,
                "Saved Practice progress could not be read and was preserved.");
        }
    }

    public PracticeProgressRecordResult Record(LearnerId learnerId, PracticeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var chartId = result.Setup.ChartId;
        var path = GetProgressPath(learnerId, chartId);
        PracticeProgress progress;
        if (File.Exists(path))
        {
            try
            {
                progress = ToProgress(ReadDocument(path, learnerId, chartId));
            }
            catch (Exception exception) when (JsonFile.IsDataFailure(exception) || exception is FormatException)
            {
                return new PracticeProgressRecordResult(
                    PracticeProgress.Empty,
                    Saved: false,
                    "Existing Practice progress could not be read and was not overwritten.");
            }
        }
        else
        {
            progress = PracticeProgress.Empty;
        }

        var updated = Add(progress, result);
        var document = FromProgress(learnerId, chartId, updated);
        if (!JsonFile.TryWrite(path, document))
        {
            return new PracticeProgressRecordResult(
                progress,
                Saved: false,
                "Practice progress could not be saved.");
        }

        return new PracticeProgressRecordResult(updated, Saved: true, Warning: null);
    }

    private string GetProgressPath(LearnerId learnerId, ChartId chartId)
    {
        return JsonFile.GetChartPath(
            Path.Combine(_progressDirectory, learnerId.ToString()),
            chartId);
    }

    private static PracticeProgress Add(PracticeProgress progress, PracticeResult result)
    {
        var results = progress.Results
            .Append(result)
            .OrderBy(item => item.EndedAtUtc)
            .ThenBy(item => item.Id)
            .TakeLast(ResultRetention)
            .ToArray();
        var detailIds = results
            .GroupBy(item => item.Setup)
            .SelectMany(group => group
                .OrderByDescending(item => item.EndedAtUtc)
                .ThenByDescending(item => item.Id)
                .Take(DetailRetentionPerSetup)
                .Select(item => item.Id))
            .ToHashSet();
        results = results
            .Select(item => detailIds.Contains(item.Id)
                ? item
                : item with { NoteDetails = Array.Empty<PracticeFeedback>() })
            .ToArray();

        var accuracyBests = progress.AccuracyBests.ToList();
        var timingBests = progress.TimingBests.ToList();
        var completions = progress.FirstCompletions.ToList();
        if (result.IsEligible && result.Completion.Ratio == 1m)
        {
            UpdateBest(
                accuracyBests,
                result.Setup,
                calibrationRevision: null,
                result,
                CompareAccuracy);

            if (result.Accuracy.RequiredNotesHitRatio == 1m && result.Timing is not null)
            {
                UpdateBest(
                    timingBests,
                    result.Setup,
                    result.Timing.CalibrationRevision,
                    result,
                    CompareTiming);
            }

            if (completions.All(item => item.Setup != result.Setup))
            {
                completions.Add(new PracticeProgress.StoredCompletion(
                    result.Setup,
                    WithoutDetails(result)));
            }
        }

        return new PracticeProgress(results, accuracyBests, timingBests, completions);
    }

    private static void UpdateBest(
        List<PracticeProgress.StoredBest> bests,
        ComparablePracticeSetup setup,
        int? calibrationRevision,
        PracticeResult candidate,
        Func<PracticeResult, PracticeResult, int> compare)
    {
        var index = bests.FindIndex(
            best => best.Setup == setup && best.CalibrationRevision == calibrationRevision);
        if (index < 0)
        {
            bests.Add(new PracticeProgress.StoredBest(
                setup,
                calibrationRevision,
                new PracticePersonalBest(
                    WithoutDetails(candidate),
                    candidate.EndedAtUtc,
                    candidate.EndedAtUtc,
                    MatchCount: 1)));
            return;
        }

        var existing = bests[index];
        var comparison = compare(candidate, existing.Best.Result);
        if (comparison > 0)
        {
            bests[index] = existing with
            {
                Best = new PracticePersonalBest(
                    WithoutDetails(candidate),
                    candidate.EndedAtUtc,
                    candidate.EndedAtUtc,
                    MatchCount: 1)
            };
        }
        else if (comparison == 0)
        {
            bests[index] = existing with
            {
                Best = existing.Best with
                {
                    LatestMatchedAtUtc = candidate.EndedAtUtc,
                    MatchCount = existing.Best.MatchCount + 1
                }
            };
        }
    }

    private static int CompareAccuracy(PracticeResult candidate, PracticeResult existing)
    {
        var hitComparison = candidate.Accuracy.RequiredNotesHitRatio.CompareTo(
            existing.Accuracy.RequiredNotesHitRatio);
        return hitComparison != 0
            ? hitComparison
            : existing.Accuracy.ExtraNotes.CompareTo(candidate.Accuracy.ExtraNotes);
    }

    private static int CompareTiming(PracticeResult candidate, PracticeResult existing)
    {
        return existing.Timing!.AverageAbsoluteErrorMicroseconds.CompareTo(
            candidate.Timing!.AverageAbsoluteErrorMicroseconds);
    }

    private static PracticeResult WithoutDetails(PracticeResult result)
    {
        return result with { NoteDetails = Array.Empty<PracticeFeedback>() };
    }

    private static PracticeProgressDocument ReadDocument(
        string path,
        LearnerId expectedLearnerId,
        ChartId expectedChartId)
    {
        var document = JsonFile.Read<PracticeProgressDocument>(path);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Practice progress version {document.Version}.");
        if (document.LearnerId != expectedLearnerId.Value || document.ChartId != expectedChartId.Value)
            throw new InvalidDataException("The Practice progress belongs to another Learner or Chart.");
        return document;
    }

    private static PracticeProgress ToProgress(PracticeProgressDocument document)
    {
        if (document.Results is null || document.AccuracyBests is null ||
            document.TimingBests is null || document.FirstCompletions is null ||
            document.Results.Any(item => item is null) ||
            document.AccuracyBests.Any(item => item is null) ||
            document.TimingBests.Any(item => item is null) ||
            document.FirstCompletions.Any(item => item is null))
        {
            throw new InvalidDataException("Practice progress is missing required collections.");
        }

        var progress = new PracticeProgress(
            document.Results.Select(ToResult).ToArray(),
            document.AccuracyBests.Select(ToStoredBest).ToArray(),
            document.TimingBests.Select(ToStoredBest).ToArray(),
            document.FirstCompletions.Select(item => new PracticeProgress.StoredCompletion(
                ToSetup(item.Setup),
                ToResult(item.Result))).ToArray());
        if (progress.Results.Any(result => result.Setup.ChartId.Value != document.ChartId) ||
            progress.AccuracyBests.Any(best => best.Setup.ChartId.Value != document.ChartId) ||
            progress.TimingBests.Any(best => best.Setup.ChartId.Value != document.ChartId) ||
            progress.FirstCompletions.Any(item => item.Setup.ChartId.Value != document.ChartId) ||
            progress.AccuracyBests.Any(best => best.Best.Result.Setup != best.Setup) ||
            progress.TimingBests.Any(best =>
                best.Best.Result.Setup != best.Setup ||
                best.Best.Result.Timing?.CalibrationRevision != best.CalibrationRevision) ||
            progress.FirstCompletions.Any(item => item.Result.Setup != item.Setup) ||
            progress.AccuracyBests.Any(best => !IsValidAccuracyBest(best)) ||
            progress.TimingBests.Any(best => !IsValidTimingBest(best)) ||
            progress.FirstCompletions.Any(item =>
                !item.Result.IsEligible || item.Result.Completion.Ratio != 1m) ||
            progress.AccuracyBests.GroupBy(best => best.Setup).Any(group => group.Count() > 1) ||
            progress.TimingBests.GroupBy(best => new { best.Setup, best.CalibrationRevision }).Any(group => group.Count() > 1) ||
            progress.FirstCompletions.GroupBy(item => item.Setup).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Practice progress contains conflicting identity or milestone records.");
        }
        return progress;
    }

    private static bool IsValidAccuracyBest(PracticeProgress.StoredBest stored)
    {
        return stored.CalibrationRevision is null &&
               stored.Best.Result.IsEligible &&
               stored.Best.Result.Completion.Ratio == 1m &&
               HasValidBestHistory(stored.Best);
    }

    private static bool IsValidTimingBest(PracticeProgress.StoredBest stored)
    {
        return stored.CalibrationRevision is not null &&
               stored.Best.Result.IsEligible &&
               stored.Best.Result.Completion.Ratio == 1m &&
               stored.Best.Result.Accuracy.RequiredNotesHitRatio == 1m &&
               stored.Best.Result.Timing is not null &&
               HasValidBestHistory(stored.Best);
    }

    private static bool HasValidBestHistory(PracticePersonalBest best)
    {
        return best.Result.EndedAtUtc == best.FirstAchievedAtUtc &&
               best.LatestMatchedAtUtc >= best.FirstAchievedAtUtc &&
               (best.MatchCount != 1 || best.LatestMatchedAtUtc == best.FirstAchievedAtUtc);
    }

    private static PracticeProgressDocument FromProgress(
        LearnerId learnerId,
        ChartId chartId,
        PracticeProgress progress)
    {
        return new PracticeProgressDocument
        {
            Version = SchemaVersion,
            LearnerId = learnerId.Value,
            ChartId = chartId.Value,
            Results = progress.Results.Select(FromResult).ToList(),
            AccuracyBests = progress.AccuracyBests.Select(FromStoredBest).ToList(),
            TimingBests = progress.TimingBests.Select(FromStoredBest).ToList(),
            FirstCompletions = progress.FirstCompletions.Select(item => new CompletionDocument
            {
                Setup = FromSetup(item.Setup),
                Result = FromResult(item.Result)
            }).ToList()
        };
    }

    private static PracticeProgress.StoredBest ToStoredBest(BestDocument document)
    {
        if (document is null || document.Setup is null || document.Result is null || document.MatchCount < 1)
            throw new InvalidDataException("A personal best must have at least one match.");
        return new PracticeProgress.StoredBest(
            ToSetup(document.Setup),
            document.CalibrationRevision,
            new PracticePersonalBest(
                ToResult(document.Result),
                RequireUtc(document.FirstAchievedAtUtc),
                RequireUtc(document.LatestMatchedAtUtc),
                document.MatchCount));
    }

    private static BestDocument FromStoredBest(PracticeProgress.StoredBest stored)
    {
        return new BestDocument
        {
            Setup = FromSetup(stored.Setup),
            CalibrationRevision = stored.CalibrationRevision,
            Result = FromResult(stored.Best.Result),
            FirstAchievedAtUtc = stored.Best.FirstAchievedAtUtc.ToUniversalTime(),
            LatestMatchedAtUtc = stored.Best.LatestMatchedAtUtc.ToUniversalTime(),
            MatchCount = stored.Best.MatchCount
        };
    }

    private static PracticeResult ToResult(ResultDocument document)
    {
        if (document is null || document.Setup is null || document.NoteDetails is null ||
            document.NoteDetails.Any(detail => detail is null) ||
            document.NoteDetails.Any(detail => detail.PositionMicroseconds < 0) ||
            document.Id == Guid.Empty || document.TotalRequiredNotes < 0 ||
            document.EvaluatedRequiredNotes < 0 || document.RequiredNotesHit < 0 ||
            document.ExtraNotes < 0 || document.RequiredNotesHit > document.TotalRequiredNotes ||
            document.RequiredNotesHit > document.EvaluatedRequiredNotes ||
            document.EvaluatedRequiredNotes > document.TotalRequiredNotes ||
            document.CorrectAttackRatio is < 0 or > 1 ||
            document.EndedAtUtc < document.StartedAtUtc ||
            document.Timing is { MatchedNotes: <= 0 } ||
            document.Timing is { } timingDocument &&
            (timingDocument.MatchedNotes > document.RequiredNotesHit ||
             timingDocument.AverageAbsoluteErrorMicroseconds < 0 ||
             Math.Abs(timingDocument.AverageSignedOffsetMicroseconds) >
             timingDocument.AverageAbsoluteErrorMicroseconds ||
             timingDocument.CalibrationRevision < 0))
        {
            throw new InvalidDataException("Practice progress contains invalid result metrics.");
        }

        var timing = document.Timing is null
            ? null
            : new PracticeTiming(
                document.Timing.MatchedNotes,
                document.Timing.AverageAbsoluteErrorMicroseconds,
                document.Timing.AverageSignedOffsetMicroseconds,
                document.Timing.IsCalibrated,
                document.Timing.CalibrationRevision);
        return new PracticeResult(
            document.Id,
            ToSetup(document.Setup),
            RequireUtc(document.StartedAtUtc),
            RequireUtc(document.EndedAtUtc),
            ParseEnum<PracticeResultOutcome>(document.Outcome),
            document.Assisted,
            new PracticeCompletion(document.EvaluatedRequiredNotes, document.TotalRequiredNotes),
            new PracticeAccuracy(
                document.RequiredNotesHit,
                document.TotalRequiredNotes,
                document.ExtraNotes,
                document.CorrectAttackRatio),
            timing,
            document.NoteDetails.Select(detail => new PracticeFeedback(
                detail.Pitch,
                ChartTime.FromMicroseconds(detail.PositionMicroseconds),
                ParseEnum<TimingJudgment>(detail.Judgment),
                detail.SignedOffsetMicroseconds)).ToArray());
    }

    private static ResultDocument FromResult(PracticeResult result)
    {
        return new ResultDocument
        {
            Id = result.Id,
            Setup = FromSetup(result.Setup),
            StartedAtUtc = result.StartedAtUtc.ToUniversalTime(),
            EndedAtUtc = result.EndedAtUtc.ToUniversalTime(),
            Outcome = result.Outcome.ToString(),
            Assisted = result.Assisted,
            EvaluatedRequiredNotes = result.Completion.EvaluatedRequiredNotes,
            TotalRequiredNotes = result.Completion.TotalRequiredNotes,
            RequiredNotesHit = result.Accuracy.RequiredNotesHit,
            ExtraNotes = result.Accuracy.ExtraNotes,
            CorrectAttackRatio = result.Accuracy.CorrectAttackRatio,
            Timing = result.Timing is null ? null : new TimingDocument
            {
                MatchedNotes = result.Timing.MatchedNotes,
                AverageAbsoluteErrorMicroseconds = result.Timing.AverageAbsoluteErrorMicroseconds,
                AverageSignedOffsetMicroseconds = result.Timing.AverageSignedOffsetMicroseconds,
                IsCalibrated = result.Timing.IsCalibrated,
                CalibrationRevision = result.Timing.CalibrationRevision
            },
            NoteDetails = result.NoteDetails.Select(detail => new FeedbackDocument
            {
                Pitch = detail.Pitch,
                PositionMicroseconds = detail.Position.Microseconds,
                Judgment = detail.Judgment.ToString(),
                SignedOffsetMicroseconds = detail.SignedOffsetMicroseconds
            }).ToList()
        };
    }

    private static ComparablePracticeSetup ToSetup(SetupDocument document)
    {
        if (document is null)
            throw new InvalidDataException("Practice progress is missing a comparable setup.");
        if (document.RangeStartMicroseconds < 0 || document.RangeEndMicroseconds < 0)
            throw new InvalidDataException("Practice progress contains a negative Chart time.");
        var start = ChartTime.FromMicroseconds(document.RangeStartMicroseconds);
        var end = ChartTime.FromMicroseconds(document.RangeEndMicroseconds);
        if (end.CompareTo(start) <= 0 || document.TempoRatio <= 0 ||
            string.IsNullOrWhiteSpace(document.ScoringPolicyVersion))
        {
            throw new InvalidDataException("Practice progress contains an invalid setup.");
        }

        return new ComparablePracticeSetup(
            ChartId.Parse(document.ChartId),
            ParseEnum<PracticeMode>(document.Mode),
            ParseEnum<RequiredHands>(document.RequiredHands),
            ParseEnum<Accompaniment>(document.Accompaniment),
            document.TempoRatio,
            new PracticeRange(start, end),
            document.ScoringPolicyVersion);
    }

    private static SetupDocument FromSetup(ComparablePracticeSetup setup)
    {
        return new SetupDocument
        {
            ChartId = setup.ChartId.Value,
            Mode = setup.Mode.ToString(),
            RequiredHands = setup.RequiredHands.ToString(),
            Accompaniment = setup.Accompaniment.ToString(),
            TempoRatio = setup.TempoRatio,
            RangeStartMicroseconds = setup.Range.Start.Microseconds,
            RangeEndMicroseconds = setup.Range.End.Microseconds,
            ScoringPolicyVersion = setup.ScoringPolicyVersion
        };
    }

    private static T ParseEnum<T>(string value) where T : struct, Enum
    {
        return Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"Unknown {typeof(T).Name} value '{value}'.");
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Practice progress timestamps must be UTC.");
        return value;
    }

    private sealed class PracticeProgressDocument
    {
        public int Version { get; set; }
        public Guid LearnerId { get; set; }
        public string ChartId { get; set; } = string.Empty;
        public List<ResultDocument> Results { get; set; } = new();
        public List<BestDocument> AccuracyBests { get; set; } = new();
        public List<BestDocument> TimingBests { get; set; } = new();
        public List<CompletionDocument> FirstCompletions { get; set; } = new();
    }

    private sealed class BestDocument
    {
        public SetupDocument Setup { get; set; } = new();
        public int? CalibrationRevision { get; set; }
        public ResultDocument Result { get; set; } = new();
        public DateTimeOffset FirstAchievedAtUtc { get; set; }
        public DateTimeOffset LatestMatchedAtUtc { get; set; }
        public int MatchCount { get; set; }
    }

    private sealed class CompletionDocument
    {
        public SetupDocument Setup { get; set; } = new();
        public ResultDocument Result { get; set; } = new();
    }

    private sealed class SetupDocument
    {
        public string ChartId { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string RequiredHands { get; set; } = string.Empty;
        public string Accompaniment { get; set; } = string.Empty;
        public decimal TempoRatio { get; set; }
        public long RangeStartMicroseconds { get; set; }
        public long RangeEndMicroseconds { get; set; }
        public string ScoringPolicyVersion { get; set; } = string.Empty;
    }

    private sealed class ResultDocument
    {
        public Guid Id { get; set; }
        public SetupDocument Setup { get; set; } = new();
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset EndedAtUtc { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public bool Assisted { get; set; }
        public int EvaluatedRequiredNotes { get; set; }
        public int TotalRequiredNotes { get; set; }
        public int RequiredNotesHit { get; set; }
        public int ExtraNotes { get; set; }
        public decimal? CorrectAttackRatio { get; set; }
        public TimingDocument? Timing { get; set; }
        public List<FeedbackDocument> NoteDetails { get; set; } = new();
    }

    private sealed class TimingDocument
    {
        public int MatchedNotes { get; set; }
        public decimal AverageAbsoluteErrorMicroseconds { get; set; }
        public decimal AverageSignedOffsetMicroseconds { get; set; }
        public bool IsCalibrated { get; set; }
        public int CalibrationRevision { get; set; }
    }

    private sealed class FeedbackDocument
    {
        public byte Pitch { get; set; }
        public long PositionMicroseconds { get; set; }
        public string Judgment { get; set; } = string.Empty;
        public long? SignedOffsetMicroseconds { get; set; }
    }
}
