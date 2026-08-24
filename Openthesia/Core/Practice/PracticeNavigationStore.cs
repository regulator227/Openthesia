using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public sealed record PracticeNavigationLoadResult(
    PracticeNavigation Navigation,
    string? Warning);

public sealed record PracticeNavigationSaveResult(
    bool Saved,
    string? Warning);

public sealed class PracticeNavigationStore
{
    private const int SchemaVersion = 1;
    private readonly string _navigationDirectory;

    public PracticeNavigationStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        _navigationDirectory = Path.Combine(
            Path.GetFullPath(dataDirectory),
            "PracticeNavigation");
    }

    public PracticeNavigationLoadResult Load(
        LearnerId learnerId,
        ChartId chartId,
        ChartTime chartDuration)
    {
        ArgumentNullException.ThrowIfNull(chartId);
        var path = GetPath(learnerId, chartId);
        if (!File.Exists(path))
            return new PracticeNavigationLoadResult(PracticeNavigation.Empty, Warning: null);

        try
        {
            return new PracticeNavigationLoadResult(
                ReadNavigation(path, learnerId, chartId, chartDuration),
                Warning: null);
        }
        catch (Exception exception) when (JsonFile.IsDataFailure(exception))
        {
            return new PracticeNavigationLoadResult(
                PracticeNavigation.Empty,
                "Saved loops and bookmarks could not be read and were preserved.");
        }
    }

    public PracticeNavigationSaveResult Save(
        LearnerId learnerId,
        ChartId chartId,
        ChartTime chartDuration,
        PracticeNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(chartId);
        ArgumentNullException.ThrowIfNull(navigation);
        if (!navigation.IsValid(chartDuration))
        {
            return new PracticeNavigationSaveResult(
                Saved: false,
                "Loops or bookmarks fall outside this Chart and were not saved.");
        }

        var path = GetPath(learnerId, chartId);
        if (!JsonFile.ExistingDocumentCanBeOverwritten(
                path,
                candidate => ReadNavigation(candidate, learnerId, chartId, chartDuration)))
        {
            return new PracticeNavigationSaveResult(
                Saved: false,
                "Existing loops and bookmarks could not be read and were not overwritten.");
        }

        var document = new PracticeNavigationDocument
        {
            Version = SchemaVersion,
            LearnerId = learnerId.Value,
            ChartId = chartId.Value,
            Loops = navigation.Loops.Select(loop => new PracticeLoopDocument
            {
                Id = loop.Id,
                Name = loop.Name,
                StartMicroseconds = loop.Range.Start.Microseconds,
                EndMicroseconds = loop.Range.End.Microseconds
            }).ToList(),
            Bookmarks = navigation.Bookmarks.Select(bookmark => new PracticeBookmarkDocument
            {
                Id = bookmark.Id,
                Name = bookmark.Name,
                PositionMicroseconds = bookmark.Position.Microseconds
            }).ToList()
        };
        return JsonFile.TryWrite(path, document)
            ? new PracticeNavigationSaveResult(Saved: true, Warning: null)
            : new PracticeNavigationSaveResult(
                Saved: false,
                "Loops and bookmarks could not be saved.");
    }

    private string GetPath(LearnerId learnerId, ChartId chartId)
    {
        return JsonFile.GetChartPath(
            Path.Combine(_navigationDirectory, learnerId.ToString()),
            chartId);
    }

    private static PracticeNavigation ReadNavigation(
        string path,
        LearnerId expectedLearnerId,
        ChartId expectedChartId,
        ChartTime chartDuration)
    {
        var document = JsonFile.Read<PracticeNavigationDocument>(path);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Practice navigation version {document.Version}.");
        if (document.LearnerId != expectedLearnerId.Value || document.ChartId != expectedChartId.Value)
            throw new InvalidDataException("The Practice navigation belongs to another Learner or Chart.");

        var navigation = new PracticeNavigation(
            document.Loops.Select(loop => new PracticeLoop(
                loop.Id,
                loop.Name,
                new PracticeRange(
                    ChartTime.FromMicroseconds(loop.StartMicroseconds),
                    ChartTime.FromMicroseconds(loop.EndMicroseconds)))).ToArray(),
            document.Bookmarks.Select(bookmark => new PracticeBookmark(
                bookmark.Id,
                bookmark.Name,
                ChartTime.FromMicroseconds(bookmark.PositionMicroseconds))).ToArray());
        if (!navigation.IsValid(chartDuration))
            throw new InvalidDataException("The Practice navigation contains invalid Chart positions.");
        return navigation;
    }

    private sealed class PracticeNavigationDocument
    {
        public int Version { get; set; }
        public Guid LearnerId { get; set; }
        public string ChartId { get; set; } = string.Empty;
        public List<PracticeLoopDocument> Loops { get; set; } = new();
        public List<PracticeBookmarkDocument> Bookmarks { get; set; } = new();
    }

    private sealed class PracticeLoopDocument
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long StartMicroseconds { get; set; }
        public long EndMicroseconds { get; set; }
    }

    private sealed class PracticeBookmarkDocument
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long PositionMicroseconds { get; set; }
    }
}
