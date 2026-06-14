using System.Security.Cryptography;
using Platform.Application.Security;

namespace Platform.Infrastructure.Security;

/// <summary>
/// PBKDF2 (HMAC-SHA256) password hashing with a per-password random salt. The stored
/// string is self-describing — <c>pbkdf2$sha256$iterations$saltB64$hashB64</c> — so the
/// iteration count can be raised over time without breaking existing hashes.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "pbkdf2$sha256$";

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (password is null || string.IsNullOrEmpty(hash) || !hash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = hash.Split('$');
        // pbkdf2 $ sha256 $ iterations $ salt $ hash
        if (parts.Length != 5 || !int.TryParse(parts[2], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
