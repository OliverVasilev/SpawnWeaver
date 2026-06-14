using System.Net.WebSockets;
using Platform.Application.Abstractions;
using Platform.Realtime.Connections;

namespace Platform.Tests.TestDoubles;

/// <summary>An <see cref="IClock"/> whose time can be set/advanced for deterministic tests.</summary>
internal sealed class MutableClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>A no-op WebSocket; room-manager tests store connections but never do I/O on them.</summary>
internal sealed class FakeWebSocket : WebSocket
{
    public override WebSocketCloseStatus? CloseStatus => null;

    public override string? CloseStatusDescription => null;

    public override WebSocketState State => WebSocketState.Open;

    public override string? SubProtocol => null;

    public override void Abort()
    {
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override void Dispose()
    {
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal static class TestConnections
{
    public static RealtimeConnection Create(string projectId, string? id = null, string? playerId = null)
    {
        var connectionId = id ?? $"conn_{Guid.NewGuid():N}";
        // Player id defaults to the connection id so room-manager tests can compare against it.
        return new RealtimeConnection(connectionId, projectId, playerId ?? connectionId, new FakeWebSocket(), default);
    }
}
