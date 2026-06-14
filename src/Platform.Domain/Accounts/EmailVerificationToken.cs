namespace Platform.Domain.Accounts;

/// <summary>
/// A single-use, short-lived email-verification token. Only the <em>hash</em> of the token is
/// stored; the raw token travels in the emailed link. Consuming it marks the owning user's
/// email as verified. Unlike <see cref="LoginToken"/>, this is keyed to an existing
/// <see cref="UserId"/> rather than provisioning an account.
/// </summary>
public sealed class EmailVerificationToken
{
    public string TokenHash { get; private set; }
    public string UserId { get; private set; }
    public string Email { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    // EF Core materialization.
    private EmailVerificationToken()
    {
        TokenHash = null!;
        UserId = null!;
        Email = null!;
    }

    private EmailVerificationToken(
        string tokenHash, string userId, string email, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        TokenHash = tokenHash;
        UserId = userId;
        Email = email;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static EmailVerificationToken Create(
        string tokenHash, string userId, string email, DateTimeOffset createdAtUtc, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new EmailVerificationToken(
            tokenHash, userId, User.NormalizeEmail(email), createdAtUtc, createdAtUtc + lifetime);
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAtUtc is null && now < ExpiresAtUtc;

    public void Consume(DateTimeOffset now) => ConsumedAtUtc = now;
}
