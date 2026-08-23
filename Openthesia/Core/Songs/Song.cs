namespace Openthesia.Core.Songs;

public sealed record Song(SongId Id, string Title, IReadOnlyList<ChartId> ChartIds);
