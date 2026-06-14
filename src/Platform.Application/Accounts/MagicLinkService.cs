using Platform.Application.Abstractions;
using Platform.Application.Security;
using Platform.Domain.Accounts;

namespace Platform.Application.Accounts;

/// <summary>Result of requesting a magic link. <see cref="RawToken"/> is set only on success.</summary>
public sealed record MagicLinkRequestResult(bool Sent, string? RawToken)
{
    public static readonly MagicLinkRequestResult Throttled = new(false, null);
}

/// <summary>
/// Passwordless email sign-in (Milestone: free-beta one-click auth). Requesting a link issues a
/// single-use, short-lived token (only its hash is stored); consuming it provisions the account
/// on first use and signs the user in. Identified by email, so it's rate-limitable and bannable.
/// </summary>
public sealed class MagicLinkService
{
    /// <summary>How long a magic link stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Minimum gap between link requests for the same email.</summary>
    public static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(30);

    private readonly ILoginTokenRepository _tokens;
    private readonly AccountService _accounts;
    private readonly IEmailSender _email;
    private readonly IApiKeyGenerator _keys;
    private readonly IApiKeyHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MagicLinkService(
        ILoginTokenRepository tokens,
        AccountService accounts,
        IEmailSender email,
        IApiKeyGenerator keys,
        IApiKeyHasher hasher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _tokens = tokens;
        _accounts = accounts;
        _email = email;
        _keys = keys;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>True for the dev (logging) email sender, so the UI may reveal the link locally.</summary>
    public bool IsDevSender => _email.IsDevSender;

    /// <summary>
    /// Issues a magic link for an email and "sends" it. Returns the raw token so the caller can
    /// build the absolute link. Throttled per email; an invalid email returns "sent" without
    /// issuing anything (no account enumeration).
    /// </summary>
    public async Task<MagicLinkRequestResult> RequestAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > User.MaxEmailLength)
        {
            return new MagicLinkRequestResult(Sent: true, RawToken: null);
        }

        var normalizedEmail = User.NormalizeEmail(email);

        var latest = await _tokens.GetLatestForEmailAsync(normalizedEmail, ct);
        if (latest is not null && _clock.UtcNow - latest.CreatedAtUtc < MinRequestInterval)
        {
            return MagicLinkRequestResult.Throttled;
        }

        // The raw token is high-entropy; we store only its hash.
        var rawToken = _keys.GenerateSecretKey();
        var token = LoginToken.Create(_hasher.Hash(rawToken), normalizedEmail, _clock.UtcNow, Lifetime);
        await _tokens.AddAsync(token, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new MagicLinkRequestResult(Sent: true, RawToken: rawToken);
    }

    /// <summary>Sends the email for a freshly issued token.</summary>
    public Task SendAsync(string email, string link, CancellationToken ct = default)
        => _email.SendMagicLinkAsync(email, link, ct);

    /// <summary>
    /// Consumes a raw token: validates it (exists, unexpired, unused), marks it used, and
    /// returns the signed-in user (provisioning the account on first use). Null if invalid.
    /// </summary>
    public async Task<User?> ConsumeAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var token = await _tokens.GetByHashAsync(_hasher.Hash(rawToken), ct);
        if (token is null || !token.IsUsable(_clock.UtcNow))
        {
            return null;
        }

        token.Consume(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return await _accounts.GetOrCreateByEmailAsync(token.Email, ct);
    }
}
