using System.Text;
using System.Text.Json;
using Platform.Contracts.Realtime;
using Platform.Realtime.Protocol;
using Xunit;

namespace Platform.Tests.Protocol;

public sealed class EnvelopeSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Parses_valid_envelope_with_payload_and_request_id()
    {
        var utf8 = Encoding.UTF8.GetBytes(
            """{"type":"room.join","requestId":"req_1","payload":{"roomCode":"ABCD12"}}""");

        var ok = EnvelopeReader.TryParse(utf8, out var envelope, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("room.join", envelope!.Type);
        Assert.Equal("req_1", envelope.RequestId);
        Assert.Equal("ABCD12", envelope.Payload!.Value.GetProperty("roomCode").GetString());
    }

    [Fact]
    public void Parses_envelope_without_optional_fields()
    {
        var utf8 = Encoding.UTF8.GetBytes("""{"type":"ping"}""");

        var ok = EnvelopeReader.TryParse(utf8, out var envelope, out _);

        Assert.True(ok);
        Assert.Equal("ping", envelope!.Type);
        Assert.Null(envelope.RequestId);
        Assert.Null(envelope.Payload);
    }

    [Fact]
    public void Fails_when_type_is_missing()
    {
        var utf8 = Encoding.UTF8.GetBytes("""{"requestId":"req_1"}""");

        var ok = EnvelopeReader.TryParse(utf8, out var envelope, out var error);

        Assert.False(ok);
        Assert.Null(envelope);
        Assert.NotNull(error);
    }

    [Fact]
    public void Fails_on_malformed_json()
    {
        var utf8 = Encoding.UTF8.GetBytes("{ not valid json ");

        var ok = EnvelopeReader.TryParse(utf8, out var envelope, out var error);

        Assert.False(ok);
        Assert.Null(envelope);
        Assert.NotNull(error);
    }

    [Fact]
    public void Contract_round_trips_through_json()
    {
        var json = """{"type":"game.event","requestId":"req_5","payload":{"x":10,"y":4}}""";

        var envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(json, JsonOptions)!;
        var reserialized = JsonSerializer.Serialize(envelope, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<RealtimeEnvelope>(reserialized, JsonOptions)!;

        Assert.Equal(envelope.Type, roundTripped.Type);
        Assert.Equal(envelope.RequestId, roundTripped.RequestId);
        Assert.Equal(
            envelope.Payload!.Value.GetProperty("x").GetInt32(),
            roundTripped.Payload!.Value.GetProperty("x").GetInt32());
    }
}
