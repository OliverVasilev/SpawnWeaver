using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Realtime;
using Platform.Realtime.Connections;
using Platform.Realtime.Protocol;

namespace Platform.Realtime.Rooms;

/// <summary>
/// Orchestrates room operations on behalf of message handlers and the connection lifecycle:
/// mutates the <see cref="RoomManager"/> and sends the resulting messages to members.
/// </summary>
internal sealed class RoomService
{
    private readonly RoomManager _rooms;
    private readonly ILogger<RoomService> _logger;

    public RoomService(RoomManager rooms, ILogger<RoomService> logger)
    {
        _rooms = rooms;
        _logger = logger;
    }

    public async Task CreateRoomAsync(MessageContext context, string? playerName)
    {
        var connection = context.Connection;
        var room = _rooms.Create(connection.ProjectId, connection, playerName);
        RoomLog.Created(_logger, room.Id, room.Code, connection.ProjectId);

        var payload = new RoomCreatedPayload(room.Id, room.Code, connection.PlayerId, ToPlayers(room.MembersSnapshot()));
        await context.RespondAsync(RealtimeMessageTypes.RoomCreated, payload).ConfigureAwait(false);
    }

    public async Task JoinRoomAsync(MessageContext context, string roomCode, string? playerName)
    {
        var connection = context.Connection;
        var outcome = _rooms.Join(connection.ProjectId, roomCode, connection, playerName);

        if (outcome.Status == RoomJoinStatus.NotFound)
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.RoomNotFound, $"Room '{roomCode}' was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (outcome.Status == RoomJoinStatus.Full)
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.RoomFull, $"Room '{roomCode}' is full.")
                .ConfigureAwait(false);
            return;
        }

        var room = outcome.Room!;
        var roster = ToPlayers(outcome.Members);
        RoomLog.Joined(_logger, connection.PlayerId, room.Id, roster.Length);
        var payload = new RoomJoinedPayload(
            room.Id, room.Code, new RoomPlayer(connection.PlayerId, playerName), roster);

        // Response to the joiner (echoes requestId)...
        await context.RespondAsync(RealtimeMessageTypes.RoomJoined, payload).ConfigureAwait(false);

        // ...the late-join state snapshot (if the room has any state)...
        await State.StateService.SendSnapshotAsync(connection, room, context.CancellationToken).ConfigureAwait(false);

        // ...and notify everyone already in the room.
        await BroadcastAsync(
            outcome.Members, excludePlayerId: connection.PlayerId,
            RealtimeMessageTypes.RoomJoined, payload, context.CancellationToken).ConfigureAwait(false);
    }

    public async Task LeaveRoomAsync(MessageContext context, string roomId)
    {
        var connection = context.Connection;
        var outcome = _rooms.Leave(connection.Id, roomId);

        if (outcome is null)
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.RoomNotFound, $"You are not in room '{roomId}'.")
                .ConfigureAwait(false);
            return;
        }

        RoomLog.Left(_logger, connection.PlayerId, roomId, outcome.RemainingMembers.Count);
        var payload = new RoomLeftPayload(roomId, connection.PlayerId);

        // Acknowledge to the leaver, then tell the remaining members.
        await context.RespondAsync(RealtimeMessageTypes.RoomLeft, payload).ConfigureAwait(false);
        await BroadcastAsync(
            outcome.RemainingMembers, excludePlayerId: null,
            RealtimeMessageTypes.RoomLeft, payload, context.CancellationToken).ConfigureAwait(false);
    }

    public async Task ListPlayersAsync(MessageContext context, string roomId)
    {
        var connection = context.Connection;
        var room = _rooms.Find(roomId);

        if (room is null ||
            !string.Equals(room.ProjectId, connection.ProjectId, StringComparison.Ordinal) ||
            !room.ContainsMember(connection.Id))
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.RoomNotFound, $"Room '{roomId}' was not found.")
                .ConfigureAwait(false);
            return;
        }

        var payload = new RoomPlayersPayload(roomId, ToPlayers(room.MembersSnapshot()));
        await context.RespondAsync(RealtimeMessageTypes.RoomPlayers, payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Relays a game event to the other members of the sender's room. Validates that the
    /// sender is actually a member of the room (and that it belongs to their project).
    /// </summary>
    public async Task SendGameEventAsync(
        MessageContext context, string roomId, string eventName, JsonElement? data)
    {
        var connection = context.Connection;
        var room = _rooms.Find(roomId);

        if (room is null ||
            !string.Equals(room.ProjectId, connection.ProjectId, StringComparison.Ordinal) ||
            !room.ContainsMember(connection.Id))
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.RoomNotFound, $"You are not in room '{roomId}'.")
                .ConfigureAwait(false);
            return;
        }

        var payload = new GameEventPayload(roomId, eventName, data, connection.PlayerId);
        await BroadcastAsync(
            room.MembersSnapshot(), excludePlayerId: connection.PlayerId,
            RealtimeMessageTypes.GameEvent, payload, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a disconnected connection from all its rooms and notifies remaining members.</summary>
    public async Task HandleDisconnectAsync(RealtimeConnection connection, CancellationToken ct)
    {
        foreach (var outcome in _rooms.RemoveConnection(connection.Id))
        {
            RoomLog.Left(_logger, connection.PlayerId, outcome.Room.Id, outcome.RemainingMembers.Count);
            var payload = new RoomLeftPayload(outcome.Room.Id, connection.PlayerId);
            await BroadcastAsync(
                outcome.RemainingMembers, excludePlayerId: null,
                RealtimeMessageTypes.RoomLeft, payload, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Removes expired rooms and notifies any remaining members.</summary>
    public async Task SweepExpiredAsync(CancellationToken ct)
    {
        foreach (var room in _rooms.SweepExpired())
        {
            RoomLog.Expired(_logger, room.Id);
            var members = room.MembersSnapshot();
            if (members.Count == 0)
            {
                continue;
            }

            // Lobbies close; plain rooms expire.
            var (type, payload) = room.IsLobby
                ? (RealtimeMessageTypes.LobbyClosed, (object)new LobbyClosedPayload(room.Id))
                : (RealtimeMessageTypes.RoomExpired, new RoomExpiredPayload(room.Id));

            await BroadcastAsync(members, excludePlayerId: null, type, payload, ct).ConfigureAwait(false);
        }
    }

    private static Task BroadcastAsync(
        IReadOnlyList<RoomMember> members, string? excludePlayerId, string type, object payload, CancellationToken ct)
        => RoomBroadcast.SendToMembersAsync(members, excludePlayerId, type, payload, ct);

    private static RoomPlayer[] ToPlayers(IReadOnlyList<RoomMember> members)
        => RoomBroadcast.ToPlayers(members);
}
