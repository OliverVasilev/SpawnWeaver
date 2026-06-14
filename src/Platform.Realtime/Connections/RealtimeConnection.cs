using System.Net.WebSockets;

namespace Platform.Realtime.Connections;

/// <summary>
/// A single live client WebSocket connection. Wraps the socket and serializes
/// writes (the WebSocket API forbids concurrent sends).
/// </summary>
public sealed class RealtimeConnection : IAsyncDisposable
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public RealtimeConnection(
        string id, string projectId, string playerId, WebSocket socket, DateTimeOffset connectedAtUtc)
    {
        Id = id;
        ProjectId = projectId;
        PlayerId = playerId;
        _socket = socket;
        ConnectedAtUtc = connectedAtUtc;
    }

    public string Id { get; }

    public string ProjectId { get; }

    /// <summary>Stable player identity (survives reconnects); distinct from the connection id.</summary>
    public string PlayerId { get; }

    public DateTimeOffset ConnectedAtUtc { get; }

    public WebSocketState State => _socket.State;

    public async Task SendTextAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct)
        => _socket.ReceiveAsync(buffer, ct);

    public Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken ct)
        => _socket.CloseAsync(status, description, ct);

    public ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
