using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Platform.Realtime.Connections;

namespace Platform.Realtime.Protocol;

/// <summary>Serializes outbound envelopes to UTF-8 JSON and writes them to a connection.</summary>
internal static class RealtimeMessageSender
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Relaxed escaping keeps the wire JSON readable (e.g. ' instead of ').
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Serializes an envelope to UTF-8 JSON (reused across recipients for broadcasts).</summary>
    public static byte[] Serialize(string type, string? requestId, object? payload)
        => JsonSerializer.SerializeToUtf8Bytes(new OutboundEnvelope(type, requestId, payload), Options);

    /// <summary>Sends pre-serialized bytes to a connection.</summary>
    public static Task SendRawAsync(RealtimeConnection connection, ReadOnlyMemory<byte> bytes, CancellationToken ct)
        => connection.SendTextAsync(bytes, ct);

    public static Task SendAsync(
        RealtimeConnection connection,
        string type,
        string? requestId,
        object? payload,
        CancellationToken ct)
        => connection.SendTextAsync(Serialize(type, requestId, payload), ct);
}
