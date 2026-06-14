using Microsoft.Extensions.Logging;

namespace Platform.Realtime.Transport;

/// <summary>Source-generated, low-allocation log messages for connection lifecycle.</summary>
internal static partial class RealtimeLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Realtime connected: connection {ConnectionId} for project {ProjectId}")]
    public static partial void Connected(ILogger logger, string connectionId, string projectId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Realtime disconnected: connection {ConnectionId} for project {ProjectId}")]
    public static partial void Disconnected(ILogger logger, string connectionId, string projectId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Realtime connection rejected: {Reason}")]
    public static partial void Rejected(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Realtime abuse: connection {ConnectionId} (project {ProjectId}) - {Reason}")]
    public static partial void Abuse(ILogger logger, string connectionId, string projectId, string reason);
}
