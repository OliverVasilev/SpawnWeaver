using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Contracts.Realtime;
using Platform.Realtime.Protocol;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Matchmaking;

/// <summary>
/// Queue-based matchmaking: enqueues players, forms a match (a room) when a bucket fills,
/// notifies matched players, and times out players who wait too long.
/// </summary>
internal sealed class MatchmakingService
{
    public const int DefaultMatchSize = 2;
    public const string DefaultRegion = "global";

    private readonly MatchQueue _queue;
    private readonly RoomManager _rooms;
    private readonly IClock _clock;
    private readonly RealtimeOptions _options;
    private readonly ILogger<MatchmakingService> _logger;

    public MatchmakingService(
        MatchQueue queue,
        RoomManager rooms,
        IClock clock,
        IOptions<RealtimeOptions> options,
        ILogger<MatchmakingService> logger)
    {
        _queue = queue;
        _rooms = rooms;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task JoinAsync(MessageContext context, string gameMode, string region, int matchSize, string? playerName)
    {
        var connection = context.Connection;
        var bucketKey = $"{connection.ProjectId}|{gameMode}|{region}|{matchSize}";
        var ticket = new MatchTicket(connection, playerName, gameMode, region, matchSize, bucketKey, _clock.UtcNow);

        var group = _queue.Enqueue(ticket);
        if (group is null)
        {
            await context.RespondAsync(
                RealtimeMessageTypes.MatchmakingQueued,
                new MatchmakingQueuedPayload(gameMode, region, matchSize)).ConfigureAwait(false);
            return;
        }

        await CreateMatchAsync(context, gameMode, region, group).ConfigureAwait(false);
    }

    public async Task LeaveAsync(MessageContext context)
    {
        _queue.Remove(context.Connection.Id);
        await context.RespondAsync(RealtimeMessageTypes.MatchmakingLeft).ConfigureAwait(false);
    }

    public void HandleDisconnect(string connectionId) => _queue.Remove(connectionId);

    public async Task SweepTimeoutsAsync(CancellationToken ct)
    {
        foreach (var ticket in _queue.SweepTimeouts(_clock.UtcNow, _options.MatchmakingTimeout))
        {
            MatchmakingLog.TimedOut(_logger, ticket.Connection.Id, ticket.GameMode, ticket.Region);
            try
            {
                await RealtimeMessageSender.SendAsync(
                    ticket.Connection, RealtimeMessageTypes.MatchmakingTimeout, requestId: null,
                    new MatchmakingTimeoutPayload(ticket.GameMode, ticket.Region), ct).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Connection already gone.
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CreateMatchAsync(
        MessageContext context, string gameMode, string region, IReadOnlyList<MatchTicket> group)
    {
        var joiner = context.Connection;
        var room = _rooms.CreateMatchRoom(joiner.ProjectId);
        foreach (var ticket in group)
        {
            _rooms.AddMatchedMember(room, ticket.Connection, ticket.PlayerName);
        }

        var players = RoomBroadcast.ToPlayers(room.MembersSnapshot());
        var payload = new MatchFoundPayload(room.Id, room.Code, gameMode, region, players);
        MatchmakingLog.MatchFound(_logger, room.Id, gameMode, region, players.Length);

        foreach (var ticket in group)
        {
            if (string.Equals(ticket.Connection.Id, joiner.Id, StringComparison.Ordinal))
            {
                await context.RespondAsync(RealtimeMessageTypes.MatchFound, payload).ConfigureAwait(false);
            }
            else
            {
                await RealtimeMessageSender.SendAsync(
                    ticket.Connection, RealtimeMessageTypes.MatchFound, requestId: null, payload,
                    context.CancellationToken).ConfigureAwait(false);
            }
        }
    }
}
