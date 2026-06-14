namespace Platform.Application.Security;

/// <summary>
/// Hashes and verifies secret API keys. Keys are high-entropy random values, so a
/// fast deterministic hash (allowing hash-based lookup) is appropriate.
/// </summary>
public interface IApiKeyHasher
{
    string Hash(string apiKey);

    bool Verify(string apiKey, string hash);
}
