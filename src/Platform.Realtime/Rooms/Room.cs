using Platform.Realtime.Connections;
using Platform.Realtime.State;

namespace Platform.Realtime.Rooms;

/// <summary>
/// An in-memory room that owns its connected players (§4.1 "small actor"). A room may also
/// be a <em>lobby</em> — carrying a name, visibility, max-player cap, and metadata. State
/// mutations are serialized by a per-room lock; callers broadcast outside the lock using the
/// returned member snapshots.
/// </summary>
internal sealed class Room
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>();

    private readonly object _gate = new();
    private readonly Dictionary<string, RoomMember> _members = new(StringComparer.Ordinal);

    public Room(string id, string code, string projectId, DateTimeOffset createdAtUtc, LobbyAttributes? lobby = null)
    {
        Id = id;
        Code = code;
        ProjectId = projectId;
        CreatedAtUtc = createdAtUtc;
        LastActivityAtUtc = createdAtUtc;

        if (lobby is not null)
        {
            IsLobby = true;
            Name = lobby.Name;
            Visibility = lobby.Visibility;
            MaxPlayers = lobby.MaxPlayers;
            Metadata = lobby.Metadata;
        }
        else
        {
            Metadata = EmptyMetadata;
        }
    }

    public string Id { get; }

    public string Code { get; }

    public string ProjectId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset LastActivityAtUtc { get; private set; }

    // Lobby attributes (defaults make a plain room).
    public bool IsLobby { get; }

    public string? Name { get; }

    public LobbyVisibility Visibility { get; }

    public int? MaxPlayers { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>The player who created the room/lobby (the host). Null for match rooms.</summary>
    public string? HostPlayerId { get; private set; }

    /// <summary>Live room + entity state (Milestone 23 state sync).</summary>
    public RoomStateStore State { get; } = new();

    /// <summary>Records the creator as the host (diagnostics / lobby semantics).</summary>
    public void MarkHost(string playerId) => HostPlayerId = playerId;

    public int MemberCount
    {
        get
        {
            lock (_gate)
            {
                return _members.Count;
            }
        }
    }

    /// <summary>
    /// Adds a member, enforcing <see cref="MaxPlayers"/>. Returns false (without changing
    /// state) if the room is full. Re-adding an existing member always succeeds.
    /// </summary>
    public bool TryAddMember(RealtimeConnection connection, string? playerName, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_members.ContainsKey(connection.Id) && MaxPlayers is int max && _members.Count >= max)
            {
                return false;
            }

            _members[connection.Id] = new RoomMember(connection, playerName);
            LastActivityAtUtc = now;
            return true;
        }
    }

    public bool RemoveMember(string connectionId, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_members.Remove(connectionId))
            {
                return false;
            }

            LastActivityAtUtc = now;
            return true;
        }
    }

    public bool ContainsMember(string connectionId)
    {
        lock (_gate)
        {
            return _members.ContainsKey(connectionId);
        }
    }

    public IReadOnlyList<RoomMember> MembersSnapshot()
    {
        lock (_gate)
        {
            return _members.Values.ToArray();
        }
    }

    /// <summary>True if the room is empty and has been idle for at least <paramref name="ttl"/>.</summary>
    public bool IsExpired(DateTimeOffset now, TimeSpan ttl)
    {
        lock (_gate)
        {
            return _members.Count == 0 && (now - LastActivityAtUtc) >= ttl;
        }
    }
}
