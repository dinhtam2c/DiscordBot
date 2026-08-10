namespace Domain.ValueObjects;

public readonly record struct RoleId(ulong Value)
{
    public override string ToString() => Value.ToString();
}
