using Platform.Application.Abstractions;
using Platform.Application.Security;
using Platform.Domain.Accounts;

namespace Platform.Application.Accounts;

/// <summary>Result of issuing a verification link. <see cref="RawToken"/> is set only when one was issued.</summary>
public sealed record EmailVerificationIssueResult(bool Issued, string? RawToken)
{
    public static readonly EmailVerificationIssueResult Throttled = new(false, null);
    public static readonly EmailVerificationIssueResult NotNeeded = new(false, null);
}

/// <summary>
/// Email-address verification for password sign-up. Issuing a link stores only the hash of a
/// single-use token; consuming it marks the owning user's email verified. Mirrors
/// <see cref="MagicLinkService"/> but is keyed to an existing user and has a longer lifetime.
/// </summary>
public sealed class EmailVerificationService
{
    /// <summary>How long a verification link stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Minimum gap between verification-link requests for the same user.</summary>
    public static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(60);

    private readonly IEmailVerificationTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly IApiKeyGenerator _keys;
    private readonly IApiKeyHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public EmailVerificationService(
        IEmailVerificationTokenRepository tokens,
        IUserRepository users,
        IApiKeyGenerator keys,
        IApiKeyHasher hasher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _tokens = tokens;
        _users = users;
        _keys = keys;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Issues a verification token for a user. Returns the raw token so the caller can build the
    /// absolute link. Throttled per user; returns <see cref="EmailVerificationIssueResult.NotNeeded"/>
    /// if the user is already verified.
    /// </summary>
    public async Task<EmailVerificationIssueResult> IssueAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.IsEmailVerified)
        {
            return EmailVerificationIssueResult.NotNeeded;
        }

        var latest = await _tokens.GetLatestForUserAsync(user.Id, ct);
        if (latest is not null && _clock.UtcNow - latest.CreatedAtUtc < MinRequestInterval)
        {
            return EmailVerificationIssueResult.Throttled;
        }

        // The raw token is high-entropy; we store only its hash.
        var rawToken = _keys.GenerateSecretKey();
        var token = EmailVerificationToken.Create(
            _hasher.Hash(rawToken), user.Id, user.Email, _clock.UtcNow, Lifetime);
        await _tokens.AddAsync(token, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new EmailVerificationIssueResult(Issued: true, RawToken: rawToken);
    }

    /// <summary>
    /// Consumes a raw token: validates it (exists, unexpired, unused), marks it used, marks the
    /// owning user's email verified, and returns that user. Null if the token is invalid.
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

        var user = await _users.GetByIdAsync(token.UserId, ct);
        if (user is null)
        {
            return null;
        }

        token.Consume(_clock.UtcNow);
        user.MarkEmailVerified(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return user;
    }
}
