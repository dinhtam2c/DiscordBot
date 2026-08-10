namespace Infrastructure.Discord;

internal interface IDiscordReadyHandler
{
    public Task OnReadyAsync(CancellationToken ct);
}
