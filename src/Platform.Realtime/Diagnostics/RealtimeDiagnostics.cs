using Platform.Contracts.Admin;
using Platform.Realtime.Connections;
using Platform.Realtime.Matchmaking;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Diagnostics;

/// <summary>
/// Public read-only view of in-memory realtime state for the dashboard and admin API,
/// including the Milestone 22 debugger surfaces (session inspector, error explorer,
/// room/lobby inspector, matchmaking inspector).
/// </summary>
public sealed class RealtimeDiagnostics
{
    private readonly ConnectionManager _connections;
    private readonly RoomManager _rooms;
    private readonly SessionTracker _sessions;
    private readonly RealtimeActivity _activity;
    private readonly MatchQueue _matchQueue;

    internal RealtimeDiagnostics(
        ConnectionManager connections,
        RoomManager rooms,
        SessionTracker sessions,
        RealtimeActivity activity,
        MatchQueue matchQueue)
    {
        _connections = connections;
        _rooms = rooms;
        _sessions = sessions;
        _activity = activity;
        _matchQueue = matchQueue;
    }

    public int ActiveConnections => _connections.Count;

    public int ActiveRooms => _rooms.RoomCount;

    /// <summary>Active lobbies (a subset of rooms).</summary>
    public int ActiveLobbies => _rooms.AllRooms().Count(r => r.IsLobby);

    /// <summary>Players currently waiting in the matchmaking queue.</summary>
    public int MatchmakingWaiting => _matchQueue.Count;

    public IReadOnlyList<ConnectionSummary> GetConnections()
        => _connections.Snapshot()
            .Select(c => new ConnectionSummary(c.Id, c.ProjectId, c.PlayerId, c.ConnectedAtUtc))
            .ToArray();

    public IReadOnlyList<RoomSummary> GetRooms()
        => _rooms.AllRooms()
            .Select(r => new RoomSummary(r.Id, r.Code, r.ProjectId, r.IsLobby, r.MemberCount, r.CreatedAtUtc))
            .ToArray();

    public IReadOnlyList<SessionSummary> GetRecentSessions() => _sessions.Recent();

    /// <summary>The session/connection inspector: timeline + metadata for one connection.</summary>
    public SessionDetail? GetSessionDetail(string connectionId)
    {
        var detail = _activity.GetSession(connectionId);
        if (detail is null)
        {
            return null;
        }

        // The current room is derived from live membership at query time.
        var rooms = _rooms.RoomsForConnection(connectionId);
        return detail with { CurrentRoomId = rooms.Count > 0 ? rooms[0].Id : null };
    }

    /// <summary>The error explorer: aggregated error codes with counts and suggested fixes.</summary>
    public IReadOnlyList<ErrorBucket> GetErrors() => _activity.GetErrors();

    /// <summary>The room/lobby inspector: members, metadata, host, and lobby attributes.</summary>
    public RoomDetail? GetRoomDetail(string roomId)
    {
        var room = _rooms.Find(roomId);
        if (room is null)
        {
            return null;
        }

        var members = room.MembersSnapshot()
            .Select(m => new RoomMemberInfo(m.PlayerId, m.PlayerName))
            .ToArray();

        var snapshot = room.State.Snapshot();
        var roomState = snapshot.RoomState.ToDictionary(kv => kv.Key, kv => kv.Value.GetRawText(), StringComparer.Ordinal);
        var entities = snapshot.Entities
            .Select(e => new RoomEntityInfo(
                e.EntityId,
                e.OwnerId,
                e.State.Count,
                System.Text.Json.JsonSerializer.Serialize(e.State),
                e.UpdatedAtUtc))
            .ToArray();

        return new RoomDetail(
            room.Id,
            room.Code,
            room.ProjectId,
            room.IsLobby,
            room.Name,
            room.IsLobby ? room.Visibility.ToString().ToLowerInvariant() : null,
            room.MaxPlayers,
            room.MemberCount,
            room.CreatedAtUtc,
            room.LastActivityAtUtc,
            room.HostPlayerId,
            members,
            room.Metadata,
            roomState,
            entities);
    }

    /// <summary>The matchmaking inspector: active queues (buckets) and their waiting counts.</summary>
    public AdminMatchmakingResponse GetMatchmaking()
    {
        var queues = new List<MatchmakingQueueInfo>();
        foreach (var (bucketKey, waiting) in _matchQueue.Snapshot())
        {
            // Bucket key is project|gameMode|region|matchSize.
            var parts = bucketKey.Split('|');
            var gameMode = parts.Length > 1 ? parts[1] : "?";
            var region = parts.Length > 2 ? parts[2] : "?";
            var matchSize = parts.Length > 3 && int.TryParse(parts[3], out var size) ? size : 0;
            queues.Add(new MatchmakingQueueInfo(gameMode, region, matchSize, waiting));
        }

        return new AdminMatchmakingResponse(_matchQueue.Count, queues);
    }
}
