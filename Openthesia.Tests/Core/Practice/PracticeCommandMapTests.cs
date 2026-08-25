using Openthesia.Core.Practice;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeCommandMapTests
{
    [Fact]
    public void CanvasOwnedSpaceMapsToPlayPause()
    {
        var mapped = PracticeCommandMap.TryMap(
            new PracticeInputContext(
                TextInputActive: false,
                ControlFocused: false,
                ComputerKeyboardInputEnabled: false),
            new PracticeKeyStroke(PracticeKey.Space),
            out var command);

        Assert.True(mapped);
        Assert.Equal(PracticeCommand.TogglePlayPause, command);
    }

    [Theory]
    [InlineData(PracticeKey.R, false, PracticeCommand.ToggleDirection)]
    [InlineData(PracticeKey.T, false, PracticeCommand.ToggleNoteLabels)]
    [InlineData(PracticeKey.LeftArrow, false, PracticeCommand.SeekBackward)]
    [InlineData(PracticeKey.RightArrow, false, PracticeCommand.SeekForward)]
    [InlineData(PracticeKey.G, false, PracticeCommand.ToggleGlow)]
    [InlineData(PracticeKey.Escape, false, PracticeCommand.Exit)]
    [InlineData(PracticeKey.Backspace, false, PracticeCommand.ClearInput)]
    [InlineData(PracticeKey.R, true, PracticeCommand.ToggleRecording)]
    public void CanvasShortcutsMapToPracticeCommands(
        PracticeKey key,
        bool control,
        PracticeCommand expected)
    {
        var mapped = PracticeCommandMap.TryMap(
            new PracticeInputContext(false, false, false),
            new PracticeKeyStroke(key, Control: control),
            out var command);

        Assert.True(mapped);
        Assert.Equal(expected, command);
    }

    [Fact]
    public void ComputerPianoInputOwnsNoteLettersButNotModifiedCommands()
    {
        var context = new PracticeInputContext(false, false, true);

        Assert.False(PracticeCommandMap.TryMap(
            context,
            new PracticeKeyStroke(PracticeKey.T),
            out _));
        Assert.False(PracticeCommandMap.TryMap(
            context,
            new PracticeKeyStroke(PracticeKey.G),
            out _));
        Assert.True(PracticeCommandMap.TryMap(
            context,
            new PracticeKeyStroke(PracticeKey.R, Control: true),
            out var command));
        Assert.Equal(PracticeCommand.ToggleRecording, command);
    }

    [Fact]
    public void FocusedControlsOwnActivationButEscapeReturns()
    {
        var context = new PracticeInputContext(false, true, false);

        Assert.False(PracticeCommandMap.TryMap(
            context,
            new PracticeKeyStroke(PracticeKey.Space),
            out _));
        Assert.True(PracticeCommandMap.TryMap(
            context,
            new PracticeKeyStroke(PracticeKey.Escape),
            out var command));
        Assert.Equal(PracticeCommand.Exit, command);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TextInputAndOverlaysOwnEscape(bool textInput, bool overlayOpen)
    {
        var context = new PracticeInputContext(
            TextInputActive: textInput,
            ControlFocused: false,
            ComputerKeyboardInputEnabled: false,
            OverlayOpen: overlayOpen);

        Assert.False(PracticeCommandMap.TryMap(
            context,
            new PracticeKeyStroke(PracticeKey.Escape),
            out _));
    }

    [Fact]
    public void PassiveNavigationFocusDoesNotDisableComputerPianoNotes()
    {
        var focused = new PracticeInputContext(
            TextInputActive: false,
            ControlFocused: true,
            ComputerKeyboardInputEnabled: true,
            OverlayOpen: false,
            ControlActive: false);
        var active = focused with { ControlActive = true };

        Assert.True(PracticeCommandMap.CanRouteComputerPianoNotes(focused));
        Assert.False(PracticeCommandMap.CanRouteComputerPianoNotes(active));
    }
}
