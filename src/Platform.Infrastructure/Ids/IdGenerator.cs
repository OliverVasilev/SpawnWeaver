using System.Security.Cryptography;
using Platform.Application.Abstractions;

namespace Platform.Infrastructure.Ids;

internal sealed class IdGenerator : IIdGenerator
{
    public string NewId(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        return $"{prefix}_{random}";
    }
}
