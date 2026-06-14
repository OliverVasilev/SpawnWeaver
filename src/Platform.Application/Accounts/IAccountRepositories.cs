using Platform.Domain.Accounts;

namespace Platform.Application.Accounts;

/// <summary>Persistence boundary for <see cref="User"/> accounts.</summary>
public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct = default);

    Task<User?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Looks up a user by normalized (lowercased) email.</summary>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
}

/// <summary>Persistence boundary for <see cref="Organization"/> workspaces.</summary>
public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken ct = default);

    Task<Organization?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Organizations owned by a user, most-recent first.</summary>
    Task<IReadOnlyList<Organization>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default);
}

/// <summary>Persistence boundary for <see cref="UserSession"/> dashboard sessions.</summary>
public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken ct = default);

    Task<UserSession?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Active (non-expired) sessions for a user, most-recent first.</summary>
    Task<IReadOnlyList<UserSession>> ListByUserAsync(string userId, CancellationToken ct = default);

    void Remove(UserSession session);

    /// <summary>Removes every session for a user (sign-out-everywhere). Returns the count removed.</summary>
    Task<int> RemoveAllForUserAsync(string userId, CancellationToken ct = default);
}

/// <summary>Persistence boundary for single-use magic-link <see cref="LoginToken"/>s.</summary>
public interface ILoginTokenRepository
{
    Task AddAsync(LoginToken token, CancellationToken ct = default);

    Task<LoginToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Most recent token created for an email (for request throttling).</summary>
    Task<LoginToken?> GetLatestForEmailAsync(string normalizedEmail, CancellationToken ct = default);
}

/// <summary>Persistence boundary for single-use <see cref="EmailVerificationToken"/>s.</summary>
public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token, CancellationToken ct = default);

    Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Most recent token created for a user (for resend throttling).</summary>
    Task<EmailVerificationToken?> GetLatestForUserAsync(string userId, CancellationToken ct = default);
}
