namespace Platform.Infrastructure.Security;

/// <summary>Options for player tokens (bound from the <c>Auth</c> config section).</summary>
public sealed class PlayerTokenOptions
{
    /// <summary>
    /// HMAC signing secret. If empty, a random per-process secret is used (tokens are then
    /// invalidated on restart). Set <c>Auth__TokenSecret</c> to keep tokens valid across restarts.
    /// </summary>
    public string? TokenSecret { get; set; }

    /// <summary>How long an issued token is valid.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
