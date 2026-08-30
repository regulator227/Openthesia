using Openthesia.Settings;

namespace Openthesia.Core.Midi;

public enum MidiBrowserView
{
    SearchPaths,
    Directory,
    AllMidiFiles
}

public sealed record MidiBrowserBackResult(
    bool ReturnHome,
    string? FocusPath);

public sealed class MidiBrowserNavigation
{
    public MidiBrowserView View { get; private set; } = MidiBrowserView.SearchPaths;
    public string? SearchPath { get; private set; }
    public string? CurrentDirectory { get; private set; }

    public void Reset()
    {
        View = MidiBrowserView.SearchPaths;
        SearchPath = null;
        CurrentDirectory = null;
    }

    public void OpenAllMidiFiles()
    {
        View = MidiBrowserView.AllMidiFiles;
        SearchPath = null;
        CurrentDirectory = null;
    }

    public void OpenSearchPath(string searchPath)
    {
        var normalized = MidiPathsManager.NormalizePath(searchPath);
        View = MidiBrowserView.Directory;
        SearchPath = normalized;
        CurrentDirectory = normalized;
    }

    public void OpenDirectory(string directory)
    {
        if (View != MidiBrowserView.Directory || SearchPath is null)
            throw new InvalidOperationException("Open a MIDI Search Path before opening one of its folders.");

        var normalized = MidiPathsManager.NormalizePath(directory);
        if (!IsWithinSearchPath(SearchPath, normalized))
            throw new InvalidOperationException("MIDI browsing cannot leave the configured MIDI Search Path.");
        CurrentDirectory = normalized;
    }

    public void ReturnToSourceDirectory(MidiSourceEntry source)
    {
        OpenSearchPath(source.SearchPath);
        OpenDirectory(source.ContainingFolder);
    }

    public MidiBrowserBackResult Back()
    {
        if (View == MidiBrowserView.SearchPaths)
            return new MidiBrowserBackResult(ReturnHome: true, FocusPath: null);

        if (View == MidiBrowserView.AllMidiFiles)
        {
            Reset();
            return new MidiBrowserBackResult(ReturnHome: false, FocusPath: MidiBrowserWindowKeys.AllMidiFiles);
        }

        var searchPath = SearchPath!;
        var currentDirectory = CurrentDirectory!;
        if (string.Equals(currentDirectory, searchPath, StringComparison.OrdinalIgnoreCase))
        {
            Reset();
            return new MidiBrowserBackResult(ReturnHome: false, FocusPath: searchPath);
        }

        var parent = Directory.GetParent(currentDirectory)?.FullName;
        if (parent is null || !IsWithinSearchPath(searchPath, parent))
            parent = searchPath;
        CurrentDirectory = MidiPathsManager.NormalizePath(parent);
        return new MidiBrowserBackResult(ReturnHome: false, FocusPath: currentDirectory);
    }

    private static bool IsWithinSearchPath(string searchPath, string candidate)
    {
        var relative = Path.GetRelativePath(searchPath, candidate);
        return relative == "." ||
               (!Path.IsPathRooted(relative) &&
                relative != ".." &&
                !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }
}

public static class MidiBrowserWindowKeys
{
    public const string AllMidiFiles = "all-midi-files";
}
