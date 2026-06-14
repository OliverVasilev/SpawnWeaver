using Microsoft.Extensions.Logging;

namespace Platform.Realtime.Matchmaking;

internal static partial class MatchmakingLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Information,
        Message = "Match found: room {RoomId} for {GameMode}/{Region} with {PlayerCount} players")]
    public static partial void MatchFound(ILogger logger, string roomId, string gameMode, string region, int playerCount);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information,
        Message = "Matchmaking timeout for connection {ConnectionId} ({GameMode}/{Region})")]
    public static partial void TimedOut(ILogger logger, string connectionId, string gameMode, string region);
}
