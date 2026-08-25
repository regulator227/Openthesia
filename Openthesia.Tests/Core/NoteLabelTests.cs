using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Interaction;
using Openthesia.Enums;
using Openthesia.Ui.Helpers;
using Xunit;

namespace Openthesia.Tests.Core;

public sealed class NoteLabelTests
{
    [Fact]
    public void PitchAndOctaveUsesACompactVisibleLabel()
    {
        var note = new Note(new SevenBitNumber(61));

        var label = Drawings.GetNoteTextAs(TextTypes.PitchAndOctave, note);

        Assert.Equal("C#4", label);
    }
}
