using Openthesia.Core.Songs;
using System.Runtime.CompilerServices;

namespace Openthesia.Core.Practice;

public sealed record PracticeAccessibilityDescription(
    string TargetText,
    string FeedbackText,
    string NavigationText);

public static class PracticeAccessibility
{
    private static readonly ConditionalWeakTable<PracticeChart, ChartIndex> ChartIndexes = new();
    private static readonly string[] PitchClasses =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    public static PracticeAccessibilityDescription Describe(
        PracticeChart chart,
        PracticeSessionSnapshot snapshot,
        IReadOnlyList<PracticeFeedback> feedback,
        PracticeNavigation navigation,
        PracticeLoop? activeLoop,
        RequiredHands requiredHands = RequiredHands.Both,
        PracticeTarget? nextTarget = null)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(navigation);

        var chartIndex = ChartIndexes.GetValue(chart, value => new ChartIndex(value));
        nextTarget ??= chartIndex.NextRequiredTarget(
            snapshot.Position,
            requiredHands,
            activeLoop?.Range);
        var nextBookmark = navigation.FindBookmark(
            snapshot.Position,
            PracticeNavigationDirection.Next);
        return DescribePrepared(
            chart,
            snapshot,
            feedback,
            activeLoop,
            requiredHands,
            nextTarget,
            nextBookmark);
    }

    internal static void Prepare(PracticeChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ChartIndexes.GetValue(chart, value => new ChartIndex(value));
    }

    internal static PracticeAccessibilityDescription DescribePrepared(
        PracticeChart chart,
        PracticeSessionSnapshot snapshot,
        IReadOnlyList<PracticeFeedback> feedback,
        PracticeLoop? activeLoop,
        RequiredHands requiredHands,
        PracticeTarget? nextTarget,
        PracticeBookmark? nextBookmark)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(feedback);

        var chartIndex = ChartIndexes.GetValue(chart, value => new ChartIndex(value));
        var target = snapshot.Target ?? nextTarget;
        var targetParts = target?.Pitches
                .Distinct()
                .OrderBy(pitch => pitch)
                .Select(pitch => $"{PitchName(pitch)} · {chartIndex.HandName(target!.Onset, pitch, requiredHands)}")
                .ToArray() ?? Array.Empty<string>();
        var targetText = targetParts.Length == 0
            ? string.Empty
            : $"{(snapshot.Target is null ? "Next" : "Current")} target: {string.Join(" + ", targetParts)}";
        var feedbackText = string.Join(
            " | ",
            feedback.Select(FormatFeedback));
        var navigationParts = new List<string>();
        if (activeLoop is not null)
            navigationParts.Add($"Active loop: {activeLoop.Name}");
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

    private static bool IsRequired(PianoHand hand, RequiredHands requiredHands)
    {
        return requiredHands == RequiredHands.Both ||
               requiredHands == RequiredHands.Left && hand == PianoHand.Left ||
               requiredHands == RequiredHands.Right && hand == PianoHand.Right;
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

    private sealed class ChartIndex
    {
        private readonly IndexedOnset[] _onsets;
        private readonly IReadOnlyDictionary<ChartTime, IndexedOnset> _byOnset;

        public ChartIndex(PracticeChart chart)
        {
            _onsets = chart.Notes
                .GroupBy(note => note.Onset)
                .OrderBy(group => group.Key)
                .Select(group => new IndexedOnset(group.Key, group.ToArray()))
                .ToArray();
            _byOnset = _onsets.ToDictionary(onset => onset.Onset);
        }

        public PracticeTarget? NextRequiredTarget(
            ChartTime position,
            RequiredHands requiredHands,
            PracticeRange? range)
        {
            if (range is not null && position.CompareTo(range.End) > 0)
                return null;

            var searchPosition = range is not null && position.CompareTo(range.Start) < 0
                ? range.Start
                : position;
            for (var index = LowerBound(searchPosition); index < _onsets.Length; index++)
            {
                var onset = _onsets[index];
                if (range is not null && !range.Contains(onset.Onset))
                {
                    if (onset.Onset.CompareTo(range.End) > 0)
                        return null;
                    continue;
                }

                var pitches = onset.Notes
                    .Where(note => IsRequired(note.Hand, requiredHands))
                    .Select(note => note.Pitch)
                    .Distinct()
                    .OrderBy(pitch => pitch)
                    .ToArray();
                if (pitches.Length > 0)
                    return new PracticeTarget(onset.Onset, pitches);
            }

            return null;
        }

        public string HandName(
            ChartTime onset,
            byte pitch,
            RequiredHands requiredHands)
        {
            if (!_byOnset.TryGetValue(onset, out var indexedOnset))
                return "Required";

            var hands = indexedOnset.Notes
                .Where(note => note.Pitch == pitch && IsRequired(note.Hand, requiredHands))
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

        private int LowerBound(ChartTime position)
        {
            var low = 0;
            var high = _onsets.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (_onsets[middle].Onset.CompareTo(position) < 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }
    }

    private sealed record IndexedOnset(
        ChartTime Onset,
        IReadOnlyList<PracticeChartNote> Notes);
}
