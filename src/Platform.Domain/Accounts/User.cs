namespace Platform.Domain.Accounts;

/// <summary>
/// A developer account for dashboard access. Stores only the <em>hash</em> of the
/// password — the plaintext is never persisted.
/// </summary>
public sealed class User
{
    public const int MaxEmailLength = 256;
    public const int MaxDisplayNameLength = 80;

    public string Id { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    /// <summary>When the email was verified, or null if it hasn't been. See <see cref="IsEmailVerified"/>.</summary>
    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    /// <summary>True once the account's email address has been confirmed.</summary>
    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    // EF Core materialization.
    private User()
    {
        Id = null!;
        Email = null!;
        DisplayName = null!;
        PasswordHash = null!;
    }

    private User(
        string id,
        string email,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Creates a new account. The caller generates the id and the password <em>hash</em>;
    /// the email is normalized (trimmed + lowercased) for case-insensitive uniqueness.
    /// </summary>
    public static User Create(
        string id,
        string email,
        string displayName,
        string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length > MaxEmailLength || !normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("A valid email address is required.", nameof(email));
        }

        var name = string.IsNullOrWhiteSpace(displayName)
            ? normalizedEmail.Split('@')[0]
            : displayName.Trim();
        if (name.Length > MaxDisplayNameLength)
        {
            name = name[..MaxDisplayNameLength];
        }

        return new User(id, normalizedEmail, name, passwordHash, createdAtUtc);
    }

    /// <summary>Normalizes an email for storage/lookup: trimmed and lowercased.</summary>
    public static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    public void UpdateDisplayName(string displayName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var name = displayName.Trim();
        DisplayName = name.Length > MaxDisplayNameLength ? name[..MaxDisplayNameLength] : name;
        UpdatedAtUtc = now;
    }

    public void SetPasswordHash(string passwordHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        UpdatedAtUtc = now;
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginAtUtc = now;

    /// <summary>Marks the email address as verified. Idempotent — keeps the first verification time.</summary>
    public void MarkEmailVerified(DateTimeOffset now)
    {
        EmailVerifiedAtUtc ??= now;
        UpdatedAtUtc = now;
    }
}
