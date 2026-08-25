namespace Openthesia.Core.Practice;

public enum PracticeCommand
{
    TogglePlayPause,
    ToggleDirection,
    ToggleNoteLabels,
    SeekBackward,
    SeekForward,
    ToggleGlow,
    Exit,
    ClearInput,
    ToggleRecording
}

public enum PracticeKey
{
    Space,
    R,
    T,
    G,
    LeftArrow,
    RightArrow,
    Escape,
    Backspace
}

public readonly record struct PracticeKeyStroke(
    PracticeKey Key,
    bool Control = false,
    bool Shift = false,
    bool Alt = false);

public readonly record struct PracticeInputContext(
    bool TextInputActive,
    bool ControlFocused,
    bool ComputerKeyboardInputEnabled,
    bool OverlayOpen = false,
    bool ControlActive = false);

public static class PracticeCommandMap
{
    public static bool TryMap(
        PracticeInputContext context,
        PracticeKeyStroke stroke,
        out PracticeCommand command)
    {
        command = default;
        if (context.TextInputActive || context.OverlayOpen)
            return false;
        if ((context.ControlFocused || context.ControlActive) && stroke.Key != PracticeKey.Escape)
            return false;
        if (context.ComputerKeyboardInputEnabled &&
            !stroke.Control &&
            !stroke.Shift &&
            !stroke.Alt &&
            stroke.Key is PracticeKey.T or PracticeKey.G)
        {
            return false;
        }

        var mapped = (stroke.Key, stroke.Control, stroke.Shift, stroke.Alt) switch
        {
            (PracticeKey.Space, false, false, false) => PracticeCommand.TogglePlayPause,
            (PracticeKey.R, false, false, false) => PracticeCommand.ToggleDirection,
            (PracticeKey.T, false, false, false) => PracticeCommand.ToggleNoteLabels,
            (PracticeKey.LeftArrow, _, false, false) => PracticeCommand.SeekBackward,
            (PracticeKey.RightArrow, _, false, false) => PracticeCommand.SeekForward,
            (PracticeKey.G, false, false, false) => PracticeCommand.ToggleGlow,
            (PracticeKey.Escape, false, false, false) => PracticeCommand.Exit,
            (PracticeKey.Backspace, false, false, false) => PracticeCommand.ClearInput,
            (PracticeKey.R, true, false, false) => PracticeCommand.ToggleRecording,
            _ => (PracticeCommand?)null
        };
        if (mapped is null)
            return false;

        command = mapped.Value;
        return true;
    }

    public static bool CanRouteComputerPianoNotes(PracticeInputContext context)
    {
        return context.ComputerKeyboardInputEnabled &&
               !context.TextInputActive &&
               !context.ControlActive &&
               !context.OverlayOpen;
    }
}
