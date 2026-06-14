using Microsoft.Extensions.Logging;

namespace Platform.Realtime.Lobbies;

internal static partial class LobbyLog
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Information,
        Message = "Lobby created: {LobbyId} ({Visibility}) for project {ProjectId}")]
    public static partial void Created(ILogger logger, string lobbyId, string visibility, string projectId);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Information,
        Message = "Player {PlayerId} joined lobby {LobbyId} (now {MemberCount} players)")]
    public static partial void Joined(ILogger logger, string playerId, string lobbyId, int memberCount);
}
