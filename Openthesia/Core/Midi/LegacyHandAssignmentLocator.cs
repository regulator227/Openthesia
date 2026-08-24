using Openthesia.Core.Songs;

namespace Openthesia.Core.Midi;

public sealed class LegacyHandAssignmentLocator
{
    private readonly string _dataDirectory;
    private readonly string _legacyDirectory;
    private readonly IReadOnlyList<string> _midiDirectories;

    public LegacyHandAssignmentLocator(
        string dataDirectory,
        string legacyDirectory,
        IEnumerable<string> midiDirectories)
    {
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _legacyDirectory = Path.GetFullPath(legacyDirectory);
        _midiDirectories = midiDirectories.ToArray();
    }

    public LegacyHandAssignmentCandidate Find(string sourcePath)
    {
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        var legacyPath = Path.Combine(
            _legacyDirectory,
            Path.GetFileNameWithoutExtension(normalizedSourcePath) + ".xml");
        var knownSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalizedSourcePath
        };

        try
        {
            knownSources.UnionWith(new SongCatalog(_dataDirectory).GetKnownSourcePaths());
            foreach (var directory in _midiDirectories.Where(Directory.Exists))
            {
                knownSources.UnionWith(Directory.EnumerateFiles(
                    directory,
                    "*.mid",
                    SearchOption.TopDirectoryOnly).Select(Path.GetFullPath));
            }
        }
        catch (Exception)
        {
            return new LegacyHandAssignmentCandidate(legacyPath, IsUnambiguous: false);
        }

        var matchingNames = knownSources.Count(path => StringComparer.OrdinalIgnoreCase.Equals(
            Path.GetFileName(path),
            Path.GetFileName(normalizedSourcePath)));
        return new LegacyHandAssignmentCandidate(
            legacyPath,
            IsUnambiguous: matchingNames == 1);
    }
}
