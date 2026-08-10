namespace Domain.ValueObjects;

public readonly record struct UserId(ulong Value)
{
    public override string ToString() => Value.ToString();
}
