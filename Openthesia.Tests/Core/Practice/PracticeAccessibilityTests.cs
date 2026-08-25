using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Practice;

public sealed class PracticeAccessibilityTests
{
    [Fact]
    public void CurrentTargetNamesPitchOctaveHandAndState()
    {
        var onset = ChartTime.FromMicroseconds(1_000_000);
        var chart = new PracticeChart(
            ChartId.Parse($"chart-v1-sha256:{new string('a', 64)}"),
            ChartTime.FromMicroseconds(4_000_000),
            new[]
            {
                new PracticeChartNote(1, 61, onset, ChartTime.FromMicroseconds(500_000), PianoHand.Left),
                new PracticeChartNote(2, 72, onset, ChartTime.FromMicroseconds(500_000), PianoHand.Right)
            });
        var snapshot = new PracticeSessionSnapshot(
            PracticeSessionState.WaitingForInput,
            onset,
            new PracticeTarget(onset, new byte[] { 72, 61 }));

        var description = PracticeAccessibility.Describe(
            chart,
            snapshot,
            Array.Empty<PracticeFeedback>(),
            PracticeNavigation.Empty,
            activeLoop: null);

        Assert.Equal(
            "Current target: C#4 · Left + C5 · Right",
            description.TargetText);
    }

    [Fact]
    public void TimingFeedbackNamesThePitchAndJudgmentWithoutColor()
    {
        var onset = ChartTime.FromMicroseconds(1_000_000);
        var chart = new PracticeChart(
            ChartId.Parse($"chart-v1-sha256:{new string('b', 64)}"),
            ChartTime.FromMicroseconds(4_000_000),
            Array.Empty<PracticeChartNote>());
        var snapshot = new PracticeSessionSnapshot(
            PracticeSessionState.Running,
            onset,
            Target: null);

        var description = PracticeAccessibility.Describe(
            chart,
            snapshot,
            new[] { new PracticeFeedback(64, onset, TimingJudgment.Early, -45_000) },
            PracticeNavigation.Empty,
            activeLoop: null);

        Assert.Equal("Early: E4 · 45 ms", description.FeedbackText);
    }

    [Fact]
    public void NavigationDescriptionNamesTheActiveLoopAndNextBookmark()
    {
        var chart = new PracticeChart(
            ChartId.Parse($"chart-v1-sha256:{new string('c', 64)}"),
            ChartTime.FromMicroseconds(4_000_000),
            Array.Empty<PracticeChartNote>());
        var loop = new PracticeLoop(
            Guid.NewGuid(),
            "Chorus",
            new PracticeRange(
                ChartTime.FromMicroseconds(1_000_000),
                ChartTime.FromMicroseconds(2_000_000)));
        var navigation = new PracticeNavigation(
            new[] { loop },
            new[]
            {
                new PracticeBookmark(
                    Guid.NewGuid(),
                    "Coda",
                    ChartTime.FromMicroseconds(3_000_000))
            });
        var snapshot = new PracticeSessionSnapshot(
            PracticeSessionState.Running,
            ChartTime.FromMicroseconds(1_500_000),
            Target: null);

        var description = PracticeAccessibility.Describe(
            chart,
            snapshot,
            Array.Empty<PracticeFeedback>(),
            navigation,
            loop);

        Assert.Equal(
            "Active loop: Chorus · Next bookmark: Coda at 00:03",
            description.NavigationText);
    }

    [Fact]
    public void RunningModesDescribeTheNextRequiredTarget()
    {
        var chart = new PracticeChart(
            ChartId.Parse($"chart-v1-sha256:{new string('d', 64)}"),
            ChartTime.FromMicroseconds(4_000_000),
            new[]
            {
                new PracticeChartNote(
                    1,
                    48,
                    ChartTime.FromMicroseconds(1_000_000),
                    ChartTime.FromMicroseconds(500_000),
                    PianoHand.Left),
                new PracticeChartNote(
                    2,
                    64,
                    ChartTime.FromMicroseconds(2_000_000),
                    ChartTime.FromMicroseconds(500_000),
                    PianoHand.Right)
            });
        var snapshot = new PracticeSessionSnapshot(
            PracticeSessionState.Running,
            ChartTime.FromMicroseconds(1_500_000),
            Target: null);

        var description = PracticeAccessibility.Describe(
            chart,
            snapshot,
            Array.Empty<PracticeFeedback>(),
            PracticeNavigation.Empty,
            activeLoop: null,
            RequiredHands.Right);

        Assert.Equal("Next target: E4 · Right", description.TargetText);
    }

    [Theory]
    [InlineData(RequiredHands.Left, "Current target: C4 · Left")]
    [InlineData(RequiredHands.Right, "Current target: C4 · Right")]
    public void CurrentTargetUsesTheRequiredHandForOverlappingPitch(
        RequiredHands requiredHands,
        string expected)
    {
        var onset = ChartTime.FromMicroseconds(1_000_000);
        var chart = new PracticeChart(
            ChartId.Parse($"chart-v1-sha256:{new string('e', 64)}"),
            ChartTime.FromMicroseconds(4_000_000),
            new[]
            {
                new PracticeChartNote(1, 60, onset, ChartTime.FromMicroseconds(500_000), PianoHand.Left),
                new PracticeChartNote(2, 60, onset, ChartTime.FromMicroseconds(500_000), PianoHand.Right)
            });
        var snapshot = new PracticeSessionSnapshot(
            PracticeSessionState.WaitingForInput,
            onset,
            new PracticeTarget(onset, new byte[] { 60 }));

        var description = PracticeAccessibility.Describe(
            chart,
            snapshot,
            Array.Empty<PracticeFeedback>(),
            PracticeNavigation.Empty,
            activeLoop: null,
            requiredHands);

        Assert.Equal(expected, description.TargetText);
    }
}
