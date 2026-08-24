namespace Openthesia.Core.Songs;

public sealed class SongCatalog
{
    private const int SchemaVersion = 1;
    private readonly string _catalogPath;
    private readonly string _midiSourcesPath;

    public SongCatalog(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        var root = Path.GetFullPath(dataDirectory);
        _catalogPath = Path.Combine(root, "SongCatalog.json");
        _midiSourcesPath = Path.Combine(root, "MidiSources.json");
    }

    public ResolvedSongChart ResolveMidiSource(string sourcePath, ChartId chartId)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A MIDI source path is required.", nameof(sourcePath));

        var normalizedPath = Path.GetFullPath(sourcePath);
        var document = ReadDocument();
        var sourceDocument = ReadMidiSourcesDocument();
        ValidateSourceReferences(sourceDocument, document);
        var source = sourceDocument.Sources.FirstOrDefault(candidate =>
            StringComparer.OrdinalIgnoreCase.Equals(candidate.Path, normalizedPath));
        var chartDocument = document.Charts.FirstOrDefault(chart =>
            ChartId.Parse(chart.Id) == chartId);
        var catalogChanged = false;

        if (chartDocument is null)
        {
            SongDocument songDocument;
            if (source is null)
            {
                var songId = SongId.New();
                songDocument = new SongDocument
                {
                    Id = songId.ToString(),
                    Title = Path.GetFileNameWithoutExtension(normalizedPath),
                    ChartIds = new List<string> { chartId.Value }
                };
                document.Songs.Add(songDocument);
            }
            else
            {
                var previousChart = document.Charts.Single(chart => chart.Id == source.ChartId);
                songDocument = document.Songs.Single(song => song.Id == previousChart.SongId);
                songDocument.ChartIds.Add(chartId.Value);
            }

            chartDocument = new ChartDocument
            {
                Id = chartId.Value,
                SongId = songDocument.Id
            };
            document.Charts.Add(chartDocument);
            catalogChanged = true;
        }
        else if (source is null || source.ChartId != chartDocument.Id)
        {
            var registeredSong = document.Songs.Single(song => song.Id == chartDocument.SongId);
            if (!registeredSong.ChartIds.Contains(chartDocument.Id))
                throw new InvalidDataException("The Chart is not registered with its Song.");
        }

        var sourceChanged = false;
        if (source is null)
        {
            source = new MidiSourceDocument
            {
                Path = normalizedPath,
                ChartId = chartDocument.Id
            };
            sourceDocument.Sources.Add(source);
            sourceChanged = true;
        }
        else if (source.ChartId != chartDocument.Id)
        {
            source.ChartId = chartDocument.Id;
            sourceChanged = true;
        }

        if (catalogChanged)
            WriteDocument(document);
        if (sourceChanged)
            WriteMidiSourcesDocument(sourceDocument);

        var existingSong = document.Songs.Single(song => song.Id == chartDocument.SongId);
        return ToResolvedSongChart(existingSong, chartDocument, source);
    }

    public IReadOnlyList<string> GetKnownSourcePaths()
    {
        var catalog = ReadDocument();
        var sources = ReadMidiSourcesDocument();
        ValidateSourceReferences(sources, catalog);
        return sources.Sources
            .Select(source => source.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private CatalogDocument ReadDocument()
    {
        if (!File.Exists(_catalogPath))
            return new CatalogDocument
            {
                Version = SchemaVersion,
                Songs = new List<SongDocument>(),
                Charts = new List<ChartDocument>()
            };

        var document = JsonFile.Read<CatalogDocument>(_catalogPath);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Song catalog version {document.Version}.");
        ValidateCatalog(document);

        return document;
    }

    private void WriteDocument(CatalogDocument document)
    {
        JsonFile.Write(_catalogPath, document);
    }

    private MidiSourcesDocument ReadMidiSourcesDocument()
    {
        if (!File.Exists(_midiSourcesPath))
            return new MidiSourcesDocument
            {
                Version = SchemaVersion,
                Sources = new List<MidiSourceDocument>()
            };

        var document = JsonFile.Read<MidiSourcesDocument>(_midiSourcesPath);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported MIDI Sources version {document.Version}.");
        if (document.Sources is null)
            throw new InvalidDataException("The MIDI Sources document has no Sources collection.");
        foreach (var source in document.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Path))
                throw new InvalidDataException("A MIDI Source path is missing.");
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(source.Path), source.Path))
                throw new InvalidDataException("A MIDI Source path is not normalized.");
            ChartId.Parse(source.ChartId);
        }
        if (document.Sources.Select(source => source.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            document.Sources.Count)
        {
            throw new InvalidDataException("A MIDI Source path is registered more than once.");
        }
        return document;
    }

    private void WriteMidiSourcesDocument(MidiSourcesDocument document)
    {
        JsonFile.Write(_midiSourcesPath, document);
    }

    private static void ValidateCatalog(CatalogDocument document)
    {
        if (document.Songs is null || document.Charts is null)
            throw new InvalidDataException("The Song catalog is missing a collection.");

        var songIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var song in document.Songs)
        {
            if (Guid.ParseExact(song.Id, "N") == Guid.Empty)
                throw new InvalidDataException("A Song identity is empty.");
            if (!songIds.Add(song.Id))
                throw new InvalidDataException("A Song identity is registered more than once.");
            if (song.ChartIds is null || song.ChartIds.Count == 0)
                throw new InvalidDataException("A Song must contain at least one Chart.");
            foreach (var id in song.ChartIds)
                ChartId.Parse(id);
        }

        var chartIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chart in document.Charts)
        {
            ChartId.Parse(chart.Id);
            if (!chartIds.Add(chart.Id))
                throw new InvalidDataException("A Chart identity is registered more than once.");
            if (!songIds.Contains(chart.SongId))
                throw new InvalidDataException("A Chart refers to an unknown Song.");
        }

        foreach (var song in document.Songs)
        {
            if (song.ChartIds.Distinct(StringComparer.Ordinal).Count() != song.ChartIds.Count ||
                song.ChartIds.Any(id => !chartIds.Contains(id)) ||
                song.ChartIds.Any(id => document.Charts.Single(chart => chart.Id == id).SongId != song.Id) ||
                document.Charts.Any(chart => chart.SongId == song.Id && !song.ChartIds.Contains(chart.Id)))
            {
                throw new InvalidDataException("A Song's Chart membership is inconsistent.");
            }
        }
    }

    private static void ValidateSourceReferences(
        MidiSourcesDocument sources,
        CatalogDocument catalog)
    {
        var chartIds = catalog.Charts.Select(chart => chart.Id).ToHashSet(StringComparer.Ordinal);
        if (sources.Sources.Any(source => !chartIds.Contains(source.ChartId)))
            throw new InvalidDataException("A MIDI Source refers to an unknown Chart.");
    }

    private static ResolvedSongChart ToResolvedSongChart(
        SongDocument song,
        ChartDocument chart,
        MidiSourceDocument source)
    {
        var songId = new SongId(Guid.ParseExact(song.Id, "N"));
        return new ResolvedSongChart(
            new Song(songId, song.Title, song.ChartIds.Select(ChartId.Parse).ToArray()),
            new Chart(ChartId.Parse(chart.Id), songId),
            new MidiSource(source.Path, ChartId.Parse(source.ChartId)));
    }

    private sealed class CatalogDocument
    {
        public int Version { get; set; }
        public List<SongDocument> Songs { get; set; } = null!;
        public List<ChartDocument> Charts { get; set; } = null!;
    }

    private sealed class SongDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> ChartIds { get; set; } = null!;
    }

    private sealed class ChartDocument
    {
        public string Id { get; set; } = string.Empty;
        public string SongId { get; set; } = string.Empty;
    }

    private sealed class MidiSourcesDocument
    {
        public int Version { get; set; }
        public List<MidiSourceDocument> Sources { get; set; } = null!;
    }

    private sealed class MidiSourceDocument
    {
        public string Path { get; set; } = string.Empty;
        public string ChartId { get; set; } = string.Empty;
    }
}
