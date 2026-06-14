using System.Text.Json;

namespace Platform.Contracts.Realtime;

/// <summary>
/// Payload for a <c>game.event</c> sent by a client. <see cref="Data"/> is opaque
/// application JSON relayed unchanged to other room members.
/// </summary>
public sealed record GameEventRequest(string RoomId, string Event, JsonElement? Data);

/// <summary>
/// Payload for a <c>game.event</c> relayed to other room members. Adds
/// <see cref="FromPlayerId"/> identifying the sender.
/// </summary>
public sealed record GameEventPayload(string RoomId, string Event, JsonElement? Data, string FromPlayerId);
