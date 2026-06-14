using System.Net.WebSockets;
using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeGameEventsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeGameEventsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Game_event_is_relayed_to_other_room_members()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var alice = await _factory.ConnectReadyAsync(key);
        await alice.SendMessageAsync(RealtimeMessageTypes.RoomCreate, "c1", null);
        var created = (await alice.ReceiveEnvelopeAsync()).DeserializePayload<RoomCreatedPayload>()!;
        var aliceId = created.PlayerId;

        using var bob = await _factory.ConnectReadyAsync(key);
        await bob.SendMessageAsync(RealtimeMessageTypes.RoomJoin, "j1", new { roomCode = created.RoomCode });
        await bob.ReceiveEnvelopeAsync();   // room.joined response
        await alice.ReceiveEnvelopeAsync(); // room.joined broadcast

        // Alice sends a game event; Bob should receive it.
        await alice.SendMessageAsync(
            RealtimeMessageTypes.GameEvent, null,
            new { roomId = created.RoomId, @event = "player_moved", data = new { x = 10, y = 5 } });

        var relayed = await bob.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.GameEvent, relayed.Type);

        var payload = relayed.DeserializePayload<GameEventPayload>()!;
        Assert.Equal("player_moved", payload.Event);
        Assert.Equal(aliceId, payload.FromPlayerId);
        Assert.Equal(10, payload.Data!.Value.GetProperty("x").GetInt32());
        Assert.Equal(5, payload.Data.Value.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task Game_event_for_a_room_you_are_not_in_is_rejected()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await _factory.ConnectReadyAsync(key);

        await socket.SendMessageAsync(
            RealtimeMessageTypes.GameEvent, "g1", new { roomId = "room_nope", @event = "x" });

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.RoomNotFound, reply.DeserializePayload<RealtimeError>()!.Code);
    }

    [Fact]
    public async Task Oversized_message_is_rejected()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await _factory.ConnectReadyAsync(key);

        await socket.SendTextAsync(new string('a', 20_000)); // exceeds the 16 KB limit

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.PayloadTooLarge, reply.DeserializePayload<RealtimeError>()!.Code);
    }

    [Fact]
    public async Task Spammy_client_is_rate_limited()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await _factory.ConnectReadyAsync(key);

        await socket.SendMessageAsync(RealtimeMessageTypes.RoomCreate, "c1", null);
        var roomId = (await socket.ReceiveEnvelopeAsync()).DeserializePayload<RoomCreatedPayload>()!.RoomId;

        // Blast far more events than the burst allowance; alone in the room these succeed
        // silently until the bucket empties, after which we get rate_limited errors.
        for (var i = 0; i < 150; i++)
        {
            await socket.SendMessageAsync(
                RealtimeMessageTypes.GameEvent, null, new { roomId, @event = "spam" });
        }

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.RateLimited, reply.DeserializePayload<RealtimeError>()!.Code);
    }
}
