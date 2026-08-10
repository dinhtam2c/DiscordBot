using Application.Discord.Runtime;

namespace Bot;

internal class DiscordBotService(
    IDiscordGateway gateway,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting Discord bot...");

        await gateway.StartAsync(ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }

        logger.LogInformation("Stopping Discord bot...");

        await gateway.StopAsync(ct);
    }
}
