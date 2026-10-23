using System.Collections.Concurrent;
using System.Threading.Channels;
using Application.Discord.Voice;
using Discord.WebSocket;
using Domain.ValueObjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Discord.DiscordNet.Voice;

internal sealed class DiscordVoiceSessionManager(
    DiscordSocketClient client,
    ILogger<DiscordVoiceSessionManager> logger)
    : IVoiceSessionManager, IVoiceOutputProvider, IHostedService
{
    private static readonly TimeSpan ResumeGracePeriod = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<GuildId, GuildVoiceState> _states = new();
    private readonly CancellationTokenSource _stopping = new();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        var workers = _states.Values
            .Select(state => state.GetWorker())
            .Where(worker => worker is not null)
            .Cast<Task>()
            .ToArray();

        try
        {
            await Task.WhenAll(workers).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public IVoiceOutput GetOutput(GuildId guildId)
        => GetState(guildId).Output;

    public Task RequestJoinAsync(GuildId guildId, ChannelId voiceChannelId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stopping.Token.ThrowIfCancellationRequested();
        var state = GetState(guildId);
        state.SetDesiredChannel(voiceChannelId);
        EnsureWorker(state);
        return Task.CompletedTask;
    }

    public Task RequestLeaveAsync(GuildId guildId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _stopping.Token.ThrowIfCancellationRequested();
        if (!_states.TryGetValue(guildId, out var state))
            return Task.CompletedTask;

        state.SetDesiredChannel(null);
        EnsureWorker(state);
        return Task.CompletedTask;
    }

    private GuildVoiceState GetState(GuildId guildId)
        => _states.GetOrAdd(guildId, id => new GuildVoiceState(id));

    private void EnsureWorker(GuildVoiceState state)
    {
        state.EnsureWorker(() => Task.Run(() => RunGuildAsync(state, _stopping.Token)));
    }

    private async Task RunGuildAsync(GuildVoiceState state, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RunGuildCycleAsync(state, ct);
            if (ct.IsCancellationRequested)
                return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RunGuildCycleAsync(GuildVoiceState state, CancellationToken ct)
    {
        var retryDelay = TimeSpan.FromSeconds(1);
        var nextAttemptAt = DateTimeOffset.MinValue;
        var observedRevision = -1L;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                state.DrainSignals();
                var snapshot = state.GetSnapshot();

                if (snapshot.Revision != observedRevision)
                {
                    observedRevision = snapshot.Revision;
                    nextAttemptAt = DateTimeOffset.MinValue;
                    retryDelay = TimeSpan.FromSeconds(1);
                }

                if (snapshot.DesiredChannelId is null)
                {
                    if (snapshot.Session is not null)
                    {
                        await DisconnectSessionAsync(state, snapshot.Session);
                        continue;
                    }

                    await state.WaitForSignalAsync(ct);
                    continue;
                }

                if (snapshot.Session is { } currentSession)
                {
                    if (currentSession.VoiceChannelId != snapshot.DesiredChannelId)
                    {
                        await DisconnectSessionAsync(state, currentSession, disconnectFromDiscord: false);
                        nextAttemptAt = DateTimeOffset.MinValue;
                        retryDelay = TimeSpan.FromSeconds(1);
                        continue;
                    }

                    if (state.IsFaulted(currentSession))
                    {
                        await DisconnectSessionAsync(state, currentSession, disconnectFromDiscord: false);
                        nextAttemptAt = DateTimeOffset.UtcNow + retryDelay;
                        retryDelay = NextRetryDelay(retryDelay);
                        continue;
                    }

                    if (currentSession.IsConnected)
                    {
                        state.ClearUnavailableSince();
                        await state.Output.SetSessionAsync(currentSession);
                        await state.WaitForSignalAsync(ct);
                        continue;
                    }

                    await state.Output.SetSessionAsync(null);
                    await currentSession.ResetStreamsAsync();
                    var unavailableSince = state.GetOrSetUnavailableSince(DateTimeOffset.UtcNow);
                    var remainingGrace = ResumeGracePeriod - (DateTimeOffset.UtcNow - unavailableSince);
                    if (remainingGrace > TimeSpan.Zero)
                    {
                        await state.WaitForSignalOrDelayAsync(remainingGrace, ct);
                        continue;
                    }

                    await DisconnectSessionAsync(state, currentSession, disconnectFromDiscord: false);
                    nextAttemptAt = DateTimeOffset.UtcNow + retryDelay;
                    retryDelay = NextRetryDelay(retryDelay);
                    continue;
                }

                var delay = nextAttemptAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await state.WaitForSignalOrDelayAsync(delay, ct);
                    continue;
                }

                var requestedChannelId = snapshot.DesiredChannelId.Value;
                DiscordVoiceSession? connectedSession = null;
                try
                {
                    connectedSession = await ConnectAsync(snapshot.GuildId, requestedChannelId);
                    connectedSession.ConnectionChanged += state.Signal;

                    if (!state.IsDesired(requestedChannelId, snapshot.Revision))
                    {
                        await DisconnectUntrackedSessionAsync(connectedSession);
                        connectedSession = null;
                        continue;
                    }

                    state.SetSession(connectedSession);
                    if (connectedSession.IsConnected)
                        await state.Output.SetSessionAsync(connectedSession);

                    retryDelay = TimeSpan.FromSeconds(1);
                    nextAttemptAt = DateTimeOffset.MinValue;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    if (connectedSession is not null)
                        await DisconnectUntrackedSessionAsync(connectedSession);
                    break;
                }
                catch (Exception ex)
                {
                    if (connectedSession is not null)
                        await DisconnectUntrackedSessionAsync(connectedSession);

                    logger.LogWarning(
                        ex,
                        "Voice connection attempt failed for guild {GuildId}, channel {VoiceChannelId}.",
                        snapshot.GuildId,
                        requestedChannelId);

                    nextAttemptAt = DateTimeOffset.UtcNow + retryDelay;
                    retryDelay = NextRetryDelay(retryDelay);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice worker failed for guild {GuildId}.", state.GuildId);
        }
        finally
        {
            var session = state.GetSnapshot().Session;
            if (session is not null)
            {
                try
                {
                    await DisconnectSessionAsync(state, session);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to clean up voice session for guild {GuildId}.", state.GuildId);
                }
            }
        }
    }

    private async Task<DiscordVoiceSession> ConnectAsync(GuildId guildId, ChannelId voiceChannelId)
    {
        var guild = client.GetGuild(guildId.Value)
            ?? throw new InvalidOperationException($"Guild {guildId} is not available in the Discord cache.");
        var voiceChannel = guild.GetVoiceChannel(voiceChannelId.Value)
            ?? throw new InvalidOperationException($"Voice channel {voiceChannelId} is not available in the Discord cache.");

        var audioClient = await voiceChannel.ConnectAsync(selfDeaf: true);
        var session = new DiscordVoiceSession(guildId, voiceChannelId, audioClient);
        session.SubscribeToConnectionEvents();
        return session;
    }

    private async Task DisconnectSessionAsync(
        GuildVoiceState state,
        DiscordVoiceSession session,
        bool disconnectFromDiscord = true)
    {
        await state.Output.SetSessionAsync(null);
        state.SetSession(null);
        state.ClearFault(session);
        session.ConnectionChanged -= state.Signal;

        if (disconnectFromDiscord)
            await DisconnectUntrackedSessionAsync(session);
        else
            await session.DisposeAsync(disposeAudioClient: false);
    }

    private async Task DisconnectUntrackedSessionAsync(DiscordVoiceSession session)
    {
        var guild = client.GetGuild(session.GuildId.Value);
        var voiceChannel = guild?.GetVoiceChannel(session.VoiceChannelId.Value);
        if (voiceChannel is not null)
        {
            try
            {
                await voiceChannel.DisconnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Discord voice disconnect failed for guild {GuildId}.",
                    session.GuildId);
            }
        }

        try
        {
            await session.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Disposing Discord voice client failed for guild {GuildId}.", session.GuildId);
        }
    }

    private static TimeSpan NextRetryDelay(TimeSpan current)
        => TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2, MaximumRetryDelay.TotalSeconds));

    private sealed class GuildVoiceState
    {
        private readonly object _sync = new();
        private readonly Channel<byte> _signals = Channel.CreateBounded<byte>(
            new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite,
                AllowSynchronousContinuations = false,
            });

        private Task? _worker;
        private ChannelId? _desiredChannelId;
        private DiscordVoiceSession? _session;
        private DiscordVoiceSession? _faultedSession;
        private DateTimeOffset? _unavailableSince;
        private long _revision;

        public GuildVoiceState(GuildId guildId)
        {
            GuildId = guildId;
            Output = new DiscordVoiceOutput(MarkFaulted);
        }

        public DiscordVoiceOutput Output { get; }

        public GuildId GuildId { get; }

        public void EnsureWorker(Func<Task> startWorker)
        {
            lock (_sync)
            {
                if (_worker is null || _worker.IsCompleted)
                    _worker = startWorker();
            }
        }

        public Task? GetWorker()
        {
            lock (_sync)
                return _worker;
        }

        public void SetDesiredChannel(ChannelId? channelId)
        {
            lock (_sync)
            {
                _desiredChannelId = channelId;
                _revision++;
            }
            Signal();
        }

        public Snapshot GetSnapshot()
        {
            lock (_sync)
                return new Snapshot(GuildId, _desiredChannelId, _session, _revision);
        }

        public bool IsDesired(ChannelId channelId, long revision)
        {
            lock (_sync)
                return _desiredChannelId == channelId && _revision == revision;
        }

        public void SetSession(DiscordVoiceSession? session)
        {
            lock (_sync)
            {
                _session = session;
                if (!ReferenceEquals(_faultedSession, session))
                    _faultedSession = null;
            }
        }

        public bool IsFaulted(DiscordVoiceSession session)
        {
            lock (_sync)
                return ReferenceEquals(_faultedSession, session);
        }

        public void ClearFault(DiscordVoiceSession session)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_faultedSession, session))
                    _faultedSession = null;
            }
        }

        public void MarkFaulted(DiscordVoiceSession session)
        {
            lock (_sync)
            {
                if (ReferenceEquals(_session, session))
                    _faultedSession = session;
            }
            Signal();
        }

        public DateTimeOffset GetOrSetUnavailableSince(DateTimeOffset now)
        {
            lock (_sync)
            {
                _unavailableSince ??= now;
                return _unavailableSince.Value;
            }
        }

        public void ClearUnavailableSince()
        {
            lock (_sync)
                _unavailableSince = null;
        }

        public void Signal()
            => _signals.Writer.TryWrite(0);

        public void DrainSignals()
        {
            while (_signals.Reader.TryRead(out _))
            {
            }
        }

        public async Task WaitForSignalAsync(CancellationToken ct)
        {
            await _signals.Reader.ReadAsync(ct);
            DrainSignals();
        }

        public async Task WaitForSignalOrDelayAsync(TimeSpan delay, CancellationToken ct)
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var signalTask = _signals.Reader.ReadAsync(waitCts.Token).AsTask();
            var delayTask = Task.Delay(delay, ct);
            var completed = await Task.WhenAny(signalTask, delayTask);
            if (completed == signalTask)
            {
                await signalTask;
            }
            else
            {
                waitCts.Cancel();
                try
                {
                    await signalTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            ct.ThrowIfCancellationRequested();
            DrainSignals();
        }

        internal sealed record Snapshot(
            GuildId GuildId,
            ChannelId? DesiredChannelId,
            DiscordVoiceSession? Session,
            long Revision);
    }
}
