using Platform.Realtime.Connections;

namespace Platform.Realtime.Rooms;

/// <summary>A player inside a room, identified by the connection's stable player id.</summary>
internal sealed class RoomMember
{
    public RoomMember(RealtimeConnection connection, string? playerName)
    {
        Connection = connection;
        PlayerName = playerName;
    }

    public RealtimeConnection Connection { get; }

    public string PlayerId => Connection.PlayerId;

    public string? PlayerName { get; }
}
