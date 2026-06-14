using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Security;

namespace Platform.Infrastructure.Security;

/// <summary>
/// Stateless player tokens of the form <c>{playerId}.{projectId}.{exp}.{hmac}</c>, signed
/// with HMAC-SHA256. Validation needs no database lookup.
/// </summary>
internal sealed class PlayerTokenService : IPlayerTokenService
{
    private readonly byte[] _secret;
    private readonly TimeSpan _lifetime;
    private readonly IClock _clock;

    public PlayerTokenService(IOptions<PlayerTokenOptions> options, IClock clock)
    {
        _clock = clock;
        var value = options.Value;
        _lifetime = value.TokenLifetime;
        _secret = string.IsNullOrWhiteSpace(value.TokenSecret)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(value.TokenSecret);
    }

    public PlayerToken Issue(string projectId, string playerId)
    {
        var expiresAt = _clock.UtcNow.Add(_lifetime);
        var payload = $"{playerId}.{projectId}.{expiresAt.ToUnixTimeSeconds()}";
        return new PlayerToken($"{payload}.{Sign(payload)}", expiresAt);
    }

    public PlayerTokenValidation Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return PlayerTokenValidation.Invalid;
        }

        var parts = token.Split('.');
        if (parts.Length != 4)
        {
            return PlayerTokenValidation.Invalid;
        }

        var (playerId, projectId, expText, signature) = (parts[0], parts[1], parts[2], parts[3]);
        var payload = $"{playerId}.{projectId}.{expText}";

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(Sign(payload))))
        {
            return PlayerTokenValidation.Invalid;
        }

        if (!long.TryParse(expText, out var exp))
        {
            return PlayerTokenValidation.Invalid;
        }

        return _clock.UtcNow.ToUnixTimeSeconds() > exp
            ? PlayerTokenValidation.Expired(playerId, projectId)
            : PlayerTokenValidation.Valid(playerId, projectId);
    }

    private string Sign(string payload)
    {
        var hash = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
