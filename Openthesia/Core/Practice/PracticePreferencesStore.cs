using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public sealed record PracticePreferences(
    PracticeMode Mode,
    RequiredHands RequiredHands,
    Accompaniment Accompaniment,
    decimal TempoRatio,
    int CountInBeats = 4,
    bool MetronomeEnabled = true,
    bool CountInOnLoopRepeat = false)
{
    public static IReadOnlyList<int> SupportedCountInBeats { get; } =
        new[] { 0, 2, 4, 8 };

    public static PracticePreferences Default { get; } = new(
        PracticeMode.WaitForNotes,
        RequiredHands.Both,
        Accompaniment.Silent,
        TempoRatio: 1m,
        CountInBeats: 4,
        MetronomeEnabled: true,
        CountInOnLoopRepeat: false);

    public PracticePreferences WithRequiredHands(RequiredHands requiredHands)
    {
        return this with
        {
            RequiredHands = requiredHands,
            Accompaniment = requiredHands == RequiredHands.Both
                ? Accompaniment.Silent
                : Accompaniment.Automatic
        };
    }
}

public sealed record PracticePreferencesLoadResult(
    PracticePreferences Preferences,
    string? Warning);

public sealed record PracticePreferencesSaveResult(
    bool Saved,
    string? Warning);

public sealed class PracticePreferencesStore
{
    private const int SchemaVersion = 2;
    private readonly string _preferencesDirectory;

    public PracticePreferencesStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        _preferencesDirectory = Path.Combine(
            Path.GetFullPath(dataDirectory),
            "PracticePreferences");
    }

    public PracticePreferencesLoadResult Load(LearnerId learnerId, ChartId chartId)
    {
        ArgumentNullException.ThrowIfNull(chartId);
        var path = GetPreferencesPath(learnerId, chartId);
        if (!File.Exists(path))
            return new PracticePreferencesLoadResult(PracticePreferences.Default, Warning: null);

        try
        {
            var document = ReadDocument(path, learnerId, chartId);
            return new PracticePreferencesLoadResult(
                ToPreferences(document),
                Warning: null);
        }
        catch (Exception exception) when (JsonFile.IsDataFailure(exception))
        {
            return new PracticePreferencesLoadResult(
                PracticePreferences.Default,
                "Saved Practice preferences could not be read and were preserved.");
        }
    }

    public PracticePreferencesSaveResult Save(
        LearnerId learnerId,
        ChartId chartId,
        PracticePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(chartId);
        ArgumentNullException.ThrowIfNull(preferences);
        if (!IsValid(preferences))
        {
            return new PracticePreferencesSaveResult(
                Saved: false,
                "Practice preferences are invalid and were not saved.");
        }

        var path = GetPreferencesPath(learnerId, chartId);
        if (!JsonFile.ExistingDocumentCanBeOverwritten(
                path,
                candidatePath => ReadDocument(candidatePath, learnerId, chartId)))
        {
            return new PracticePreferencesSaveResult(
                Saved: false,
                "Existing Practice preferences could not be read and were not overwritten.");
        }

        var document = new PracticePreferencesDocument
        {
            Version = SchemaVersion,
            LearnerId = learnerId.Value,
            ChartId = chartId.Value,
            Mode = preferences.Mode.ToString(),
            RequiredHands = preferences.RequiredHands.ToString(),
            Accompaniment = preferences.Accompaniment.ToString(),
            TempoRatio = preferences.TempoRatio,
            CountInBeats = preferences.CountInBeats,
            MetronomeEnabled = preferences.MetronomeEnabled,
            CountInOnLoopRepeat = preferences.CountInOnLoopRepeat
        };
        return JsonFile.TryWrite(path, document)
            ? new PracticePreferencesSaveResult(Saved: true, Warning: null)
            : new PracticePreferencesSaveResult(
                Saved: false,
                "Practice preferences could not be saved.");
    }

    private string GetPreferencesPath(LearnerId learnerId, ChartId chartId)
    {
        return JsonFile.GetChartPath(
            Path.Combine(_preferencesDirectory, learnerId.ToString()),
            chartId);
    }

    private static PracticePreferencesDocument ReadDocument(
        string path,
        LearnerId expectedLearnerId,
        ChartId expectedChartId)
    {
        var document = JsonFile.Read<PracticePreferencesDocument>(path);
        if (document.Version is < 1 or > SchemaVersion)
            throw new InvalidDataException($"Unsupported Practice preferences version {document.Version}.");
        if (document.LearnerId != expectedLearnerId.Value || document.ChartId != expectedChartId.Value)
            throw new InvalidDataException("The Practice preferences belong to another Learner or Chart.");

        var preferences = ToPreferences(document);
        if (!IsValid(preferences))
            throw new InvalidDataException("The Practice preferences contain an invalid configuration.");

        return document;
    }

    private static PracticeMode ParseMode(string value)
    {
        return value switch
        {
            nameof(PracticeMode.WaitForNotes) => PracticeMode.WaitForNotes,
            nameof(PracticeMode.PlayInTime) => PracticeMode.PlayInTime,
            nameof(PracticeMode.Recital) => PracticeMode.Recital,
            _ => throw new InvalidDataException($"Unknown Practice Mode '{value}'.")
        };
    }

    private static RequiredHands ParseRequiredHands(string value)
    {
        return value switch
        {
            nameof(RequiredHands.Left) => RequiredHands.Left,
            nameof(RequiredHands.Right) => RequiredHands.Right,
            nameof(RequiredHands.Both) => RequiredHands.Both,
            _ => throw new InvalidDataException($"Unknown Required Hands value '{value}'.")
        };
    }

    private static Accompaniment ParseAccompaniment(string value)
    {
        return value switch
        {
            nameof(Accompaniment.Automatic) => Accompaniment.Automatic,
            nameof(Accompaniment.Silent) => Accompaniment.Silent,
            _ => throw new InvalidDataException($"Unknown Accompaniment value '{value}'.")
        };
    }

    private static bool IsValid(PracticePreferences preferences)
    {
        return Enum.IsDefined(preferences.Mode) &&
               Enum.IsDefined(preferences.RequiredHands) &&
               Enum.IsDefined(preferences.Accompaniment) &&
               preferences.TempoRatio > 0 &&
               PracticePreferences.SupportedCountInBeats.Contains(preferences.CountInBeats) &&
               (preferences.RequiredHands != RequiredHands.Both ||
                preferences.Accompaniment == Accompaniment.Silent);
    }

    private static PracticePreferences ToPreferences(PracticePreferencesDocument document)
    {
        return new PracticePreferences(
            ParseMode(document.Mode),
            ParseRequiredHands(document.RequiredHands),
            ParseAccompaniment(document.Accompaniment),
            document.TempoRatio,
            CountInBeats: document.Version >= 2 ? document.CountInBeats : 4,
            MetronomeEnabled: document.Version >= 2 ? document.MetronomeEnabled : true,
            CountInOnLoopRepeat: document.Version >= 2 && document.CountInOnLoopRepeat);
    }

    private sealed class PracticePreferencesDocument
    {
        public int Version { get; set; }
        public Guid LearnerId { get; set; }
        public string ChartId { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string RequiredHands { get; set; } = string.Empty;
        public string Accompaniment { get; set; } = string.Empty;
        public decimal TempoRatio { get; set; }
        public int CountInBeats { get; set; }
        public bool MetronomeEnabled { get; set; }
        public bool CountInOnLoopRepeat { get; set; }
    }
}
