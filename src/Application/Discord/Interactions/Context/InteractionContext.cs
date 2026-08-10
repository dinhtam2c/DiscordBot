using Application.Discord.Interactions.Response;
using Domain.ValueObjects;

namespace Application.Discord.Interactions.Context;

public sealed record InteractionContext(
    string CommandName,
    UserId UserId,
    ChannelId? ChannelId,
    GuildId? GuildId,
    InteractionOptions Options,
    IInteractionResponder Responder);
