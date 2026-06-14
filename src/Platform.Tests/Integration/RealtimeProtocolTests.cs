using System.Net.WebSockets;
using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class RealtimeProtocolTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public RealtimeProtocolTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ping_returns_pong_correlated_by_request_id()
    {
        using var socket = await ConnectAndDrainWelcomeAsync();

        await socket.SendTextAsync("""{"type":"ping","requestId":"req_42"}""");

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Pong, reply.Type);
        Assert.Equal("req_42", reply.RequestId);
    }

    [Fact]
    public async Task Ping_without_request_id_still_returns_pong()
    {
        using var socket = await ConnectAndDrainWelcomeAsync();

        await socket.SendTextAsync("""{"type":"ping"}""");

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Pong, reply.Type);
        Assert.Null(reply.RequestId);
    }

    [Fact]
    public async Task Unknown_type_returns_structured_error_echoing_request_id()
    {
        using var socket = await ConnectAndDrainWelcomeAsync();

        await socket.SendTextAsync("""{"type":"does.not.exist","requestId":"req_7"}""");

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal("req_7", reply.RequestId);

        var error = reply.DeserializePayload<RealtimeError>();
        Assert.NotNull(error);
        Assert.Equal(ProtocolErrorCodes.UnknownMessageType, error!.Code);
    }

    [Fact]
    public async Task Malformed_json_returns_structured_error()
    {
        using var socket = await ConnectAndDrainWelcomeAsync();

        await socket.SendTextAsync("{ this is not json ");

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);

        var error = reply.DeserializePayload<RealtimeError>();
        Assert.NotNull(error);
        Assert.Equal(ProtocolErrorCodes.MalformedMessage, error!.Code);
    }

    [Fact]
    public async Task Message_without_type_returns_malformed_error()
    {
        using var socket = await ConnectAndDrainWelcomeAsync();

        await socket.SendTextAsync("""{"requestId":"req_9","payload":{}}""");

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);

        var error = reply.DeserializePayload<RealtimeError>();
        Assert.Equal(ProtocolErrorCodes.MalformedMessage, error!.Code);
    }

    private async Task<WebSocket> ConnectAndDrainWelcomeAsync()
    {
        var publicKey = await _factory.CreateProjectKeyAsync();
        var socket = await _factory.ConnectAsync(publicKey);
        await socket.ReceiveEnvelopeAsync(); // consume the welcome message
        return socket;
    }
}
