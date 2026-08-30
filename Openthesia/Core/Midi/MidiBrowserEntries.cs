using Openthesia.Settings;

namespace Openthesia.Core.Midi;

public enum MidiBrowserEntryKind
{
    SearchPath,
    Folder,
    MidiSource,
    AllMidiFiles
}

public sealed record MidiBrowserEntry(
    MidiBrowserEntryKind Kind,
    string Key,
    string Name,
    string? Subtext,
    string? FullPath,
    MidiSourceEntry? Source = null,
    bool IsEnabled = true);

public static class MidiBrowserEntries
{
    public static IReadOnlyList<MidiBrowserEntry> BuildDirectory(
        MidiDiscoveryResult discovery,
        string currentDirectory,
        string search,
        bool ascending)
    {
        var normalizedDirectory = MidiPathsManager.NormalizePath(currentDirectory);
        var folders = new Dictionary<string, MidiBrowserEntry>(StringComparer.OrdinalIgnoreCase);
        var sources = new List<MidiBrowserEntry>();

        foreach (var source in discovery.Sources)
        {
            var relative = Path.GetRelativePath(normalizedDirectory, source.FullPath);
            if (IsOutside(relative))
                continue;

            var separatorIndex = relative.IndexOfAny(new[]
            {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            });
            if (separatorIndex >= 0)
            {
                var folderName = relative[..separatorIndex];
                var folderPath = MidiPathsManager.NormalizePath(Path.Combine(normalizedDirectory, folderName));
                folders.TryAdd(folderPath, new MidiBrowserEntry(
                    MidiBrowserEntryKind.Folder,
                    folderPath,
                    folderName,
                    null,
                    folderPath));
                continue;
            }

            sources.Add(new MidiBrowserEntry(
                MidiBrowserEntryKind.MidiSource,
                source.FullPath,
                source.FileName,
                null,
                source.FullPath,
                source));
        }

        var folderEntries = Filter(folders.Values, search, includeSubtext: false);
        var sourceEntries = Filter(sources, search, includeSubtext: false);
        return Sort(folderEntries, ascending)
            .Concat(Sort(sourceEntries, ascending))
            .ToArray();
    }

    public static IReadOnlyList<MidiBrowserEntry> BuildAllMidiFiles(
        MidiDiscoveryResult discovery,
        string search,
        bool ascending)
    {
        return Sort(
                Filter(
                    discovery.Sources.Select(source => new MidiBrowserEntry(
                        MidiBrowserEntryKind.MidiSource,
                        source.FullPath,
                        source.FileName,
                        source.ContainingFolder,
                        source.FullPath,
                        source)),
                    search,
                    includeSubtext: true),
                ascending)
            .ToArray();
    }

    private static IEnumerable<MidiBrowserEntry> Filter(
        IEnumerable<MidiBrowserEntry> entries,
        string search,
        bool includeSubtext)
    {
        if (string.IsNullOrWhiteSpace(search))
            return entries;

        return entries.Where(entry =>
            entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (includeSubtext &&
             entry.Subtext?.Contains(search, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static IOrderedEnumerable<MidiBrowserEntry> Sort(
        IEnumerable<MidiBrowserEntry> entries,
        bool ascending)
    {
        return ascending
            ? entries
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase)
            : entries
                .OrderByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsOutside(string relativePath)
    {
        return Path.IsPathRooted(relativePath) ||
               relativePath == ".." ||
               relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
               relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
