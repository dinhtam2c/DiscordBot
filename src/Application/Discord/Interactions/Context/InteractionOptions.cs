namespace Application.Discord.Interactions.Context;

public sealed class InteractionOptions(IReadOnlyDictionary<string, object> options)
{
    public T GetRequired<T>(string optionName)
    {
        if (!options.TryGetValue(optionName, out var value))
        {
            throw new InvalidOperationException($"Option '{optionName}' not found.");
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        if (typeof(T).IsEnum && value is string enumName)
        {
            if (Enum.TryParse(typeof(T), enumName, ignoreCase: true, out var result))
            {
                return (T)result!;
            }
        }

        throw new InvalidOperationException(
            $"Option '{optionName}' has incorrect type. Expected {typeof(T).Name}, actual {value?.GetType().Name ?? "null"}.");
    }

    public T? GetOptional<T>(string optionName)
    {
        if (!options.TryGetValue(optionName, out var value))
        {
            return default;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        if (typeof(T).IsEnum && value is string enumName)
        {
            if (Enum.TryParse(typeof(T), enumName, ignoreCase: true, out var result))
            {
                return (T)result!;
            }
        }

        return default;
    }
}
