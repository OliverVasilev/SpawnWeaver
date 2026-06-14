using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeAuthTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeAuthTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Welcome_issues_a_player_id_and_token()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var socket = await _factory.ConnectAsync(key);
        var welcome = (await socket.ReceiveEnvelopeAsync()).DeserializePayload<ConnectionWelcomePayload>()!;

        Assert.StartsWith("player_", welcome.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(welcome.PlayerToken));
        Assert.True(welcome.TokenExpiresAtUtc > welcome.ServerTimeUtc);
    }

    [Fact]
    public async Task Reconnecting_with_token_resumes_the_same_player()
    {
        var key = await _factory.CreateProjectKeyAsync();

        string playerId;
        string token;
        using (var first = await _factory.ConnectAsync(key))
        {
            var welcome = (await first.ReceiveEnvelopeAsync()).DeserializePayload<ConnectionWelcomePayload>()!;
            playerId = welcome.PlayerId;
            token = welcome.PlayerToken;
            await first.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }

        using var second = await _factory.ConnectWithTokenAsync(key, token);
        var resumed = (await second.ReceiveEnvelopeAsync()).DeserializePayload<ConnectionWelcomePayload>()!;

        Assert.Equal(playerId, resumed.PlayerId);
    }

    [Fact]
    public async Task Invalid_player_token_is_rejected()
    {
        var key = await _factory.CreateProjectKeyAsync();

        await Assert.ThrowsAnyAsync<Exception>(
            () => _factory.ConnectWithTokenAsync(key, "totally.invalid.player.token"));
    }

    [Fact]
    public async Task A_fresh_connection_gets_a_distinct_player_id()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var a = await _factory.ConnectAsync(key);
        using var b = await _factory.ConnectAsync(key);
        var idA = (await a.ReceiveEnvelopeAsync()).DeserializePayload<ConnectionWelcomePayload>()!.PlayerId;
        var idB = (await b.ReceiveEnvelopeAsync()).DeserializePayload<ConnectionWelcomePayload>()!.PlayerId;

        Assert.NotEqual(idA, idB);
    }
}
