using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeStateSyncTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeStateSyncTests(SpawnWeaverApiFactory factory) => _factory = factory;

    private static async Task<(WebSocket Socket, string PlayerId)> ConnectAsync(WebApplicationFactory<Program> factory, string key)
    {
        var socket = await factory.ConnectAsync(key);
        var welcome = await socket.ReceiveEnvelopeAsync();
        return (socket, welcome.Payload!.Value.GetProperty("playerId").GetString()!);
    }

    /// <summary>Host A creates a room; B joins it. Returns both sockets, ids, and the room id.</summary>
    private async Task<(WebSocket A, string AId, WebSocket B, string BId, string RoomId)> SetupRoomAsync()
    {
        var key = await _factory.CreateProjectKeyAsync("State Game");
        var (a, aId) = await ConnectAsync(_factory, key);
        var (b, bId) = await ConnectAsync(_factory, key);

        await a.SendMessageAsync("room.create", requestId: null, new { playerName = "A" });
        var created = await a.ReceiveEnvelopeAsync();
        var roomId = created.Payload!.Value.GetProperty("roomId").GetString()!;
        var code = created.Payload!.Value.GetProperty("roomCode").GetString()!;

        await b.SendMessageAsync("room.join", requestId: null, new { roomCode = code });
        await b.ReceiveEnvelopeAsync(); // room.joined for B
        await a.ReceiveEnvelopeAsync(); // room.joined broadcast to A

        return (a, aId, b, bId, roomId);
    }

    [Fact]
    public async Task Room_state_patch_is_broadcast_to_members()
    {
        var (a, _, b, _, roomId) = await SetupRoomAsync();

        await a.SendMessageAsync("state.room.patch", requestId: null,
            new { roomId, patch = new { phase = "combat", round = 2 } });

        var changed = await b.ReceiveEnvelopeAsync();
        Assert.Equal("state.room.changed", changed.Type);
        Assert.Equal("combat", changed.Payload!.Value.GetProperty("patch").GetProperty("phase").GetString());
        Assert.Equal("combat", changed.Payload!.Value.GetProperty("state").GetProperty("phase").GetString());
    }

    [Fact]
    public async Task Entity_state_can_be_set_patched_and_deleted()
    {
        var (a, aId, b, _, roomId) = await SetupRoomAsync();

        // Set.
        await a.SendMessageAsync("state.entity.set", requestId: null,
            new { roomId, entityId = "e1", state = new { x = 1, y = 2 } });
        var set = await b.ReceiveEnvelopeAsync();
        Assert.Equal("state.entity.changed", set.Type);
        Assert.Equal("e1", set.Payload!.Value.GetProperty("entityId").GetString());
        Assert.Equal(aId, set.Payload!.Value.GetProperty("ownerId").GetString());
        Assert.Equal(1, set.Payload!.Value.GetProperty("state").GetProperty("x").GetInt32());

        // Patch (merge).
        await a.SendMessageAsync("state.entity.patch", requestId: null,
            new { roomId, entityId = "e1", patch = new { x = 5 } });
        var patched = await b.ReceiveEnvelopeAsync();
        Assert.Equal("state.entity.changed", patched.Type);
        Assert.Equal(5, patched.Payload!.Value.GetProperty("state").GetProperty("x").GetInt32());
        Assert.Equal(2, patched.Payload!.Value.GetProperty("state").GetProperty("y").GetInt32());

        // Delete.
        await a.SendMessageAsync("state.entity.delete", requestId: null, new { roomId, entityId = "e1" });
        var deleted = await b.ReceiveEnvelopeAsync();
        Assert.Equal("state.entity.deleted", deleted.Type);
        Assert.Equal("e1", deleted.Payload!.Value.GetProperty("entityId").GetString());
    }

    [Fact]
    public async Task Late_joiner_receives_a_full_state_snapshot()
    {
        var key = await _factory.CreateProjectKeyAsync("Snapshot Game");
        var (a, _) = await ConnectAsync(_factory, key);

        await a.SendMessageAsync("room.create", requestId: null, new { playerName = "A" });
        var created = await a.ReceiveEnvelopeAsync();
        var roomId = created.Payload!.Value.GetProperty("roomId").GetString()!;
        var code = created.Payload!.Value.GetProperty("roomCode").GetString()!;

        // A seeds some state while alone.
        await a.SendMessageAsync("state.room.patch", requestId: null, new { roomId, patch = new { map = "forest" } });
        await a.ReceiveEnvelopeAsync(); // A's own room.changed
        await a.SendMessageAsync("state.entity.set", requestId: null,
            new { roomId, entityId = "boss", state = new { hp = 100 } });
        await a.ReceiveEnvelopeAsync(); // A's own entity.changed

        // B joins and should receive room.joined then a snapshot.
        var (b, _) = await ConnectAsync(_factory, key);
        await b.SendMessageAsync("room.join", requestId: null, new { roomCode = code });
        await b.ReceiveEnvelopeAsync(); // room.joined
        var snapshot = await b.ReceiveEnvelopeAsync();

        Assert.Equal("state.snapshot", snapshot.Type);
        Assert.Equal("forest", snapshot.Payload!.Value.GetProperty("roomState").GetProperty("map").GetString());
        var entities = snapshot.Payload!.Value.GetProperty("entities");
        Assert.Equal(1, entities.GetArrayLength());
        Assert.Equal("boss", entities[0].GetProperty("entityId").GetString());
    }

    [Fact]
    public async Task Updating_an_entity_you_do_not_own_is_rejected()
    {
        var (a, _, b, _, roomId) = await SetupRoomAsync();

        await a.SendMessageAsync("state.entity.set", requestId: null,
            new { roomId, entityId = "owned", state = new { v = 1 } });
        await b.ReceiveEnvelopeAsync(); // B sees A's entity.changed

        await b.SendMessageAsync("state.entity.patch", requestId: null,
            new { roomId, entityId = "owned", patch = new { v = 99 } });
        var rejected = await b.ReceiveEnvelopeAsync();

        Assert.Equal("state.update.rejected", rejected.Type);
        Assert.Equal("state_forbidden", rejected.Payload!.Value.GetProperty("code").GetString());
        Assert.Equal("owned", rejected.Payload!.Value.GetProperty("target").GetString());
    }

    [Fact]
    public async Task Non_host_cannot_patch_room_state()
    {
        var (_, _, b, _, roomId) = await SetupRoomAsync();

        await b.SendMessageAsync("state.room.patch", requestId: null, new { roomId, patch = new { phase = "x" } });
        var rejected = await b.ReceiveEnvelopeAsync();

        Assert.Equal("state.update.rejected", rejected.Type);
        Assert.Equal("state_forbidden", rejected.Payload!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Oversized_entity_state_is_rejected()
    {
        var (a, _, _, _, roomId) = await SetupRoomAsync();

        var big = new string('x', 5000); // exceeds the 4 KB per-entity cap
        await a.SendMessageAsync("state.entity.set", requestId: null,
            new { roomId, entityId = "big", state = new { blob = big } });
        var rejected = await a.ReceiveEnvelopeAsync();

        Assert.Equal("state.update.rejected", rejected.Type);
        Assert.Equal("state_too_large", rejected.Payload!.Value.GetProperty("code").GetString());
    }
}
