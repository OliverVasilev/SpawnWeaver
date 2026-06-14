namespace Platform.Contracts.Admin;

/// <summary>A live realtime connection (diagnostics).</summary>
public sealed record ConnectionSummary(
    string ConnectionId, string ProjectId, string PlayerId, DateTimeOffset ConnectedAtUtc);

/// <summary>A live room or lobby (diagnostics).</summary>
public sealed record RoomSummary(
    string RoomId, string Code, string ProjectId, bool IsLobby, int MemberCount, DateTimeOffset CreatedAtUtc);

/// <summary>A connection session (start, and end once disconnected).</summary>
public sealed record SessionSummary(
    string ConnectionId, string ProjectId, string PlayerId, DateTimeOffset StartedAtUtc, DateTimeOffset? EndedAtUtc);

/// <summary>A captured log record (diagnostics).</summary>
public sealed record LogRecord(DateTimeOffset TimestampUtc, string Level, string Category, string Message);

/// <summary>Admin summary of a project.</summary>
public sealed record AdminProjectSummary(string Id, string Name, bool IsActive, DateTimeOffset CreatedAtUtc);

/// <summary>Response for <c>GET /api/admin/projects</c>.</summary>
public sealed record AdminProjectsResponse(IReadOnlyList<AdminProjectSummary> Projects);

/// <summary>Response for <c>GET /api/admin/realtime</c>.</summary>
public sealed record AdminRealtimeResponse(
    int ActiveConnections,
    int ActiveRooms,
    IReadOnlyList<ConnectionSummary> Connections,
    IReadOnlyList<RoomSummary> Rooms);

/// <summary>Response for <c>GET /api/admin/sessions</c>.</summary>
public sealed record AdminSessionsResponse(IReadOnlyList<SessionSummary> Sessions);

/// <summary>Response for <c>GET /api/admin/logs</c>.</summary>
public sealed record AdminLogsResponse(IReadOnlyList<LogRecord> Logs);

/// <summary>Point-in-time realtime metrics (counters are totals since startup).</summary>
public sealed record MetricsSnapshot(
    int ActiveConnections,
    int ActiveRooms,
    long ConnectionsOpened,
    long ConnectionsClosed,
    long MessagesReceived,
    long Errors);

// --- Multiplayer debugger (Milestone 22) ---

/// <summary>One event on a session's chronological timeline.</summary>
public sealed record TimelineEntry(DateTimeOffset TimestampUtc, string Kind, string Detail);

/// <summary>Full diagnostic detail for one connection/session (the connection inspector).</summary>
public sealed record SessionDetail(
    string ConnectionId,
    string ProjectId,
    string PlayerId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? IpAddress,
    string? SdkVersion,
    string? Engine,
    string AuthStatus,
    string? DisconnectReason,
    string? CurrentRoomId,
    DateTimeOffset LastActivityAtUtc,
    IReadOnlyList<TimelineEntry> Timeline);

/// <summary>Response for <c>GET /api/admin/sessions/{connectionId}</c>.</summary>
public sealed record AdminSessionDetailResponse(SessionDetail? Session);

/// <summary>An aggregated error code with a count, recency, reach, and a suggested fix.</summary>
public sealed record ErrorBucket(
    string Code,
    long Count,
    DateTimeOffset LastOccurrenceUtc,
    string LastMessage,
    int AffectedSessions,
    string SuggestedFix);

/// <summary>Response for <c>GET /api/admin/errors</c> (the error explorer).</summary>
public sealed record AdminErrorsResponse(IReadOnlyList<ErrorBucket> Errors);

/// <summary>One active matchmaking bucket (game mode + region + size) and its waiting count.</summary>
public sealed record MatchmakingQueueInfo(string GameMode, string Region, int MatchSize, int Waiting);

/// <summary>Response for <c>GET /api/admin/matchmaking</c>.</summary>
public sealed record AdminMatchmakingResponse(int TotalWaiting, IReadOnlyList<MatchmakingQueueInfo> Queues);

/// <summary>A member of a room/lobby (room inspector).</summary>
public sealed record RoomMemberInfo(string PlayerId, string? PlayerName);

/// <summary>One live entity in a room (state inspector). <c>State</c> is raw JSON for display.</summary>
public sealed record RoomEntityInfo(
    string EntityId, string OwnerId, int KeyCount, string State, DateTimeOffset UpdatedAtUtc);

/// <summary>Full detail for one room or lobby (the room/lobby + state inspector).</summary>
public sealed record RoomDetail(
    string RoomId,
    string Code,
    string ProjectId,
    bool IsLobby,
    string? Name,
    string? Visibility,
    int? MaxPlayers,
    int MemberCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    string? HostPlayerId,
    IReadOnlyList<RoomMemberInfo> Members,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyDictionary<string, string> RoomState,
    IReadOnlyList<RoomEntityInfo> Entities);

/// <summary>Response for <c>GET /api/admin/rooms/{roomId}</c>.</summary>
public sealed record AdminRoomDetailResponse(RoomDetail? Room);
