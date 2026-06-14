using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Realtime.Connections;

namespace Platform.Realtime.Rooms;

/// <summary>
/// In-memory registry of all rooms (single-node MVP). Rooms are keyed by id and by code;
/// a reverse index maps a connection to the rooms it belongs to so disconnects are cheap.
/// </summary>
internal sealed class RoomManager
{
    private const int MaxCodeAttempts = 16;

    private readonly ConcurrentDictionary<string, Room> _byId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Room> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connectionRooms =
        new(StringComparer.Ordinal);

    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly IRoomCodeGenerator _codes;
    private readonly RealtimeOptions _options;

    public RoomManager(
        IIdGenerator ids,
        IClock clock,
        IRoomCodeGenerator codes,
        IOptions<RealtimeOptions> options)
    {
        _ids = ids;
        _clock = clock;
        _codes = codes;
        _options = options.Value;
    }

    public Room Create(string projectId, RealtimeConnection connection, string? playerName)
        => CreateCore(projectId, connection, playerName, lobby: null);

    /// <summary>Creates a lobby (a room with developer attributes) with the creator as first member.</summary>
    public Room CreateLobby(
        string projectId, RealtimeConnection connection, string? playerName, LobbyAttributes attributes)
        => CreateCore(projectId, connection, playerName, attributes);

    private Room CreateCore(
        string projectId, RealtimeConnection connection, string? playerName, LobbyAttributes? lobby)
    {
        var now = _clock.UtcNow;
        var prefix = lobby is null ? "room" : "lobby";
        var room = new Room(_ids.NewId(prefix), GenerateUniqueCode(), projectId, now, lobby);
        room.TryAddMember(connection, playerName, now);
        room.MarkHost(connection.PlayerId);

        _byId[room.Id] = room;
        _byCode[room.Code] = room;
        Track(connection.Id, room.Id);

        return room;
    }

    /// <summary>Joins a room/lobby by code (case-insensitive).</summary>
    public RoomJoinOutcome Join(string projectId, string code, RealtimeConnection connection, string? playerName)
    {
        if (!_byCode.TryGetValue(code, out var room) ||
            !string.Equals(room.ProjectId, projectId, StringComparison.Ordinal))
        {
            return RoomJoinOutcome.NotFound;
        }

        return TryJoin(room, connection, playerName);
    }

    /// <summary>Joins a room/lobby by id.</summary>
    public RoomJoinOutcome JoinById(string projectId, string roomId, RealtimeConnection connection, string? playerName)
    {
        if (!_byId.TryGetValue(roomId, out var room) ||
            !string.Equals(room.ProjectId, projectId, StringComparison.Ordinal))
        {
            return RoomJoinOutcome.NotFound;
        }

        return TryJoin(room, connection, playerName);
    }

    /// <summary>Creates an empty room to hold a matchmaking match; members are added next.</summary>
    public Room CreateMatchRoom(string projectId)
    {
        var now = _clock.UtcNow;
        var room = new Room(_ids.NewId("match"), GenerateUniqueCode(), projectId, now);
        _byId[room.Id] = room;
        _byCode[room.Code] = room;
        return room;
    }

    /// <summary>Adds a matched player to a match room (no capacity check).</summary>
    public void AddMatchedMember(Room room, RealtimeConnection connection, string? playerName)
    {
        room.TryAddMember(connection, playerName, _clock.UtcNow);
        Track(connection.Id, room.Id);
    }

    /// <summary>Public lobbies for a project, in creation order.</summary>
    public IReadOnlyList<Room> ListPublicLobbies(string projectId)
        => _byId.Values
            .Where(r => r.IsLobby
                && r.Visibility == LobbyVisibility.Public
                && string.Equals(r.ProjectId, projectId, StringComparison.Ordinal))
            .OrderBy(r => r.CreatedAtUtc)
            .ToArray();

    private RoomJoinOutcome TryJoin(Room room, RealtimeConnection connection, string? playerName)
    {
        if (!room.TryAddMember(connection, playerName, _clock.UtcNow))
        {
            return RoomJoinOutcome.Full(room);
        }

        Track(connection.Id, room.Id);
        return RoomJoinOutcome.Joined(room, room.MembersSnapshot());
    }

    /// <summary>Removes a connection from one room. Returns null if it was not a member.</summary>
    public RoomLeaveOutcome? Leave(string connectionId, string roomId)
    {
        if (!_byId.TryGetValue(roomId, out var room) || !room.RemoveMember(connectionId, _clock.UtcNow))
        {
            return null;
        }

        Untrack(connectionId, roomId);
        return new RoomLeaveOutcome(room, room.MembersSnapshot());
    }

    /// <summary>Removes a connection from every room it belonged to (used on disconnect).</summary>
    public IReadOnlyList<RoomLeaveOutcome> RemoveConnection(string connectionId)
    {
        if (!_connectionRooms.TryRemove(connectionId, out var roomIds))
        {
            return [];
        }

        var now = _clock.UtcNow;
        var outcomes = new List<RoomLeaveOutcome>();
        foreach (var roomId in roomIds.Keys)
        {
            if (_byId.TryGetValue(roomId, out var room) && room.RemoveMember(connectionId, now))
            {
                outcomes.Add(new RoomLeaveOutcome(room, room.MembersSnapshot()));
            }
        }

        return outcomes;
    }

    public Room? Find(string roomId) => _byId.GetValueOrDefault(roomId);

    public int RoomCount => _byId.Count;

    /// <summary>Snapshot of all live rooms (diagnostics).</summary>
    public IReadOnlyList<Room> AllRooms() => _byId.Values.ToArray();

    /// <summary>The rooms/lobbies a connection currently belongs to (diagnostics).</summary>
    public IReadOnlyList<Room> RoomsForConnection(string connectionId)
    {
        if (!_connectionRooms.TryGetValue(connectionId, out var roomIds))
        {
            return [];
        }

        var result = new List<Room>();
        foreach (var roomId in roomIds.Keys)
        {
            if (_byId.TryGetValue(roomId, out var room))
            {
                result.Add(room);
            }
        }

        return result;
    }

    /// <summary>Removes rooms that have been empty past the configured TTL. Returns the removed rooms.</summary>
    public IReadOnlyList<Room> SweepExpired()
    {
        var now = _clock.UtcNow;
        var expired = new List<Room>();

        foreach (var room in _byId.Values)
        {
            if (room.IsExpired(now, _options.EmptyRoomTtl) && _byId.TryRemove(room.Id, out _))
            {
                _byCode.TryRemove(room.Code, out _);
                expired.Add(room);
            }
        }

        return expired;
    }

    private string GenerateUniqueCode()
    {
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = _codes.Next();
            if (!_byCode.ContainsKey(code))
            {
                return code;
            }
        }

        // Astronomically unlikely; fall back to a guaranteed-unique id fragment.
        return _ids.NewId("R").Replace("R_", string.Empty, StringComparison.Ordinal)[..6].ToUpperInvariant();
    }

    private void Track(string connectionId, string roomId)
        => _connectionRooms.GetOrAdd(connectionId, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[roomId] = 1;

    private void Untrack(string connectionId, string roomId)
    {
        if (_connectionRooms.TryGetValue(connectionId, out var rooms))
        {
            rooms.TryRemove(roomId, out _);
            if (rooms.IsEmpty)
            {
                _connectionRooms.TryRemove(connectionId, out _);
            }
        }
    }
}
