using System.Net.WebSockets;
using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeRoomsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeRoomsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_join_players_and_leave_lifecycle()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var alice = await ConnectReadyAsync(key);

        // Alice creates a room.
        await alice.SendMessageAsync(RealtimeMessageTypes.RoomCreate, "c1", new { playerName = "Alice" });
        var created = await alice.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomCreated, created.Type);
        Assert.Equal("c1", created.RequestId);

        var createdPayload = created.DeserializePayload<RoomCreatedPayload>()!;
        Assert.Single(createdPayload.Players);
        var roomCode = createdPayload.RoomCode;
        var roomId = createdPayload.RoomId;

        // Bob joins by code.
        using var bob = await ConnectReadyAsync(key);
        await bob.SendMessageAsync(RealtimeMessageTypes.RoomJoin, "j1", new { roomCode, playerName = "Bob" });

        var joinedResponse = await bob.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomJoined, joinedResponse.Type);
        Assert.Equal("j1", joinedResponse.RequestId);
        var joinedPayload = joinedResponse.DeserializePayload<RoomJoinedPayload>()!;
        Assert.Equal(2, joinedPayload.Players.Count);
        var bobId = joinedPayload.Player.PlayerId;

        // Alice is notified that Bob joined.
        var aliceNotice = await alice.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomJoined, aliceNotice.Type);
        Assert.Null(aliceNotice.RequestId);
        Assert.Equal(bobId, aliceNotice.DeserializePayload<RoomJoinedPayload>()!.Player.PlayerId);

        // Bob lists players.
        await bob.SendMessageAsync(RealtimeMessageTypes.RoomPlayers, "p1", new { roomId });
        var playersResponse = await bob.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomPlayers, playersResponse.Type);
        Assert.Equal(2, playersResponse.DeserializePayload<RoomPlayersPayload>()!.Players.Count);

        // Bob leaves; Alice is notified.
        await bob.SendMessageAsync(RealtimeMessageTypes.RoomLeave, "l1", new { roomId });
        var leftAck = await bob.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomLeft, leftAck.Type);
        Assert.Equal("l1", leftAck.RequestId);

        var aliceLeftNotice = await alice.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomLeft, aliceLeftNotice.Type);
        Assert.Equal(bobId, aliceLeftNotice.DeserializePayload<RoomLeftPayload>()!.PlayerId);
    }

    [Fact]
    public async Task Disconnect_notifies_remaining_room_members()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var alice = await ConnectReadyAsync(key);
        await alice.SendMessageAsync(RealtimeMessageTypes.RoomCreate, "c1", null);
        var created = (await alice.ReceiveEnvelopeAsync()).DeserializePayload<RoomCreatedPayload>()!;

        var bob = await ConnectReadyAsync(key);
        await bob.SendMessageAsync(RealtimeMessageTypes.RoomJoin, "j1", new { roomCode = created.RoomCode });
        var bobId = (await bob.ReceiveEnvelopeAsync()).DeserializePayload<RoomJoinedPayload>()!.Player.PlayerId;
        await alice.ReceiveEnvelopeAsync(); // Alice's room.joined notice

        // Bob drops without leaving.
        await bob.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        bob.Dispose();

        var aliceNotice = await alice.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.RoomLeft, aliceNotice.Type);
        Assert.Equal(bobId, aliceNotice.DeserializePayload<RoomLeftPayload>()!.PlayerId);
    }

    [Fact]
    public async Task Join_unknown_code_returns_room_not_found()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await ConnectReadyAsync(key);

        await socket.SendMessageAsync(RealtimeMessageTypes.RoomJoin, "j1", new { roomCode = "ZZZZZZ" });

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.RoomNotFound, reply.DeserializePayload<RealtimeError>()!.Code);
    }

    [Fact]
    public async Task Join_without_room_code_returns_invalid_payload()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await ConnectReadyAsync(key);

        await socket.SendMessageAsync(RealtimeMessageTypes.RoomJoin, "j1", new { playerName = "Nobody" });

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.InvalidPayload, reply.DeserializePayload<RealtimeError>()!.Code);
    }

    private async Task<WebSocket> ConnectReadyAsync(string publicKey)
    {
        var socket = await _factory.ConnectAsync(publicKey);
        await socket.ReceiveEnvelopeAsync(); // consume the welcome
        return socket;
    }
}
