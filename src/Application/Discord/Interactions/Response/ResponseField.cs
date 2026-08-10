namespace Application.Discord.Interactions.Response;

public sealed record ResponseField(
    string Name,
    string Value,
    bool Inline = false);
