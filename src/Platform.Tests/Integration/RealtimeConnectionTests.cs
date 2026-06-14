using System.Net.WebSockets;
using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeConnectionTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeConnectionTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Valid_key_connects_and_receives_welcome()
    {
        var publicKey = await _factory.CreateProjectKeyAsync();

        using var socket = await _factory.ConnectAsync(publicKey);

        Assert.Equal(WebSocketState.Open, socket.State);

        var welcome = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.ConnectionWelcome, welcome.Type);

        var payload = welcome.DeserializePayload<ConnectionWelcomePayload>();
        Assert.NotNull(payload);
        Assert.StartsWith("conn_", payload!.ConnectionId);

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_key_is_rejected()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = _factory.BuildConnectUri("pk_definitely_not_real");

        await Assert.ThrowsAnyAsync<Exception>(
            () => wsClient.ConnectAsync(uri, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_key_is_rejected()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = _factory.BuildConnectUri(publicKey: null);

        await Assert.ThrowsAnyAsync<Exception>(
            () => wsClient.ConnectAsync(uri, CancellationToken.None));
    }

    [Fact]
    public async Task Server_detects_disconnect()
    {
        var publicKey = await _factory.CreateProjectKeyAsync();

        using (var socket = await _factory.ConnectAsync(publicKey))
        {
            await socket.ReceiveEnvelopeAsync(); // welcome — ensures the connection is registered
            Assert.Equal(1, await _factory.PollConnectionCountAsync(expected: 1));

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }

        Assert.Equal(0, await _factory.PollConnectionCountAsync(expected: 0));
    }
}
