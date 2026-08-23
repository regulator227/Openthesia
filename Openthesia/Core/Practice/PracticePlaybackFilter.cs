namespace Openthesia.Core.Practice;

public static class PracticePlaybackFilter
{
    private static HashSet<int> _audibleChartNoteIds = new();

    public static bool IsEnabled { get; private set; }

    public static void Configure(IReadOnlyList<int> audibleChartNoteIds)
    {
        ArgumentNullException.ThrowIfNull(audibleChartNoteIds);
        _audibleChartNoteIds = audibleChartNoteIds.ToHashSet();
        IsEnabled = true;
    }

    public static bool AllowsAll(IEnumerable<int> chartNoteIds)
    {
        ArgumentNullException.ThrowIfNull(chartNoteIds);
        return !IsEnabled || chartNoteIds.All(_audibleChartNoteIds.Contains);
    }

    public static void Disable()
    {
        _audibleChartNoteIds.Clear();
        IsEnabled = false;
    }
}
