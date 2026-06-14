namespace Platform.Contracts.Realtime;

/// <summary>
/// Payload for <c>matchmaking.join</c>. Players are matched when enough are waiting with the
/// same project, <see cref="GameMode"/>, <see cref="Region"/>, and <see cref="MatchSize"/>.
/// <see cref="Region"/> defaults to <c>global</c>; <see cref="MatchSize"/> defaults to 2.
/// </summary>
public sealed record MatchmakingJoinRequest(string GameMode, string? Region, int? MatchSize, string? PlayerName);

/// <summary>Payload for <c>matchmaking.queued</c>.</summary>
public sealed record MatchmakingQueuedPayload(string GameMode, string Region, int MatchSize);

/// <summary>
/// Payload for <c>match.found</c>. The matched players are placed into a room; use
/// <see cref="RoomId"/> for game events.
/// </summary>
public sealed record MatchFoundPayload(
    string RoomId,
    string RoomCode,
    string GameMode,
    string Region,
    IReadOnlyList<RoomPlayer> Players);

/// <summary>Payload for <c>matchmaking.timeout</c>.</summary>
public sealed record MatchmakingTimeoutPayload(string GameMode, string Region);
