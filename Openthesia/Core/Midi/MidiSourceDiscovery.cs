using Openthesia.Settings;
using System.Security;

namespace Openthesia.Core.Midi;

public sealed record MidiSourceEntry(
    string FullPath,
    string SearchPath,
    string RelativePath)
{
    public string FileName => Path.GetFileName(FullPath);
    public string ContainingFolder => Path.GetDirectoryName(FullPath) ?? SearchPath;
}

public sealed record MidiDiscoveryIssue(
    string Path,
    string Message,
    bool IsSearchPath);

public sealed record MidiDiscoveryResult(
    IReadOnlyList<MidiSourceEntry> Sources,
    IReadOnlyList<MidiDiscoveryIssue> Issues)
{
    public static MidiDiscoveryResult Empty { get; } = new(
        Array.Empty<MidiSourceEntry>(),
        Array.Empty<MidiDiscoveryIssue>());

    public bool IsSearchPathUnavailable(string path)
    {
        var normalized = MidiPathsManager.NormalizePath(path);
        return Issues.Any(issue =>
            issue.IsSearchPath &&
            string.Equals(issue.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class MidiDiscoveryProgress
{
    private int _directoriesScanned;
    private int _sourcesFound;
    private int _locationsSkipped;

    public int DirectoriesScanned => Volatile.Read(ref _directoriesScanned);
    public int SourcesFound => Volatile.Read(ref _sourcesFound);
    public int LocationsSkipped => Volatile.Read(ref _locationsSkipped);

    internal void DirectoryScanned() => Interlocked.Increment(ref _directoriesScanned);
    internal void SourceFound() => Interlocked.Increment(ref _sourcesFound);
    internal void LocationSkipped() => Interlocked.Increment(ref _locationsSkipped);
}

public static class MidiSourceDiscovery
{
    public static bool IsSupportedMidiFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".midi", StringComparison.OrdinalIgnoreCase);
    }

    public static Task<MidiDiscoveryResult> DiscoverAsync(
        IEnumerable<string> searchPaths,
        MidiDiscoveryProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        var paths = MidiPathsManager.NormalizeDistinctPaths(searchPaths);
        return Task.Run(
            () => Discover(paths, progress, cancellationToken),
            cancellationToken);
    }

    internal static MidiDiscoveryResult Discover(
        IEnumerable<string> searchPaths,
        MidiDiscoveryProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sources = new List<MidiSourceEntry>();
        var issues = new List<MidiDiscoveryIssue>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchPath in MidiPathsManager.NormalizeDistinctPaths(searchPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(searchPath))
            {
                AddIssue(searchPath, "This MIDI Search Path is unavailable.", isSearchPath: true);
                continue;
            }

            if (!TryGetAttributes(searchPath, out var searchPathAttributes, out var rootError))
            {
                AddIssue(searchPath, rootError, isSearchPath: true);
                continue;
            }
            if (searchPathAttributes.HasFlag(FileAttributes.ReparsePoint))
            {
                AddIssue(searchPath, "Directory links are not followed.", isSearchPath: true);
                continue;
            }

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(searchPath);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pendingDirectories.Pop();
                string[] entries;
                try
                {
                    entries = Directory.GetFileSystemEntries(directory);
                    progress?.DirectoryScanned();
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    AddIssue(
                        directory,
                        exception.Message,
                        isSearchPath: string.Equals(
                            directory,
                            searchPath,
                            StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!TryGetAttributes(entry, out var attributes, out var entryError))
                    {
                        AddIssue(entry, entryError, isSearchPath: false);
                        continue;
                    }
                    if (attributes.HasFlag(FileAttributes.Hidden) ||
                        attributes.HasFlag(FileAttributes.System))
                    {
                        continue;
                    }

                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                            pendingDirectories.Push(entry);
                        continue;
                    }

                    if (!IsSupportedMidiFile(entry))
                        continue;

                    var normalizedSource = MidiPathsManager.NormalizePath(entry);
                    if (!seenSources.Add(normalizedSource))
                        continue;

                    sources.Add(new MidiSourceEntry(
                        normalizedSource,
                        searchPath,
                        Path.GetRelativePath(searchPath, normalizedSource)));
                    progress?.SourceFound();
                }
            }
        }

        return new MidiDiscoveryResult(
            sources
                .OrderBy(source => source.FullPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.FullPath, StringComparer.Ordinal)
                .ToArray(),
            issues.ToArray());

        void AddIssue(string path, string message, bool isSearchPath)
        {
            issues.Add(new MidiDiscoveryIssue(
                MidiPathsManager.NormalizePath(path),
                message,
                isSearchPath));
            progress?.LocationSkipped();
        }
    }

    private static bool TryGetAttributes(
        string path,
        out FileAttributes attributes,
        out string error)
    {
        try
        {
            attributes = File.GetAttributes(path);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            attributes = default;
            error = exception.Message;
            return false;
        }
    }

    private static bool IsFileSystemException(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            SecurityException or
            ArgumentException or
            NotSupportedException;
    }
}
