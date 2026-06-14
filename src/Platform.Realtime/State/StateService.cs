using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Contracts.Realtime;
using Platform.Realtime.Connections;
using Platform.Realtime.Diagnostics;
using Platform.Realtime.Protocol;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.State;

/// <summary>
/// Orchestrates Simple State Sync v1 (Milestone 23): resolves the target room, enforces the
/// per-client update rate, applies ownership-checked, size-limited mutations to the room's
/// <see cref="RoomStateStore"/>, and broadcasts the result to members. Rejections are sent to
/// the caller as <c>state.update.rejected</c> and recorded for the dashboard debugger.
/// </summary>
internal sealed class StateService
{
    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyPatch =
        new Dictionary<string, JsonElement>();

    private sealed class RateBucket
    {
        public double Tokens { get; set; }
        public DateTimeOffset LastRefill { get; set; }
    }

    private readonly RoomManager _rooms;
    private readonly IClock _clock;
    private readonly RealtimeActivity _activity;
    private readonly StateOptions _options;
    private readonly ConcurrentDictionary<string, RateBucket> _buckets = new(StringComparer.Ordinal);

    public StateService(
        RoomManager rooms, IClock clock, RealtimeActivity activity, IOptions<StateOptions> options)
    {
        _rooms = rooms;
        _clock = clock;
        _activity = activity;
        _options = options.Value;
    }

    public async Task PatchRoomStateAsync(MessageContext context, StateRoomPatchRequest request)
    {
        var room = ResolveRoom(context, request.RoomId);
        if (room is null)
        {
            await RejectAsync(context, ProtocolErrorCodes.RoomNotFound, "You are not in that room.", null).ConfigureAwait(false);
            return;
        }

        if (!TryConsumeRate(context.Connection.Id))
        {
            await RejectAsync(context, ProtocolErrorCodes.RateLimited, "Too many state updates; slow down.", null).ConfigureAwait(false);
            return;
        }

        var playerId = context.Connection.PlayerId;
        var isHostOrOpen = room.HostPlayerId is null || string.Equals(room.HostPlayerId, playerId, StringComparison.Ordinal);
        if (!isHostOrOpen)
        {
            await RejectAsync(context, ProtocolErrorCodes.StateForbidden, "Only the room host can update room state.", null).ConfigureAwait(false);
            return;
        }

        var patch = request.Patch ?? EmptyPatch;
        var result = room.State.PatchRoom(patch, _options.MaxRoomStateBytes);
        if (!result.Ok)
        {
            await RejectForResultAsync(context, result.Result, null).ConfigureAwait(false);
            return;
        }

        var payload = new StateRoomChangedPayload(room.Id, patch, result.FullState);
        await BroadcastAsync(room, RealtimeMessageTypes.StateRoomChanged, payload, context.CancellationToken).ConfigureAwait(false);
    }

    public async Task SetEntityStateAsync(MessageContext context, StateEntitySetRequest request)
    {
        var room = await BeginEntityAsync(context, request.RoomId, request.EntityId);
        if (room is null)
        {
            return;
        }

        var playerId = context.Connection.PlayerId;
        var entityId = request.EntityId!;
        var state = request.State ?? EmptyPatch;
        var result = room.State.SetEntity(
            entityId, playerId, state, _options.MaxEntitiesPerRoom, _options.MaxEntityStateBytes, _clock.UtcNow);
        if (!result.Ok)
        {
            await RejectForResultAsync(context, result.Result, entityId).ConfigureAwait(false);
            return;
        }

        var payload = new StateEntityChangedPayload(room.Id, entityId, playerId, state, result.FullState);
        await BroadcastAsync(room, RealtimeMessageTypes.StateEntityChanged, payload, context.CancellationToken).ConfigureAwait(false);
    }

    public async Task PatchEntityStateAsync(MessageContext context, StateEntityPatchRequest request)
    {
        var room = await BeginEntityAsync(context, request.RoomId, request.EntityId);
        if (room is null)
        {
            return;
        }

        var playerId = context.Connection.PlayerId;
        var entityId = request.EntityId!;
        var patch = request.Patch ?? EmptyPatch;
        var result = room.State.PatchEntity(entityId, playerId, patch, _options.MaxEntityStateBytes, _clock.UtcNow);
        if (!result.Ok)
        {
            await RejectForResultAsync(context, result.Result, entityId).ConfigureAwait(false);
            return;
        }

        var payload = new StateEntityChangedPayload(room.Id, entityId, playerId, patch, result.FullState);
        await BroadcastAsync(room, RealtimeMessageTypes.StateEntityChanged, payload, context.CancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteEntityStateAsync(MessageContext context, StateEntityDeleteRequest request)
    {
        var room = await BeginEntityAsync(context, request.RoomId, request.EntityId);
        if (room is null)
        {
            return;
        }

        var entityId = request.EntityId!;
        var result = room.State.DeleteEntity(entityId, context.Connection.PlayerId);
        if (result != StateResult.Ok)
        {
            await RejectForResultAsync(context, result, entityId).ConfigureAwait(false);
            return;
        }

        var payload = new StateEntityDeletedPayload(room.Id, entityId);
        await BroadcastAsync(room, RealtimeMessageTypes.StateEntityDeleted, payload, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends the current room+entity state to a player who just joined (late-join snapshot).</summary>
    public static async Task SendSnapshotAsync(RealtimeConnection connection, Room room, CancellationToken ct)
    {
        var snapshot = room.State.Snapshot();
        if (snapshot.RoomState.Count == 0 && snapshot.Entities.Count == 0)
        {
            return; // nothing to sync yet
        }

        var entities = snapshot.Entities
            .Select(e => new EntitySnapshot(e.EntityId, e.OwnerId, e.State))
            .ToArray();
        var payload = new StateSnapshotPayload(room.Id, snapshot.RoomState, entities);
        await RealtimeMessageSender
            .SendAsync(connection, RealtimeMessageTypes.StateSnapshot, requestId: null, payload, ct)
            .ConfigureAwait(false);
    }

    public void HandleDisconnect(string connectionId) => _buckets.TryRemove(connectionId, out _);

    /// <summary>Shared entry for entity operations: resolves the room, rate-limits, validates the id.</summary>
    private async Task<Room?> BeginEntityAsync(MessageContext context, string? roomId, string? entityId)
    {
        var room = ResolveRoom(context, roomId);
        if (room is null)
        {
            await RejectAsync(context, ProtocolErrorCodes.RoomNotFound, "You are not in that room.", null).ConfigureAwait(false);
            return null;
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            await RejectAsync(context, ProtocolErrorCodes.InvalidPayload, "An 'entityId' is required.", null).ConfigureAwait(false);
            return null;
        }

        if (!TryConsumeRate(context.Connection.Id))
        {
            await RejectAsync(context, ProtocolErrorCodes.RateLimited, "Too many state updates; slow down.", entityId).ConfigureAwait(false);
            return null;
        }

        return room;
    }

    /// <summary>Finds the target room: the explicit id, or the connection's single room if omitted.</summary>
    private Room? ResolveRoom(MessageContext context, string? roomId)
    {
        var connection = context.Connection;
        if (!string.IsNullOrEmpty(roomId))
        {
            var room = _rooms.Find(roomId);
            return room is not null
                && string.Equals(room.ProjectId, connection.ProjectId, StringComparison.Ordinal)
                && room.ContainsMember(connection.Id)
                ? room
                : null;
        }

        var rooms = _rooms.RoomsForConnection(connection.Id);
        return rooms.Count == 1 ? rooms[0] : null;
    }

    private bool TryConsumeRate(string connectionId)
    {
        var now = _clock.UtcNow;
        var bucket = _buckets.GetOrAdd(connectionId, _ => new RateBucket { Tokens = _options.StateUpdateBurst, LastRefill = now });
        lock (bucket)
        {
            bucket.Tokens = Math.Min(
                _options.StateUpdateBurst,
                bucket.Tokens + ((now - bucket.LastRefill).TotalSeconds * _options.MaxStateUpdatesPerSecondPerClient));
            bucket.LastRefill = now;
            if (bucket.Tokens < 1.0)
            {
                return false;
            }

            bucket.Tokens -= 1.0;
            return true;
        }
    }

    private static Task BroadcastAsync(Room room, string type, object payload, CancellationToken ct)
        => RoomBroadcast.SendToMembersAsync(room.MembersSnapshot(), excludePlayerId: null, type, payload, ct);

    private Task RejectForResultAsync(MessageContext context, StateResult result, string? target)
    {
        var (code, message) = result switch
        {
            StateResult.Forbidden => (ProtocolErrorCodes.StateForbidden, "You don't own this entity."),
            StateResult.EntityNotFound => (ProtocolErrorCodes.EntityNotFound, "That entity does not exist."),
            StateResult.LimitExceeded => (ProtocolErrorCodes.StateLimitExceeded, "This room has reached its entity limit."),
            StateResult.TooLarge => (ProtocolErrorCodes.StateTooLarge, "The state is too large."),
            _ => (ProtocolErrorCodes.InvalidPayload, "The state update was rejected."),
        };
        return RejectAsync(context, code, message, target);
    }

    private Task RejectAsync(MessageContext context, string code, string message, string? target)
    {
        _activity.Error(context.Connection.Id, code, message);
        return context.RespondAsync(RealtimeMessageTypes.StateUpdateRejected, new StateRejectedPayload(code, message, target));
    }
}
