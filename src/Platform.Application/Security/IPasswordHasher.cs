namespace Platform.Application.Security;

/// <summary>
/// Hashes and verifies user passwords. Unlike API keys (high-entropy, fast hash),
/// passwords are low-entropy and need a slow, salted KDF (PBKDF2/bcrypt-class).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash string (algorithm, salt, iterations, hash).</summary>
    string Hash(string password);

    /// <summary>Constant-time verification of a password against a stored hash.</summary>
    bool Verify(string password, string hash);
}
