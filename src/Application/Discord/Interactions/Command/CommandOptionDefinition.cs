namespace Application.Discord.Interactions.Command;

public sealed record CommandOptionDefinition(
    string Name,
    string Description,
    CommandOptionType Type,
    IReadOnlyList<CommandOptionChoice>? Choices = null,
    bool Required = true);
