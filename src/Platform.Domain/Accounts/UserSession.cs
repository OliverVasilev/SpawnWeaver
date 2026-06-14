namespace Platform.Domain.Accounts;

/// <summary>
/// A server-side dashboard session. The id is a high-entropy opaque handle stored in
/// the auth cookie's claims; keeping sessions in the database makes them revocable
/// (sign-out, sign-out-everywhere) and auditable (Milestone 19.1 session management).
/// </summary>
public sealed class UserSession
{
    public string Id { get; private set; }
    public string UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    // EF Core materialization.
    private UserSession()
    {
        Id = null!;
        UserId = null!;
    }

    private UserSession(string id, string userId, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        LastSeenAtUtc = createdAtUtc;
    }

    public static UserSession Create(string id, string userId, DateTimeOffset createdAtUtc, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Session lifetime must be positive.");
        }

        return new UserSession(id, userId, createdAtUtc, createdAtUtc + lifetime);
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAtUtc;

    public void Touch(DateTimeOffset now) => LastSeenAtUtc = now;
}
