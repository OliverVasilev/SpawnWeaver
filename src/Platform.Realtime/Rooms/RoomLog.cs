using Microsoft.Extensions.Logging;

namespace Platform.Realtime.Rooms;

/// <summary>Source-generated log messages for room lifecycle (playtest observability).</summary>
internal static partial class RoomLog
{
    [LoggerMessage(EventId = 1200, Level = LogLevel.Information,
        Message = "Room created: {RoomId} ({RoomCode}) for project {ProjectId}")]
    public static partial void Created(ILogger logger, string roomId, string roomCode, string projectId);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information,
        Message = "Player {PlayerId} joined room {RoomId} (now {MemberCount} members)")]
    public static partial void Joined(ILogger logger, string playerId, string roomId, int memberCount);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information,
        Message = "Player {PlayerId} left room {RoomId} (now {MemberCount} members)")]
    public static partial void Left(ILogger logger, string playerId, string roomId, int memberCount);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information,
        Message = "Room expired: {RoomId}")]
    public static partial void Expired(ILogger logger, string roomId);
}
