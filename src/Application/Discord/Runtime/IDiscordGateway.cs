namespace Application.Discord.Runtime;

public interface IDiscordGateway
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
