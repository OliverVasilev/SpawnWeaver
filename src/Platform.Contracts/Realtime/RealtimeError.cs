namespace Platform.Contracts.Realtime;

/// <summary>
/// Payload of an <c>error</c> envelope. <see cref="Code"/> is a stable identifier from
/// <see cref="ProtocolErrorCodes"/>; <see cref="Message"/> is human-readable.
/// </summary>
public sealed record RealtimeError(string Code, string Message);
