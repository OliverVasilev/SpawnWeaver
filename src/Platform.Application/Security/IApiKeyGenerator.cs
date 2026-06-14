namespace Platform.Application.Security;

/// <summary>
/// Generates API keys for projects: a non-secret public key (safe to embed in a
/// game client) and a secret key (server-side only, shown to the developer once).
/// </summary>
public interface IApiKeyGenerator
{
    /// <summary>Public, embeddable project key, e.g. <c>pk_…</c>.</summary>
    string GeneratePublicKey();

    /// <summary>Secret server-side key, e.g. <c>sk_…</c>. Returned to the developer only once.</summary>
    string GenerateSecretKey();
}
