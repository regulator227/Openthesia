namespace Openthesia.Core.Songs;

public sealed record ChartId
{
    private const string Prefix = "chart-v1-sha256:";

    private ChartId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ChartId FromHash(ReadOnlySpan<byte> hash)
    {
        if (hash.Length != 32)
            throw new ArgumentException("A Chart hash must contain 32 bytes.", nameof(hash));

        return new ChartId($"{Prefix}{Convert.ToHexString(hash).ToLowerInvariant()}");
    }

    public static ChartId Parse(string value)
    {
        if (value is null ||
            value.Length != Prefix.Length + 64 ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new FormatException("The value is not a version 1 SHA-256 Chart identity.");
        }

        foreach (var character in value.AsSpan(Prefix.Length))
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                throw new FormatException("The Chart identity hash must be lowercase hexadecimal.");
        }

        return new ChartId(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
