namespace Platform.Contracts.Realtime;

/// <summary>
/// Payload of the <c>connection.welcome</c> envelope sent right after connecting.
/// <para>
/// <see cref="PlayerId"/> is the stable identity for this player; <see cref="PlayerToken"/>
/// is the credential to present on reconnect (as the <c>playerToken</c> query parameter)
/// to resume the same identity. The client should store it.
/// </para>
/// </summary>
public sealed record ConnectionWelcomePayload(
    string ConnectionId,
    string PlayerId,
    string PlayerToken,
    DateTimeOffset TokenExpiresAtUtc,
    DateTimeOffset ServerTimeUtc);
