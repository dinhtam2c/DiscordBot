namespace Modules.Music.Players;

internal sealed record Volume
{
    public int Value { get; init; }

    private Volume(int value)
    {
        Value = value;
    }

    public static Volume Create(int value)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100.");
        }
        return new Volume(value);
    }
}
