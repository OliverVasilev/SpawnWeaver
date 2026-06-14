namespace Platform.Contracts.Realtime;

/// <summary>A player in a room.</summary>
public sealed record RoomPlayer(string PlayerId, string? PlayerName);

// --- Requests (client → server) ---

/// <summary>Payload for <c>room.create</c>. All fields optional.</summary>
public sealed record RoomCreateRequest(string? PlayerName);

/// <summary>Payload for <c>room.join</c>.</summary>
public sealed record RoomJoinRequest(string RoomCode, string? PlayerName);

/// <summary>Payload for <c>room.leave</c>.</summary>
public sealed record RoomLeaveRequest(string RoomId);

/// <summary>Payload for the <c>room.players</c> request.</summary>
public sealed record RoomPlayersRequest(string RoomId);

// --- Responses / events (server → client) ---

/// <summary>Payload for <c>room.created</c> (sent to the creator).</summary>
public sealed record RoomCreatedPayload(
    string RoomId,
    string RoomCode,
    string PlayerId,
    IReadOnlyList<RoomPlayer> Players);

/// <summary>
/// Payload for <c>room.joined</c>. Sent to the joiner (as the response to their
/// <c>room.join</c>) and broadcast to existing members. <see cref="Player"/> is the
/// player who just joined; <see cref="Players"/> is the full roster.
/// </summary>
public sealed record RoomJoinedPayload(
    string RoomId,
    string RoomCode,
    RoomPlayer Player,
    IReadOnlyList<RoomPlayer> Players);

/// <summary>Payload for <c>room.left</c>. <see cref="PlayerId"/> is the player who left.</summary>
public sealed record RoomLeftPayload(string RoomId, string PlayerId);

/// <summary>Payload for the <c>room.players</c> response.</summary>
public sealed record RoomPlayersPayload(string RoomId, IReadOnlyList<RoomPlayer> Players);

/// <summary>Payload for <c>room.expired</c>.</summary>
public sealed record RoomExpiredPayload(string RoomId);
