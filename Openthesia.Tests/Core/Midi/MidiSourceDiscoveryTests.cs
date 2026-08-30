using Openthesia.Core.Midi;
using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Core.Midi;

public sealed class MidiSourceDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void DiscoversSupportedMidiExtensionsRecursively()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Collection", "Nested")).FullName;
        var directMidi = WriteFile(Path.Combine(_root, "Direct.mid"));
        var upperMidi = WriteFile(Path.Combine(_root, "Collection", "Upper.MID"));
        var longExtensionMidi = WriteFile(Path.Combine(nested, "Long.MiDi"));
        WriteFile(Path.Combine(nested, "Notes.txt"));

        var result = MidiSourceDiscovery.Discover(new[] { _root });

        Assert.Empty(result.Issues);
        Assert.Equal(
            new[] { directMidi, upperMidi, longExtensionMidi }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            result.Sources.Select(source => source.FullPath));
        Assert.All(result.Sources, source => Assert.Equal(
            MidiPathsManager.NormalizePath(_root),
            source.SearchPath));
    }

    [Fact]
    public void ExcludesHiddenItemsAndKeepsBrokenMidiSourcesDiscoverable()
    {
        Directory.CreateDirectory(_root);
        var visibleBrokenMidi = WriteFile(Path.Combine(_root, "Broken.mid"));
        var hiddenMidi = WriteFile(Path.Combine(_root, "Hidden.mid"));
        File.SetAttributes(hiddenMidi, File.GetAttributes(hiddenMidi) | FileAttributes.Hidden);
        var hiddenFolder = Directory.CreateDirectory(Path.Combine(_root, "Hidden folder"));
        WriteFile(Path.Combine(hiddenFolder.FullName, "Nested.mid"));
        File.SetAttributes(hiddenFolder.FullName, hiddenFolder.Attributes | FileAttributes.Hidden);

        var result = MidiSourceDiscovery.Discover(new[] { _root });

        var source = Assert.Single(result.Sources);
        Assert.Equal(visibleBrokenMidi, source.FullPath);
    }

    [Fact]
    public void DeduplicatesAFileReachedThroughOverlappingSearchPaths()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "Nested")).FullName;
        var sourcePath = WriteFile(Path.Combine(nested, "Only once.mid"));

        var result = MidiSourceDiscovery.Discover(new[] { _root, nested });

        var source = Assert.Single(result.Sources);
        Assert.Equal(sourcePath, source.FullPath);
        Assert.Equal(MidiPathsManager.NormalizePath(_root), source.SearchPath);
    }

    [Fact]
    public void KeepsValidResultsWhenAnotherSearchPathIsUnavailable()
    {
        Directory.CreateDirectory(_root);
        var sourcePath = WriteFile(Path.Combine(_root, "Available.mid"));
        var missing = Path.Combine(_root, "Missing");

        var result = MidiSourceDiscovery.Discover(new[] { missing, _root });

        Assert.Equal(sourcePath, Assert.Single(result.Sources).FullPath);
        var issue = Assert.Single(result.Issues);
        Assert.True(issue.IsSearchPath);
        Assert.Equal(MidiPathsManager.NormalizePath(missing), issue.Path);
        Assert.True(result.IsSearchPathUnavailable(missing));
    }

    [Fact]
    public void DirectoryEntriesShowOnlyImmediateFoldersThatLeadToMidiSources()
    {
        var root = MidiPathsManager.NormalizePath(_root);
        var direct = Entry(root, "Zebra.mid");
        var nested = Entry(root, Path.Combine("Album", "Disc", "Alpha.mid"));
        var discovery = new MidiDiscoveryResult(
            new[] { direct, nested },
            Array.Empty<MidiDiscoveryIssue>());

        var entries = MidiBrowserEntries.BuildDirectory(discovery, root, string.Empty, ascending: true);

        Assert.Collection(
            entries,
            folder =>
            {
                Assert.Equal(MidiBrowserEntryKind.Folder, folder.Kind);
                Assert.Equal("Album", folder.Name);
            },
            source =>
            {
                Assert.Equal(MidiBrowserEntryKind.MidiSource, source.Kind);
                Assert.Equal("Zebra.mid", source.Name);
            });
    }

    [Fact]
    public void AllMidiFilesSearchesPathAndSortsByFilenameThenPath()
    {
        var root = MidiPathsManager.NormalizePath(_root);
        var first = Entry(root, Path.Combine("Second", "Prelude.mid"));
        var second = Entry(root, Path.Combine("First", "Prelude.mid"));
        var third = Entry(root, Path.Combine("Other", "Waltz.mid"));
        var discovery = new MidiDiscoveryResult(
            new[] { first, second, third },
            Array.Empty<MidiDiscoveryIssue>());

        var all = MidiBrowserEntries.BuildAllMidiFiles(discovery, string.Empty, ascending: true);
        var filtered = MidiBrowserEntries.BuildAllMidiFiles(discovery, "Second", ascending: true);

        Assert.Equal(
            new[] { second.FullPath, first.FullPath, third.FullPath },
            all.Select(entry => entry.FullPath));
        Assert.Equal(first.FullPath, Assert.Single(filtered).FullPath);
        Assert.Equal(first.ContainingFolder, Assert.Single(filtered).Subtext);
    }

    [Fact]
    public void NormalizedSearchPathsRemoveCaseAndTrailingSeparatorDuplicates()
    {
        Directory.CreateDirectory(_root);

        var paths = MidiPathsManager.NormalizeDistinctPaths(new[]
        {
            _root,
            _root + Path.DirectorySeparatorChar,
            _root.ToUpperInvariant()
        });

        Assert.Single(paths);
    }

    private MidiSourceEntry Entry(string searchPath, string relativePath)
    {
        var fullPath = MidiPathsManager.NormalizePath(Path.Combine(searchPath, relativePath));
        return new MidiSourceEntry(fullPath, searchPath, relativePath);
    }

    private static string WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return MidiPathsManager.NormalizePath(path);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
            return;

        foreach (var path in Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }
}
