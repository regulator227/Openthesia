using System.Xml.Linq;

namespace Openthesia.Core.Songs;

public sealed class ChartHandAssignmentStore
{
    private const int SchemaVersion = 1;
    private readonly string _assignmentsDirectory;

    public ChartHandAssignmentStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        _assignmentsDirectory = Path.Combine(
            Path.GetFullPath(dataDirectory),
            "ChartHandAssignments");
    }

    public HandAssignmentLoadResult Load(ChartId chartId, int noteCount)
    {
        return Load(chartId, noteCount, legacyCandidate: null);
    }

    public HandAssignmentLoadResult Load(
        ChartId chartId,
        int noteCount,
        LegacyHandAssignmentCandidate? legacyCandidate)
    {
        if (noteCount < 0)
            throw new ArgumentOutOfRangeException(nameof(noteCount));

        var path = GetAssignmentPath(chartId);
        if (!File.Exists(path))
            return LoadLegacyOrDefault(chartId, noteCount, legacyCandidate);

        try
        {
            var document = ReadDocument(path, chartId);
            if (document.Hands.Count != noteCount)
            {
                return Defaults(
                    noteCount,
                    "Saved Hand Assignments do not match this Chart's notes and were not loaded.");
            }

            return new HandAssignmentLoadResult(
                document.Hands.Select(ParseHand).ToArray(),
                Warning: null);
        }
        catch (Exception exception) when (JsonFile.IsDataFailure(exception))
        {
            return Defaults(
                noteCount,
                "Saved Hand Assignments could not be read and were preserved.");
        }
    }

    public HandAssignmentSaveResult Save(ChartId chartId, IReadOnlyList<PianoHand> hands)
    {
        ArgumentNullException.ThrowIfNull(hands);
        foreach (var hand in hands)
            ParseHand(hand.ToString());

        var path = GetAssignmentPath(chartId);
        if (!JsonFile.ExistingDocumentCanBeOverwritten(
                path,
                candidatePath => ReadDocument(candidatePath, chartId)))
        {
            return new HandAssignmentSaveResult(
                Saved: false,
                "Existing Hand Assignments could not be read and were not overwritten.");
        }

        var document = new HandAssignmentDocument
        {
            Version = SchemaVersion,
            ChartId = chartId.Value,
            Hands = hands.Select(hand => hand.ToString()).ToList()
        };
        return JsonFile.TryWrite(path, document)
            ? new HandAssignmentSaveResult(Saved: true, Warning: null)
            : new HandAssignmentSaveResult(
                Saved: false,
                "Hand Assignments could not be saved.");
    }

    private string GetAssignmentPath(ChartId chartId)
    {
        return JsonFile.GetChartPath(_assignmentsDirectory, chartId);
    }

    private HandAssignmentLoadResult LoadLegacyOrDefault(
        ChartId chartId,
        int noteCount,
        LegacyHandAssignmentCandidate? candidate)
    {
        if (candidate is null || !File.Exists(candidate.Path))
            return Defaults(noteCount);

        if (!candidate.IsUnambiguous)
        {
            return Defaults(
                noteCount,
                "Legacy Hand Assignments were not imported because the MIDI filename is ambiguous.");
        }

        try
        {
            var document = XDocument.Load(candidate.Path);
            if (document.Root?.Name.LocalName != "LeftRightData")
                throw new InvalidDataException("Unknown legacy Hand Assignment document.");

            var legacyValues = document.Root
                .Elements()
                .Single(element => element.Name.LocalName == "IsRightNote")
                .Elements()
                .Select(element => bool.Parse(element.Value))
                .Select(isRight => isRight ? PianoHand.Right : PianoHand.Left)
                .ToArray();
            if (legacyValues.Length != noteCount)
            {
                return Defaults(
                    noteCount,
                    "Legacy Hand Assignments do not match this Chart's notes and were not imported.");
            }

            var values = ReorderLegacyAssignments(legacyValues, candidate, noteCount);

            var saveResult = Save(chartId, values);
            if (!saveResult.Saved)
                return Defaults(noteCount, saveResult.Warning);

            return new HandAssignmentLoadResult(
                values,
                Warning: null,
                MigratedLegacyData: true);
        }
        catch (Exception exception) when (IsLegacyReadFailure(exception))
        {
            return Defaults(
                noteCount,
                "Legacy Hand Assignments could not be read and were preserved.");
        }
    }

    private static HandAssignmentDocument ReadDocument(string path, ChartId expectedChartId)
    {
        var document = JsonFile.Read<HandAssignmentDocument>(path);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Hand Assignment version {document.Version}.");
        if (document.ChartId != expectedChartId.Value)
            throw new InvalidDataException("The Hand Assignment document belongs to another Chart.");
        ChartId.Parse(document.ChartId);
        if (document.Hands is null)
            throw new InvalidDataException("The Hand Assignment document has no assignments.");

        foreach (var hand in document.Hands)
            ParseHand(hand);
        return document;
    }

    private static IReadOnlyList<PianoHand> ReorderLegacyAssignments(
        IReadOnlyList<PianoHand> legacyValues,
        LegacyHandAssignmentCandidate candidate,
        int noteCount)
    {
        var indices = candidate.CanonicalToLegacyNoteIndices;
        if (indices is null)
            return legacyValues;
        if (indices.Count != noteCount ||
            indices.Distinct().Count() != noteCount ||
            indices.Any(index => index < 0 || index >= noteCount))
        {
            throw new InvalidDataException("The legacy note order does not match this Chart.");
        }

        return indices.Select(index => legacyValues[index]).ToArray();
    }

    private static PianoHand ParseHand(string value)
    {
        return value switch
        {
            nameof(PianoHand.Left) => PianoHand.Left,
            nameof(PianoHand.Right) => PianoHand.Right,
            _ => throw new InvalidDataException($"Unknown Hand Assignment value '{value}'.")
        };
    }

    private static HandAssignmentLoadResult Defaults(int noteCount, string? warning = null)
    {
        return new HandAssignmentLoadResult(
            Enumerable.Repeat(PianoHand.Right, noteCount).ToArray(),
            warning);
    }

    private static bool IsLegacyReadFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or InvalidDataException or
            System.Xml.XmlException or InvalidOperationException or FormatException;
    }

    private sealed class HandAssignmentDocument
    {
        public int Version { get; set; }
        public string ChartId { get; set; } = string.Empty;
        public List<string> Hands { get; set; } = new();
    }
}
