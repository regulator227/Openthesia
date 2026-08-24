using Openthesia.Core.Songs;

namespace Openthesia.Core.Midi;

public sealed record MidiLoadContext
{
    private MidiLoadContext(
        ChartId chartId,
        string? sourcePath,
        ResolvedSongChart? songChart)
    {
        ChartId = chartId;
        SourcePath = sourcePath;
        SongChart = songChart;
    }

    public ChartId ChartId { get; }
    public string? SourcePath { get; }
    public ResolvedSongChart? SongChart { get; }
    public bool IsTransient => SourcePath is null;

    public static MidiLoadContext Transient(ChartId chartId)
    {
        return new MidiLoadContext(chartId, sourcePath: null, songChart: null);
    }

    public static MidiLoadContext FromSource(
        string sourcePath,
        ChartId chartId,
        ResolvedSongChart? songChart)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A MIDI source path is required.", nameof(sourcePath));
        if (songChart is not null && songChart.Chart.Id != chartId)
            throw new ArgumentException("The resolved Chart must match the MIDI pattern.", nameof(songChart));

        return new MidiLoadContext(
            chartId,
            Path.GetFullPath(sourcePath),
            songChart);
    }
}
