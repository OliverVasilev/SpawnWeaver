namespace Platform.Domain.Accounts;

/// <summary>
/// A single-use, short-lived magic-link login token. Only the <em>hash</em> of the token is
/// stored; the raw token travels in the emailed link. Consuming it signs the user in
/// (provisioning the account on first use).
/// </summary>
public sealed class LoginToken
{
    public string TokenHash { get; private set; }
    public string Email { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    // EF Core materialization.
    private LoginToken()
    {
        TokenHash = null!;
        Email = null!;
    }

    private LoginToken(string tokenHash, string email, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        TokenHash = tokenHash;
        Email = email;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static LoginToken Create(string tokenHash, string email, DateTimeOffset createdAtUtc, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new LoginToken(tokenHash, User.NormalizeEmail(email), createdAtUtc, createdAtUtc + lifetime);
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAtUtc is null && now < ExpiresAtUtc;

    public void Consume(DateTimeOffset now) => ConsumedAtUtc = now;
}
