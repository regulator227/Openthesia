namespace Openthesia.Core.Songs;

public readonly record struct LearnerId(Guid Value)
{
    public static LearnerId New()
    {
        return new LearnerId(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("N");
    }
}
