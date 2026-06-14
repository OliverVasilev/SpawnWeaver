namespace Platform.Contracts.Realtime;

// Visibility values used on the wire.
public static class LobbyVisibilities
{
    public const string Public = "public";
    public const string Private = "private";
}

// --- Requests (client → server) ---

/// <summary>
/// Payload for <c>lobby.create</c>. <see cref="Visibility"/> is <c>public</c> (listed) or
/// <c>private</c> (join by code only); defaults to public. <see cref="MaxPlayers"/> null = unlimited.
/// </summary>
public sealed record LobbyCreateRequest(
    string? Name,
    string? Visibility,
    int? MaxPlayers,
    IReadOnlyDictionary<string, string>? Metadata,
    string? PlayerName);

/// <summary>Payload for <c>lobby.list</c> (no fields).</summary>
public sealed record LobbyListRequest;

/// <summary>Payload for <c>lobby.join</c>. Provide <see cref="LobbyId"/> (public lobbies) or <see cref="Code"/>.</summary>
public sealed record LobbyJoinRequest(string? LobbyId, string? Code, string? PlayerName);

// --- Responses / events (server → client) ---

/// <summary>A public lobby as it appears in the list.</summary>
public sealed record LobbySummary(
    string LobbyId,
    string? Name,
    string Visibility,
    int PlayerCount,
    int? MaxPlayers,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Payload for <c>lobby.created</c> (sent to the creator).</summary>
public sealed record LobbyCreatedPayload(
    string LobbyId,
    string Code,
    string? Name,
    string Visibility,
    int? MaxPlayers,
    IReadOnlyDictionary<string, string> Metadata,
    string PlayerId,
    IReadOnlyList<RoomPlayer> Players);

/// <summary>Payload for the <c>lobby.list</c> response (public lobbies for the project).</summary>
public sealed record LobbyListPayload(IReadOnlyList<LobbySummary> Lobbies);

/// <summary>
/// Payload for <c>lobby.joined</c>. Sent to the joiner (response) and broadcast to existing
/// members. <see cref="Player"/> is who joined; <see cref="Players"/> is the full roster.
/// </summary>
public sealed record LobbyJoinedPayload(
    string LobbyId,
    string Code,
    string? Name,
    string Visibility,
    int? MaxPlayers,
    IReadOnlyDictionary<string, string> Metadata,
    RoomPlayer Player,
    IReadOnlyList<RoomPlayer> Players);

/// <summary>Payload for <c>lobby.closed</c>.</summary>
public sealed record LobbyClosedPayload(string LobbyId);
