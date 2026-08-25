using Openthesia.Core.Songs;

namespace Openthesia.Settings;

public enum VisualEffectsPreference
{
    System,
    Reduce,
    Full
}

public sealed record AccessibilitySettings(VisualEffectsPreference VisualEffects)
{
    public static AccessibilitySettings Default { get; } = new(
        VisualEffectsPreference.System);
}

public sealed record AccessibilitySettingsLoadResult(
    AccessibilitySettings Settings,
    string? Warning = null);

public sealed record AccessibilitySettingsSaveResult(
    bool Saved,
    string? Warning = null);

public sealed class AccessibilitySettingsStore
{
    private readonly string _path;

    public AccessibilitySettingsStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));
        _path = Path.Combine(dataDirectory, "Accessibility.json");
    }

    public AccessibilitySettingsLoadResult Load()
    {
        if (!File.Exists(_path))
            return new AccessibilitySettingsLoadResult(AccessibilitySettings.Default);

        try
        {
            var document = ReadDocument(_path);
            return new AccessibilitySettingsLoadResult(new AccessibilitySettings(
                Enum.Parse<VisualEffectsPreference>(document.VisualEffects)));
        }
        catch (Exception exception) when (JsonFile.IsDataFailure(exception) || exception is ArgumentException)
        {
            return new AccessibilitySettingsLoadResult(
                AccessibilitySettings.Default,
                "Accessibility settings could not be loaded. The existing file was preserved.");
        }
    }

    public AccessibilitySettingsSaveResult Save(AccessibilitySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!JsonFile.ExistingDocumentCanBeOverwritten(_path, path => ReadDocument(path)))
        {
            return new AccessibilitySettingsSaveResult(
                false,
                "The existing accessibility settings are invalid and were preserved.");
        }

        var saved = JsonFile.TryWrite(_path, new AccessibilitySettingsDocument
        {
            Version = 1,
            VisualEffects = settings.VisualEffects.ToString()
        });
        return new AccessibilitySettingsSaveResult(
            saved,
            saved ? null : "Accessibility settings could not be saved.");
    }

    private static AccessibilitySettingsDocument ReadDocument(string path)
    {
        var document = JsonFile.Read<AccessibilitySettingsDocument>(path);
        if (document.Version != 1 ||
            !Enum.TryParse<VisualEffectsPreference>(document.VisualEffects, out var preference) ||
            !Enum.IsDefined(typeof(VisualEffectsPreference), preference))
        {
            throw new InvalidDataException("The accessibility settings document is unsupported.");
        }

        return document;
    }

    private sealed class AccessibilitySettingsDocument
    {
        public int Version { get; set; }
        public string VisualEffects { get; set; } = string.Empty;
    }
}
