using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Contracts.Realtime;
using Platform.Domain.Projects;
using Platform.Realtime.Connections;
using Platform.Realtime.Diagnostics;
using Platform.Realtime.Matchmaking;
using Platform.Realtime.Protocol;
using Platform.Realtime.Rooms;
using Platform.Realtime.State;

namespace Platform.Realtime.Transport;

/// <summary>
/// Owns a single accepted WebSocket for its whole lifetime: registers the connection,
/// sends the welcome message, pumps the receive loop (dispatching protocol messages),
/// and cleans up on disconnect.
/// </summary>
internal sealed class RealtimeConnectionHandler
{
    private const int ReceiveBufferSize = 4096;

    private readonly ConnectionManager _connections;
    private readonly MessageDispatcher _dispatcher;
    private readonly RoomService _rooms;
    private readonly MatchmakingService _matchmaking;
    private readonly StateService _state;
    private readonly SessionTracker _sessions;
    private readonly RealtimeActivity _activity;
    private readonly RealtimeMetrics _metrics;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly RealtimeOptions _options;
    private readonly ILogger<RealtimeConnectionHandler> _logger;

    public RealtimeConnectionHandler(
        ConnectionManager connections,
        MessageDispatcher dispatcher,
        RoomService rooms,
        MatchmakingService matchmaking,
        StateService state,
        SessionTracker sessions,
        RealtimeActivity activity,
        RealtimeMetrics metrics,
        IIdGenerator ids,
        IClock clock,
        IOptions<RealtimeOptions> options,
        ILogger<RealtimeConnectionHandler> logger)
    {
        _connections = connections;
        _dispatcher = dispatcher;
        _rooms = rooms;
        _matchmaking = matchmaking;
        _state = state;
        _sessions = sessions;
        _activity = activity;
        _metrics = metrics;
        _ids = ids;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(
        WebSocket socket, Project project, PlayerIdentity identity, ConnectionMetadata metadata, CancellationToken ct)
    {
        var connectionId = _ids.NewId("conn");
        await using var connection = new RealtimeConnection(
            connectionId, project.Id, identity.PlayerId, socket, _clock.UtcNow);

        _connections.Add(connection);
        _sessions.Start(connection, _clock.UtcNow);
        _activity.Started(
            connectionId, project.Id, identity.PlayerId, metadata.IpAddress, metadata.SdkVersion, metadata.Engine);
        _metrics.ConnectionOpened();
        RealtimeLog.Connected(_logger, connectionId, project.Id);

        try
        {
            await SendWelcomeAsync(connection, identity, ct).ConfigureAwait(false);
            _activity.Authenticated(connectionId, identity.PlayerId);
            await ReceiveLoopAsync(connection, ct).ConfigureAwait(false);
        }
        finally
        {
            _connections.Remove(connectionId);
            _sessions.End(connectionId, _clock.UtcNow);
            _activity.Ended(connectionId, "connection closed");
            _metrics.ConnectionClosed();
            _matchmaking.HandleDisconnect(connectionId);
            _state.HandleDisconnect(connectionId);
            // Remove from any rooms and notify remaining members (best-effort during teardown).
            await _rooms.HandleDisconnectAsync(connection, CancellationToken.None).ConfigureAwait(false);
            RealtimeLog.Disconnected(_logger, connectionId, project.Id);
        }
    }

    private Task SendWelcomeAsync(RealtimeConnection connection, PlayerIdentity identity, CancellationToken ct)
    {
        var payload = new ConnectionWelcomePayload(
            connection.Id, identity.PlayerId, identity.Token, identity.TokenExpiresAtUtc, _clock.UtcNow);
        return RealtimeMessageSender.SendAsync(
            connection, RealtimeMessageTypes.ConnectionWelcome, requestId: null, payload, ct);
    }

    /// <summary>
    /// Reads whole messages and hands them to the dispatcher, enforcing a max message size
    /// and a per-connection rate limit. WebSocket-level ping/pong heartbeats are handled by
    /// the host (KeepAliveInterval).
    /// </summary>
    private async Task ReceiveLoopAsync(RealtimeConnection connection, CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var message = new MemoryStream();
        var maxBytes = _options.MaxMessageBytes;

        // Token bucket for rate limiting. This loop runs single-threaded per connection,
        // so the bucket needs no synchronization.
        var tokens = _options.MessageBurst;
        var lastRefill = _clock.UtcNow;
        var rateLimitLogged = false;

        while (connection.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            message.SetLength(0);
            var oversize = false;
            ValueWebSocketReceiveResult result;

            try
            {
                do
                {
                    result = await connection.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await connection
                            .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None)
                            .ConfigureAwait(false);
                        return;
                    }

                    // Stop buffering once oversize, but keep draining frames to stay in sync.
                    if (!oversize && message.Length + result.Count > maxBytes)
                    {
                        oversize = true;
                    }

                    if (!oversize)
                    {
                        message.Write(buffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                // Abrupt client disconnect — treated as a normal disconnect.
                break;
            }

            // Binary frames are not part of protocol v1 (JSON only); ignore them.
            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            if (oversize)
            {
                _metrics.ErrorOccurred();
                var tooLargeText = $"Message exceeds the {maxBytes}-byte limit.";
                _activity.Error(connection.Id, ProtocolErrorCodes.PayloadTooLarge, tooLargeText);
                RealtimeLog.Abuse(_logger, connection.Id, connection.ProjectId, "oversized message rejected");
                await SendErrorAsync(
                    connection, ProtocolErrorCodes.PayloadTooLarge, tooLargeText, ct).ConfigureAwait(false);
                continue;
            }

            // Refill then try to consume one token.
            var now = _clock.UtcNow;
            tokens = Math.Min(
                _options.MessageBurst,
                tokens + ((now - lastRefill).TotalSeconds * _options.MaxMessagesPerSecond));
            lastRefill = now;

            if (tokens < 1.0)
            {
                _metrics.ErrorOccurred();
                _activity.Error(connection.Id, ProtocolErrorCodes.RateLimited, "Too many messages; slow down.");
                if (!rateLimitLogged)
                {
                    RealtimeLog.Abuse(_logger, connection.Id, connection.ProjectId, "rate limited");
                    rateLimitLogged = true;
                }

                await SendErrorAsync(
                    connection, ProtocolErrorCodes.RateLimited,
                    "Too many messages; slow down.", ct).ConfigureAwait(false);
                continue;
            }

            tokens -= 1.0;

            _metrics.MessageReceived();
            var utf8 = new ReadOnlyMemory<byte>(message.GetBuffer(), 0, (int)message.Length);
            await _dispatcher.HandleMessageAsync(connection, utf8, ct).ConfigureAwait(false);
        }
    }

    private static Task SendErrorAsync(
        RealtimeConnection connection, string code, string message, CancellationToken ct)
        => RealtimeMessageSender.SendAsync(
            connection, RealtimeMessageTypes.Error, requestId: null, new RealtimeError(code, message), ct);
}
