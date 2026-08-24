using System.Security.Cryptography;
using System.Text;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Openthesia.Core.Songs;

public static class ChartIdentity
{
    private const string Algorithm = "chart-v1-sha256";

    public static ChartId FromMidi(MidiFile midiFile)
    {
        ArgumentNullException.ThrowIfNull(midiFile);

        var tempoMap = midiFile.GetTempoMap();
        var canonicalPattern = new StringBuilder();
        canonicalPattern.AppendLine(Algorithm);

        foreach (var tempoChange in tempoMap.GetTempoChanges())
        {
            var time = TimeConverter.ConvertTo<MusicalTimeSpan>(tempoChange.Time, tempoMap);
            canonicalPattern.Append("tempo|");
            AppendFraction(canonicalPattern, time.Numerator, time.Denominator);
            canonicalPattern.Append('|');
            canonicalPattern.AppendLine(tempoChange.Value.MicrosecondsPerQuarterNote.ToString());
        }

        foreach (var note in ChartPattern.GetCanonicalNotes(midiFile))
        {
            canonicalPattern.Append("note|");
            canonicalPattern.Append((byte)note.Note.NoteNumber);
            canonicalPattern.Append('|');
            AppendFraction(canonicalPattern, note.Onset.Numerator, note.Onset.Denominator);
            canonicalPattern.Append('|');
            AppendFraction(canonicalPattern, note.Duration.Numerator, note.Duration.Denominator);
            canonicalPattern.AppendLine();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPattern.ToString()));
        return ChartId.FromHash(hash);
    }

    private static void AppendFraction(StringBuilder builder, long numerator, long denominator)
    {
        var divisor = GreatestCommonDivisor(Math.Abs(numerator), Math.Abs(denominator));
        builder.Append(numerator / divisor);
        builder.Append('/');
        builder.Append(denominator / divisor);
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left == 0 ? 1 : left;
    }

}
