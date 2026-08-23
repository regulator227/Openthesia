using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public sealed record PracticePreferences(
    PracticeMode Mode,
    RequiredHands RequiredHands,
    Accompaniment Accompaniment,
    decimal TempoRatio)
{
    public static PracticePreferences Default { get; } = new(
        PracticeMode.WaitForNotes,
        RequiredHands.Both,
        Accompaniment.Silent,
        TempoRatio: 1m);

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
    private const int SchemaVersion = 1;
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
                new PracticePreferences(
                    ParseMode(document.Mode),
                    ParseRequiredHands(document.RequiredHands),
                    ParseAccompaniment(document.Accompaniment),
                    document.TempoRatio),
                Warning: null);
        }
        catch (Exception exception) when (IsDataFailure(exception))
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
        if (File.Exists(path))
        {
            try
            {
                ReadDocument(path, learnerId, chartId);
            }
            catch (Exception exception) when (IsDataFailure(exception))
            {
                return new PracticePreferencesSaveResult(
                    Saved: false,
                    "Existing Practice preferences could not be read and were not overwritten.");
            }
        }

        var document = new PracticePreferencesDocument
        {
            Version = SchemaVersion,
            LearnerId = learnerId.Value,
            ChartId = chartId.Value,
            Mode = preferences.Mode.ToString(),
            RequiredHands = preferences.RequiredHands.ToString(),
            Accompaniment = preferences.Accompaniment.ToString(),
            TempoRatio = preferences.TempoRatio
        };
        try
        {
            JsonFile.Write(path, document);
            return new PracticePreferencesSaveResult(Saved: true, Warning: null);
        }
        catch (Exception exception) when (IsDataFailure(exception))
        {
            return new PracticePreferencesSaveResult(
                Saved: false,
                "Practice preferences could not be saved.");
        }
    }

    private string GetPreferencesPath(LearnerId learnerId, ChartId chartId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(chartId.Value));
        return Path.Combine(
            _preferencesDirectory,
            learnerId.ToString(),
            $"{Convert.ToHexString(hash).ToLowerInvariant()}.json");
    }

    private static PracticePreferencesDocument ReadDocument(
        string path,
        LearnerId expectedLearnerId,
        ChartId expectedChartId)
    {
        var document = JsonFile.Read<PracticePreferencesDocument>(path);
        if (document.Version != SchemaVersion)
            throw new InvalidDataException($"Unsupported Practice preferences version {document.Version}.");
        if (document.LearnerId != expectedLearnerId.Value || document.ChartId != expectedChartId.Value)
            throw new InvalidDataException("The Practice preferences belong to another Learner or Chart.");

        var preferences = new PracticePreferences(
            ParseMode(document.Mode),
            ParseRequiredHands(document.RequiredHands),
            ParseAccompaniment(document.Accompaniment),
            document.TempoRatio);
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
               (preferences.RequiredHands != RequiredHands.Both ||
                preferences.Accompaniment == Accompaniment.Silent);
    }

    private static bool IsDataFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;
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
    }
}
