using System.Security.Cryptography;
using Platform.Application.Security;

namespace Platform.Infrastructure.Security;

internal sealed class ApiKeyGenerator : IApiKeyGenerator
{
    public string GeneratePublicKey() => "pk_" + RandomToken(24);

    public string GenerateSecretKey() => "sk_" + RandomToken(32);

    private static string RandomToken(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);

        // URL-safe Base64 without padding.
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
