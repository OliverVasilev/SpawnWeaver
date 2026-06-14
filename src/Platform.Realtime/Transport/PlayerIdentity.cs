namespace Platform.Realtime.Transport;

/// <summary>The authenticated player identity established at connect time.</summary>
internal sealed record PlayerIdentity(string PlayerId, string Token, DateTimeOffset TokenExpiresAtUtc);
