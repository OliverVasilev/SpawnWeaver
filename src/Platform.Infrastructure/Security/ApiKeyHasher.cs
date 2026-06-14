using System.Security.Cryptography;
using System.Text;
using Platform.Application.Security;

namespace Platform.Infrastructure.Security;

/// <summary>
/// SHA-256 hashing of secret API keys. The keys are 256-bit random values, so a
/// fast deterministic hash is sufficient and lets us look up by hash later.
/// </summary>
internal sealed class ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool Verify(string apiKey, string hash)
    {
        var computed = Hash(apiKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hash));
    }
}
