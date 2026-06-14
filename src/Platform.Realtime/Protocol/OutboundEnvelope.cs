namespace Platform.Realtime.Protocol;

/// <summary>
/// Wire shape for messages the server sends. Mirrors
/// <see cref="Platform.Contracts.Realtime.RealtimeEnvelope"/> but carries a strongly-typed
/// payload object for serialization (the contract uses raw JSON for reads).
/// </summary>
internal sealed record OutboundEnvelope(string Type, string? RequestId, object? Payload);
