using Syroot.Windows.IO;

namespace Openthesia.Settings;

public static class MidiPathsManager
{
    public static List<string> MidiPaths { get; private set; } = new()
    {
        KnownFolders.Documents.Path,
        KnownFolders.Downloads.Path,
        KnownFolders.Music.Path,
    };

    public static void LoadValidPaths(List<string> paths)
    {
        if (paths.Count == 0)
            return;
        MidiPaths.Clear();
        MidiPaths.AddRange(NormalizeDistinctPaths(paths));
    }

    public static bool TryAddPath(string path)
    {
        string normalized;
        try
        {
            normalized = NormalizePath(path);
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            NotSupportedException or
            IOException or
            System.Security.SecurityException)
        {
            return false;
        }

        if (MidiPaths.Any(existing =>
                string.Equals(NormalizePath(existing), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        MidiPaths.Add(normalized);
        return true;
    }

    public static IReadOnlyList<string> GetDistinctPaths()
    {
        return NormalizeDistinctPaths(MidiPaths);
    }

    public static IReadOnlyList<string> NormalizeDistinctPaths(IEnumerable<string> paths)
    {
        var normalizedPaths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string normalized;
            try
            {
                normalized = NormalizePath(path);
            }
            catch (Exception exception) when (exception is
                ArgumentException or
                NotSupportedException or
                IOException or
                System.Security.SecurityException)
            {
                continue;
            }

            if (seen.Add(normalized))
                normalizedPaths.Add(normalized);
        }
        return normalizedPaths;
    }

    public static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }
}
