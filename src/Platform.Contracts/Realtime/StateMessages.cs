using System.Text.Json;

namespace Platform.Contracts.Realtime;

// --- Inbound (client → server) ---

/// <summary>Request to patch shared room state. <c>RoomId</c> defaults to the caller's current room.</summary>
public sealed record StateRoomPatchRequest(string? RoomId, IReadOnlyDictionary<string, JsonElement>? Patch);

/// <summary>Request to set (create or replace) an entity's full state. The caller becomes the owner.</summary>
public sealed record StateEntitySetRequest(
    string? RoomId, string? EntityId, IReadOnlyDictionary<string, JsonElement>? State);

/// <summary>Request to merge a partial patch into an entity the caller owns.</summary>
public sealed record StateEntityPatchRequest(
    string? RoomId, string? EntityId, IReadOnlyDictionary<string, JsonElement>? Patch);

/// <summary>Request to delete an entity the caller owns.</summary>
public sealed record StateEntityDeleteRequest(string? RoomId, string? EntityId);

// --- Outbound (server → client) ---

/// <summary>Broadcast when room state changes: the applied <c>Patch</c> and the resulting full <c>State</c>.</summary>
public sealed record StateRoomChangedPayload(
    string RoomId,
    IReadOnlyDictionary<string, JsonElement> Patch,
    IReadOnlyDictionary<string, JsonElement> State);

/// <summary>Broadcast when an entity changes: the applied <c>Patch</c> and the resulting full <c>State</c>.</summary>
public sealed record StateEntityChangedPayload(
    string RoomId,
    string EntityId,
    string OwnerId,
    IReadOnlyDictionary<string, JsonElement> Patch,
    IReadOnlyDictionary<string, JsonElement> State);

/// <summary>Broadcast when an entity is deleted.</summary>
public sealed record StateEntityDeletedPayload(string RoomId, string EntityId);

/// <summary>One entity in a snapshot.</summary>
public sealed record EntitySnapshot(string EntityId, string OwnerId, IReadOnlyDictionary<string, JsonElement> State);

/// <summary>Full current state sent to a player on joining a room (late-join snapshot).</summary>
public sealed record StateSnapshotPayload(
    string RoomId,
    IReadOnlyDictionary<string, JsonElement> RoomState,
    IReadOnlyList<EntitySnapshot> Entities);

/// <summary>Sent to the caller when a state update is rejected. <c>Target</c> is the entity id, if any.</summary>
public sealed record StateRejectedPayload(string Code, string Message, string? Target);
