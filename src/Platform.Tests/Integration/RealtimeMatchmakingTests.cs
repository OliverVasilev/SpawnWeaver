using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeMatchmakingTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeMatchmakingTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Two_players_in_the_same_bucket_are_matched()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var a = await _factory.ConnectReadyAsync(key);
        await a.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "a1", new { gameMode = "duel" });
        Assert.Equal(RealtimeMessageTypes.MatchmakingQueued, (await a.ReceiveEnvelopeAsync()).Type);

        using var b = await _factory.ConnectReadyAsync(key);
        await b.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "b1", new { gameMode = "duel" });

        var bMatch = await b.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.MatchFound, bMatch.Type);
        Assert.Equal("b1", bMatch.RequestId);
        var bPayload = bMatch.DeserializePayload<MatchFoundPayload>()!;
        Assert.Equal(2, bPayload.Players.Count);

        var aMatch = await a.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.MatchFound, aMatch.Type);
        Assert.Null(aMatch.RequestId);
        Assert.Equal(bPayload.RoomId, aMatch.DeserializePayload<MatchFoundPayload>()!.RoomId);
    }

    [Fact]
    public async Task Different_game_modes_do_not_match()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var a = await _factory.ConnectReadyAsync(key);
        await a.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "a1", new { gameMode = "duel" });
        await a.ReceiveEnvelopeAsync(); // queued

        using var b = await _factory.ConnectReadyAsync(key);
        await b.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "b1", new { gameMode = "coop" });

        // B should be queued, not matched.
        Assert.Equal(RealtimeMessageTypes.MatchmakingQueued, (await b.ReceiveEnvelopeAsync()).Type);
    }

    [Fact]
    public async Task Leaving_the_queue_prevents_matching()
    {
        var key = await _factory.CreateProjectKeyAsync();

        using var a = await _factory.ConnectReadyAsync(key);
        await a.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "a1", new { gameMode = "duel" });
        await a.ReceiveEnvelopeAsync(); // queued

        await a.SendMessageAsync(RealtimeMessageTypes.MatchmakingLeave, "a2", null);
        Assert.Equal(RealtimeMessageTypes.MatchmakingLeft, (await a.ReceiveEnvelopeAsync()).Type);

        using var b = await _factory.ConnectReadyAsync(key);
        await b.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "b1", new { gameMode = "duel" });

        // A left, so B just waits.
        Assert.Equal(RealtimeMessageTypes.MatchmakingQueued, (await b.ReceiveEnvelopeAsync()).Type);
    }

    [Fact]
    public async Task Join_without_game_mode_returns_invalid_payload()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await _factory.ConnectReadyAsync(key);

        await socket.SendMessageAsync(RealtimeMessageTypes.MatchmakingJoin, "a1", new { region = "eu" });

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.InvalidPayload, reply.DeserializePayload<RealtimeError>()!.Code);
    }
}
