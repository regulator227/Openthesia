using Openthesia.Core.Midi;
using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Core.Midi;

public sealed class MidiBrowserNavigationTests
{
    private readonly string _root = MidiPathsManager.NormalizePath(
        Path.Combine(Path.GetTempPath(), "Openthesia.Tests", "Navigation"));

    [Fact]
    public void BackWalksFoldersThenSearchPathsThenHome()
    {
        var navigation = new MidiBrowserNavigation();
        var firstChild = Path.Combine(_root, "First");
        var secondChild = Path.Combine(firstChild, "Second");
        navigation.OpenSearchPath(_root);
        navigation.OpenDirectory(firstChild);
        navigation.OpenDirectory(secondChild);

        var fromSecond = navigation.Back();
        Assert.False(fromSecond.ReturnHome);
        Assert.Equal(MidiPathsManager.NormalizePath(firstChild), navigation.CurrentDirectory);
        Assert.Equal(MidiPathsManager.NormalizePath(secondChild), fromSecond.FocusPath);

        var fromFirst = navigation.Back();
        Assert.False(fromFirst.ReturnHome);
        Assert.Equal(_root, navigation.CurrentDirectory);
        Assert.Equal(MidiPathsManager.NormalizePath(firstChild), fromFirst.FocusPath);

        var fromRoot = navigation.Back();
        Assert.False(fromRoot.ReturnHome);
        Assert.Equal(MidiBrowserView.SearchPaths, navigation.View);
        Assert.Equal(_root, fromRoot.FocusPath);

        var fromPaths = navigation.Back();
        Assert.True(fromPaths.ReturnHome);
    }

    [Fact]
    public void BackFromAllMidiFilesReturnsToSearchPathsAndRestoresFocus()
    {
        var navigation = new MidiBrowserNavigation();
        navigation.OpenAllMidiFiles();

        var result = navigation.Back();

        Assert.False(result.ReturnHome);
        Assert.Equal(MidiBrowserView.SearchPaths, navigation.View);
        Assert.Equal(MidiBrowserWindowKeys.AllMidiFiles, result.FocusPath);
    }

    [Fact]
    public void CannotNavigateOutsideSearchPath()
    {
        var navigation = new MidiBrowserNavigation();
        navigation.OpenSearchPath(_root);

        Assert.Throws<InvalidOperationException>(() =>
            navigation.OpenDirectory(Path.GetDirectoryName(_root)!));
    }

    [Fact]
    public void CombinedSourceReturnsToItsContainingFolder()
    {
        var source = new MidiSourceEntry(
            Path.Combine(_root, "Album", "Prelude.mid"),
            _root,
            Path.Combine("Album", "Prelude.mid"));
        var navigation = new MidiBrowserNavigation();
        navigation.OpenAllMidiFiles();

        navigation.ReturnToSourceDirectory(source);

        Assert.Equal(MidiBrowserView.Directory, navigation.View);
        Assert.Equal(_root, navigation.SearchPath);
        Assert.Equal(MidiPathsManager.NormalizePath(Path.Combine(_root, "Album")), navigation.CurrentDirectory);
    }
}
