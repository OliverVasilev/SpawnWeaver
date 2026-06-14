namespace Platform.Application.Security;

/// <summary>An issued player token and when it expires.</summary>
public sealed record PlayerToken(string Value, DateTimeOffset ExpiresAtUtc);

/// <summary>Result of validating a player token.</summary>
public sealed record PlayerTokenValidation(bool IsValid, bool IsExpired, string? PlayerId, string? ProjectId)
{
    public static PlayerTokenValidation Invalid { get; } = new(false, false, null, null);

    public static PlayerTokenValidation Expired(string playerId, string projectId)
        => new(false, true, playerId, projectId);

    public static PlayerTokenValidation Valid(string playerId, string projectId)
        => new(true, false, playerId, projectId);
}

/// <summary>
/// Issues and validates stateless, signed player tokens. A token both identifies a player
/// (stable across reconnects) and serves as the reconnect credential.
/// </summary>
public interface IPlayerTokenService
{
    PlayerToken Issue(string projectId, string playerId);

    PlayerTokenValidation Validate(string token);
}
