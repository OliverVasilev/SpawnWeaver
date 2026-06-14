namespace Platform.Application.Accounts;

/// <summary>
/// Transactional email configuration (bound from the <c>Email</c> section). When a provider API
/// key is present the real sender is used and email verification is enforced; otherwise the dev
/// (logging) sender is used and accounts are auto-verified locally.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>"dev" or "resend". When unset, it is inferred: "resend" if an API key is present.</summary>
    public string? Provider { get; set; }

    /// <summary>Resend API key. Supplied via environment/secret only — never committed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The "From" address, e.g. "SpawnWeaver &lt;noreply@spawnweaver.example&gt;" or a bare address.</summary>
    public string FromAddress { get; set; } = "onboarding@resend.dev";

    /// <summary>Display name used when <see cref="FromAddress"/> is a bare address.</summary>
    public string FromName { get; set; } = "SpawnWeaver";

    /// <summary>
    /// Absolute public base URL (e.g. https://spawnweaver.example) used to build links in emails.
    /// When empty, links fall back to the incoming request's scheme + host.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>True when a real provider is configured (an API key is present).</summary>
    public bool HasRealProvider => !string.IsNullOrWhiteSpace(ApiKey);
}
