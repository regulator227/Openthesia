namespace Openthesia.Core.Songs;

public readonly record struct SongId(Guid Value)
{
    public static SongId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
