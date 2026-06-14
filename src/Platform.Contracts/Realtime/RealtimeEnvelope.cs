using System.Text.Json;

namespace Platform.Contracts.Realtime;

/// <summary>
/// Canonical realtime message envelope (protocol v1):
/// <code>{ "type": "message.type", "requestId": "optional", "payload": { } }</code>
/// <para>
/// <see cref="Payload"/> is kept as raw JSON so each message type interprets it
/// independently. <see cref="RequestId"/> is optional and, when present, is echoed
/// back on the response for request/response correlation.
/// </para>
/// </summary>
public sealed record RealtimeEnvelope
{
    public required string Type { get; init; }

    public string? RequestId { get; init; }

    public JsonElement? Payload { get; init; }
}
