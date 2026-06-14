namespace Platform.Contracts.Realtime;

/// <summary>Stable machine-readable codes carried by <see cref="RealtimeError"/>.</summary>
public static class ProtocolErrorCodes
{
    /// <summary>The message could not be parsed as a protocol envelope.</summary>
    public const string MalformedMessage = "malformed_message";

    /// <summary>The envelope's <c>type</c> has no registered handler.</summary>
    public const string UnknownMessageType = "unknown_message_type";

    /// <summary>The message payload was missing or failed validation.</summary>
    public const string InvalidPayload = "invalid_payload";

    /// <summary>No room/lobby matches the given code/id (or it belongs to another project).</summary>
    public const string RoomNotFound = "room_not_found";

    /// <summary>The room/lobby is at its maximum player count.</summary>
    public const string RoomFull = "room_full";

    /// <summary>The message exceeded the maximum allowed size.</summary>
    public const string PayloadTooLarge = "payload_too_large";

    /// <summary>The connection sent messages faster than the allowed rate.</summary>
    public const string RateLimited = "rate_limited";

    // --- State sync (Milestone 23) ---

    /// <summary>The caller is not allowed to update this state (not the owner / not the host).</summary>
    public const string StateForbidden = "state_forbidden";

    /// <summary>The referenced entity does not exist.</summary>
    public const string EntityNotFound = "entity_not_found";

    /// <summary>A state limit was exceeded (too many entities).</summary>
    public const string StateLimitExceeded = "state_limit_exceeded";

    /// <summary>The resulting state exceeds the maximum allowed size.</summary>
    public const string StateTooLarge = "state_too_large";
}
