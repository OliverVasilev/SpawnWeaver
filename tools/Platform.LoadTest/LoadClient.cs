using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Platform.LoadTest;

/// <summary>A single load-test WebSocket client: connects, joins a room, sends/receives events.</summary>
internal sealed class LoadClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ClientWebSocket _socket = new();
    private readonly TaskCompletionSource<string> _roomReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LoadClient(int index) => Index = index;

    public int Index { get; }
    public string? RoomId { get; private set; }
    public long EventsReceived { get; private set; }
    public bool Failed { get; private set; }

    /// <summary>Completes with the room code once this client has created or joined a room.</summary>
    public Task<string> RoomReady => _roomReady.Task;

    public async Task ConnectAsync(string baseUrl, string projectKey, CancellationToken ct)
    {
        var uri = new Uri($"{baseUrl}?projectKey={Uri.EscapeDataString(projectKey)}");
        await _socket.ConnectAsync(uri, ct);

        // The receive loop lives for the whole connection (until CloseAsync). It must NOT
        // use the connect-timeout token, since cancelling a WebSocket receive aborts it.
        _ = Task.Run(() => ReceiveLoopAsync(CancellationToken.None), CancellationToken.None);
    }

    public Task CreateRoomAsync(CancellationToken ct) => SendAsync(new { type = "room.create" }, ct);

    public Task JoinRoomAsync(string roomCode, CancellationToken ct)
        => SendAsync(new { type = "room.join", payload = new { roomCode } }, ct);

    public Task SendEventAsync(CancellationToken ct)
        => SendAsync(
            new { type = "game.event", payload = new { roomId = RoomId, @event = "tick", data = new { t = Index } } },
            ct);

    public async Task CloseAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            // Ignore close races.
        }
    }

    public void Dispose() => _socket.Dispose();

    private async Task SendAsync(object envelope, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (_socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                HandleMessage(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (WebSocketException)
        {
            Failed = true;
        }
    }

    private void HandleMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type is null || !root.TryGetProperty("payload", out var payload))
        {
            return;
        }

        switch (type)
        {
            case "room.created":
            case "room.joined":
                RoomId = payload.GetProperty("roomId").GetString();
                if (payload.TryGetProperty("roomCode", out var code) && !_roomReady.Task.IsCompleted)
                {
                    _roomReady.TrySetResult(code.GetString() ?? string.Empty);
                }

                break;
            case "game.event":
                EventsReceived++;
                break;
            case "error":
                Failed = true;
                break;
        }
    }
}
