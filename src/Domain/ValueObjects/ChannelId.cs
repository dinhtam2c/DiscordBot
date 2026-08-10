namespace Domain.ValueObjects;

public readonly record struct ChannelId(ulong Value)
{
    public override string ToString() => Value.ToString();
}
