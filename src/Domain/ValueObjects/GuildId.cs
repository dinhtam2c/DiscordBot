namespace Domain.ValueObjects;

public readonly record struct GuildId(ulong Value)
{
    public override string ToString() => Value.ToString();
}
