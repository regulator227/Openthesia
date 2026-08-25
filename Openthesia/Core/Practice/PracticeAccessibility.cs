using Openthesia.Core.Songs;

namespace Openthesia.Core.Practice;

public sealed record PracticeAccessibilityDescription(
    string TargetText,
    string FeedbackText,
    string NavigationText);

public static class PracticeAccessibility
{
    private static readonly string[] PitchClasses =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    public static PracticeAccessibilityDescription Describe(
        PracticeChart chart,
        PracticeSessionSnapshot snapshot,
        IReadOnlyList<PracticeFeedback> feedback,
        PracticeNavigation navigation,
        PracticeLoop? activeLoop)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(navigation);

        var targetParts = snapshot.Target?.Pitches
                .Distinct()
                .OrderBy(pitch => pitch)
                .Select(pitch => $"{PitchName(pitch)} · {HandName(chart, snapshot.Target!.Onset, pitch)}")
                .ToArray() ?? Array.Empty<string>();
        var targetText = targetParts.Length == 0
            ? string.Empty
            : $"Current target: {string.Join(" + ", targetParts)}";
        var feedbackText = string.Join(
            " | ",
            feedback.Select(FormatFeedback));
        var navigationParts = new List<string>();
        if (activeLoop is not null)
            navigationParts.Add($"Active loop: {activeLoop.Name}");
        var nextBookmark = navigation.FindBookmark(
            snapshot.Position,
            PracticeNavigationDirection.Next);
        if (nextBookmark is not null)
        {
            navigationParts.Add(
                $"Next bookmark: {nextBookmark.Name} at {FormatTime(nextBookmark.Position)}");
        }

        return new PracticeAccessibilityDescription(
            targetText,
            feedbackText,
            string.Join(" · ", navigationParts));
    }

    public static string PitchName(byte pitch)
    {
        return $"{PitchClasses[pitch % 12]}{pitch / 12 - 1}";
    }

    private static string HandName(PracticeChart chart, ChartTime onset, byte pitch)
    {
        var hands = chart.Notes
            .Where(note => note.Onset == onset && note.Pitch == pitch)
            .Select(note => note.Hand)
            .Distinct()
            .ToArray();
        return hands.Length switch
        {
            0 => "Required",
            > 1 => "Both",
            _ => hands[0] == PianoHand.Left ? "Left" : "Right"
        };
    }

    private static string FormatFeedback(PracticeFeedback feedback)
    {
        var timing = feedback.SignedOffsetMicroseconds is { } offset &&
                     feedback.Judgment is TimingJudgment.Early or TimingJudgment.Late
            ? $" · {Math.Abs(offset) / 1_000d:0} ms"
            : string.Empty;
        return $"{feedback.Judgment}: {PitchName(feedback.Pitch)}{timing}";
    }

    private static string FormatTime(ChartTime time)
    {
        var value = TimeSpan.FromMilliseconds(time.Microseconds / 1_000d);
        return $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }
}
