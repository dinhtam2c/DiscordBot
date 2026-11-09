using Discord;
using Discord.LibDave;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordGateway(
    IOptions<DiscordOptions> options,
    IHostApplicationLifetime lifetime,
    DiscordSocketClient client,
    DiscordInteractionAdapter interactions,
    DiscordCommandRegistrar registrar,
    ILogger<DiscordGateway> logger) : IHostedService, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _shutdown = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
    private Task? _startTask;
    private Task? _stopTask;
    private Task? _synchronizationTask;

    public Task StartAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            if (_stopTask is not null)
                throw new InvalidOperationException("The Discord gateway has already stopped.");
            return _startTask ??= StartCoreAsync(ct);
        }
    }

    private async Task StartCoreAsync(CancellationToken ct)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        logger.LogInformation("Starting Discord gateway...");
        await registrar.LoadModulesAsync(startup.Token);
        startup.Token.ThrowIfCancellationRequested();

        Dave.SetLogSink(static (_, _, _, _) => { });
        client.Log += Log;
        client.Ready += HandleReadyAsync;
        client.InteractionCreated += interactions.HandleAsync;

        await client.LoginAsync(TokenType.Bot, options.Value.Token);
        startup.Token.ThrowIfCancellationRequested();
        await client.StartAsync();
    }

    private Task HandleReadyAsync()
    {
        lock (_sync)
        {
            if (!_shutdown.IsCancellationRequested && _stopTask is null)
                _synchronizationTask ??= Task.Run(SynchronizeCommandsAsync);
        }
        return Task.CompletedTask;
    }

    private async Task SynchronizeCommandsAsync()
    {
        var ct = _shutdown.Token;
        var retryDelay = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await registrar.SynchronizeAsync(ct);
                logger.LogInformation("Discord commands are synchronized.");
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not synchronize Discord commands; retrying in {RetrySeconds} seconds.", retryDelay.TotalSeconds);
            }
            await Task.Delay(retryDelay, ct);
            retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 30));
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        lock (_sync)
            return (_stopTask ??= StopCoreAsync()).WaitAsync(ct);
    }

    private async Task StopCoreAsync()
    {
        await _shutdown.CancelAsync();
        // A stop during startup must not race login/event attachment.
        if (_startTask is not null)
        {
            try { await _startTask; }
            catch { /* Startup failures are reported by the host; cleanup still runs. */ }
        }

        client.Ready -= HandleReadyAsync;
        client.InteractionCreated -= interactions.HandleAsync;
        if (_synchronizationTask is not null)
        {
            try { await _synchronizationTask; }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        }

        logger.LogInformation("Stopping Discord gateway...");
        try { await client.StopAsync(); }
        finally { client.Log -= Log; }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _shutdown.Dispose();
    }

    private Task Log(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
        logger.Log(level, message.Exception, "{Message}", message.Message);
        return Task.CompletedTask;
    }
}
