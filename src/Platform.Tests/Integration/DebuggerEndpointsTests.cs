using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Contracts.Admin;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class DebuggerEndpointsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public DebuggerEndpointsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Session_detail_exposes_a_timeline_with_connect_and_rejection_events()
    {
        var key = await _factory.CreateProjectKeyAsync("Debugger Game");
        using var socket = await _factory.ConnectAsync(key);

        // The welcome carries the connection id.
        var welcome = await socket.ReceiveEnvelopeAsync();
        var connectionId = welcome.Payload!.Value.GetProperty("connectionId").GetString();
        Assert.False(string.IsNullOrEmpty(connectionId));

        // Trigger a rejection: an unknown message type.
        await socket.SendMessageAsync("totally.unknown.type", requestId: null, payload: null);
        var error = await socket.ReceiveEnvelopeAsync();
        Assert.Equal("error", error.Type);

        var http = _factory.CreateClient();
        var response = await http.GetFromJsonAsync<AdminSessionDetailResponse>($"/api/admin/sessions/{connectionId}");

        Assert.NotNull(response);
        var session = response!.Session!;
        Assert.Equal(connectionId, session.ConnectionId);
        Assert.Equal("authenticated", session.AuthStatus);
        Assert.Contains(session.Timeline, e => e.Kind == "connected");
        Assert.Contains(session.Timeline, e => e.Kind == "rejected" && e.Detail.Contains("unknown_message_type"));
    }

    [Fact]
    public async Task Error_explorer_aggregates_errors_with_a_suggested_fix()
    {
        var key = await _factory.CreateProjectKeyAsync("Error Game");
        using var socket = await _factory.ConnectReadyAsync(key);

        // Join a room that does not exist -> room_not_found.
        await socket.SendMessageAsync("room.join", requestId: null, new { roomCode = "NOPE99" });
        await socket.ReceiveEnvelopeAsync();

        var http = _factory.CreateClient();
        var response = await http.GetFromJsonAsync<AdminErrorsResponse>("/api/admin/errors");

        Assert.NotNull(response);
        var bucket = Assert.Single(response!.Errors, e => e.Code == "room_not_found");
        Assert.True(bucket.Count >= 1);
        Assert.True(bucket.AffectedSessions >= 1);
        Assert.False(string.IsNullOrWhiteSpace(bucket.SuggestedFix));
    }

    [Fact]
    public async Task Room_inspector_returns_members_and_host()
    {
        var key = await _factory.CreateProjectKeyAsync("Room Inspect Game");
        using var socket = await _factory.ConnectReadyAsync(key);

        await socket.SendMessageAsync("room.create", requestId: null, new { playerName = "Alice" });
        var created = await socket.ReceiveEnvelopeAsync();
        var roomId = created.Payload!.Value.GetProperty("roomId").GetString();

        var http = _factory.CreateClient();
        var response = await http.GetFromJsonAsync<AdminRoomDetailResponse>($"/api/admin/rooms/{roomId}");

        Assert.NotNull(response);
        var room = response!.Room!;
        Assert.Equal(roomId, room.RoomId);
        Assert.False(room.IsLobby);
        Assert.Equal(1, room.MemberCount);
        Assert.NotNull(room.HostPlayerId);
        Assert.Contains(room.Members, m => m.PlayerName == "Alice");
    }

    [Fact]
    public async Task Matchmaking_inspector_is_served()
    {
        var http = _factory.CreateClient();

        var response = await http.GetFromJsonAsync<AdminMatchmakingResponse>("/api/admin/matchmaking");

        Assert.NotNull(response);
        Assert.True(response!.TotalWaiting >= 0);
    }

    [Fact]
    public async Task Unknown_session_detail_returns_404()
    {
        var http = _factory.CreateClient();

        var response = await http.GetAsync("/api/admin/sessions/conn_does_not_exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
