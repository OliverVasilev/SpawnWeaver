using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeLobbiesTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeLobbiesTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_public_lobby_then_list_shows_it()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var host = await _factory.ConnectReadyAsync(key);

        await host.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new
        {
            name = "Arena",
            visibility = "public",
            maxPlayers = 4,
            metadata = new { mode = "ffa" },
        });
        var created = (await host.ReceiveEnvelopeAsync()).DeserializePayload<LobbyCreatedPayload>()!;
        Assert.Equal("Arena", created.Name);
        Assert.Equal("public", created.Visibility);
        Assert.Equal(4, created.MaxPlayers);
        Assert.Equal("ffa", created.Metadata["mode"]);

        // A second player lists public lobbies and sees it.
        using var browser = await _factory.ConnectReadyAsync(key);
        await browser.SendMessageAsync(RealtimeMessageTypes.LobbyList, "l1", null);
        var list = (await browser.ReceiveEnvelopeAsync()).DeserializePayload<LobbyListPayload>()!;

        Assert.Contains(list.Lobbies, l => l.LobbyId == created.LobbyId && l.PlayerCount == 1 && l.MaxPlayers == 4);
    }

    [Fact]
    public async Task Private_lobby_is_not_listed_but_joinable_by_code()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var host = await _factory.ConnectReadyAsync(key);

        await host.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new { name = "Secret", visibility = "private" });
        var created = (await host.ReceiveEnvelopeAsync()).DeserializePayload<LobbyCreatedPayload>()!;

        using var other = await _factory.ConnectReadyAsync(key);

        // Not in the public list.
        await other.SendMessageAsync(RealtimeMessageTypes.LobbyList, "l1", null);
        var list = (await other.ReceiveEnvelopeAsync()).DeserializePayload<LobbyListPayload>()!;
        Assert.DoesNotContain(list.Lobbies, l => l.LobbyId == created.LobbyId);

        // Joining a private lobby by id is rejected...
        await other.SendMessageAsync(RealtimeMessageTypes.LobbyJoin, "j1", new { lobbyId = created.LobbyId });
        var byId = await other.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, byId.Type);
        Assert.Equal(ProtocolErrorCodes.RoomNotFound, byId.DeserializePayload<RealtimeError>()!.Code);

        // ...but joining by code works.
        await other.SendMessageAsync(RealtimeMessageTypes.LobbyJoin, "j2", new { code = created.Code });
        var joined = await other.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.LobbyJoined, joined.Type);
        Assert.Equal(2, joined.DeserializePayload<LobbyJoinedPayload>()!.Players.Count);
    }

    [Fact]
    public async Task Public_lobby_joinable_by_id_and_notifies_members()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var host = await _factory.ConnectReadyAsync(key);

        await host.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new { name = "Open", visibility = "public" });
        var created = (await host.ReceiveEnvelopeAsync()).DeserializePayload<LobbyCreatedPayload>()!;

        using var joiner = await _factory.ConnectReadyAsync(key);
        await joiner.SendMessageAsync(RealtimeMessageTypes.LobbyJoin, "j1", new { lobbyId = created.LobbyId });

        var joinedResponse = await joiner.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.LobbyJoined, joinedResponse.Type);
        Assert.Equal("j1", joinedResponse.RequestId);
        var joinerId = joinedResponse.DeserializePayload<LobbyJoinedPayload>()!.Player.PlayerId;

        // Host is notified of the new player.
        var hostNotice = await host.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.LobbyJoined, hostNotice.Type);
        Assert.Null(hostNotice.RequestId);
        Assert.Equal(joinerId, hostNotice.DeserializePayload<LobbyJoinedPayload>()!.Player.PlayerId);
    }

    [Fact]
    public async Task Full_lobby_rejects_new_players()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var host = await _factory.ConnectReadyAsync(key);

        await host.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new { visibility = "public", maxPlayers = 1 });
        var created = (await host.ReceiveEnvelopeAsync()).DeserializePayload<LobbyCreatedPayload>()!;

        using var latecomer = await _factory.ConnectReadyAsync(key);
        await latecomer.SendMessageAsync(RealtimeMessageTypes.LobbyJoin, "j1", new { code = created.Code });

        var reply = await latecomer.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.RoomFull, reply.DeserializePayload<RealtimeError>()!.Code);
    }
}
