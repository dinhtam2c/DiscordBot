using Application.Discord.Runtime;
using Application.Abstractions;
using Discord;
using Discord.LibDave;
using Discord.LibDave.Binding;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordGateway(
    IOptions<DiscordOptions> options,
    IAppLifetime appLifetime,
    DiscordSocketClient client,
    IEnumerable<IDiscordEventSubscriber> subscribers,
    IEnumerable<IDiscordReadyHandler> readyHandlers,
    ILogger<DiscordGateway> logger) : IDiscordGateway
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Beware: multiple start
        Dave.SetLogSink(DaveLogSink);
        client.Log += Log;
        client.Ready += HandleClientReadyAsync;

        foreach (var subscriber in subscribers)
        {
            subscriber.Subscribe();
        }

        await client.LoginAsync(TokenType.Bot, options.Value.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await client.StopAsync();
    }

    private async Task HandleClientReadyAsync()
    {
        foreach (var handler in readyHandlers)
        {
            try
            {
                await handler.OnReadyAsync(appLifetime.Stopping);
            }
            catch (OperationCanceledException) when (appLifetime.Stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Discord ready handler {HandlerType} failed.", handler.GetType().Name);
            }
        }
    }

    private Task Log(LogMessage message)
    {
        LogLevel logLevel = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        logger.Log(logLevel, message.Exception, "{Message}", message.Message);

        return Task.CompletedTask;
    }

    private void DaveLogSink(LoggingSeverity severity, string filePath, int lineNumber, string message)
    {
        LogLevel logLevel = severity switch
        {
            LoggingSeverity.Error => LogLevel.Error,
            LoggingSeverity.Warning => LogLevel.Warning,
            LoggingSeverity.Info => LogLevel.Information,
            LoggingSeverity.Verbose => LogLevel.Debug,
            LoggingSeverity.None => LogLevel.None,
            _ => LogLevel.Information
        };

        //logger.Log(logLevel, "{FilePath}:{LineNumber} - {Message}", filePath, lineNumber, message);
    }
}
