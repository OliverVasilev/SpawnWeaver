using Microsoft.EntityFrameworkCore;
using Platform.Application.Accounts;
using Platform.Domain.Accounts;
using Platform.Infrastructure.Database;

namespace Platform.Infrastructure.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly PlatformDbContext _db;

    public UserRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    public Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
}

internal sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly PlatformDbContext _db;

    public OrganizationRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
        => await _db.Organizations.AddAsync(organization, ct);

    public Task<Organization?> GetByIdAsync(string id, CancellationToken ct = default)
        => _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Organization>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var orgs = await _db.Organizations
            .Where(o => o.OwnerUserId == ownerUserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return orgs
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToArray();
    }
}

internal sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly PlatformDbContext _db;

    public UserSessionRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(UserSession session, CancellationToken ct = default)
        => await _db.UserSessions.AddAsync(session, ct);

    public Task<UserSession?> GetByIdAsync(string id, CancellationToken ct = default)
        => _db.UserSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<UserSession>> ListByUserAsync(string userId, CancellationToken ct = default)
    {
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return sessions
            .OrderByDescending(s => s.LastSeenAtUtc)
            .ToArray();
    }

    public void Remove(UserSession session) => _db.UserSessions.Remove(session);

    public async Task<int> RemoveAllForUserAsync(string userId, CancellationToken ct = default)
    {
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        _db.UserSessions.RemoveRange(sessions);
        return sessions.Count;
    }
}

internal sealed class LoginTokenRepository : ILoginTokenRepository
{
    private readonly PlatformDbContext _db;

    public LoginTokenRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(LoginToken token, CancellationToken ct = default)
        => await _db.LoginTokens.AddAsync(token, ct);

    public Task<LoginToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.LoginTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<LoginToken?> GetLatestForEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        // SQLite can't ORDER BY DateTimeOffset in SQL, so order on the client.
        var tokens = await _db.LoginTokens
            .Where(t => t.Email == normalizedEmail)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return tokens.OrderByDescending(t => t.CreatedAtUtc).FirstOrDefault();
    }
}

internal sealed class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly PlatformDbContext _db;

    public EmailVerificationTokenRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(EmailVerificationToken token, CancellationToken ct = default)
        => await _db.EmailVerificationTokens.AddAsync(token, ct);

    public Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.EmailVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<EmailVerificationToken?> GetLatestForUserAsync(string userId, CancellationToken ct = default)
    {
        // SQLite can't ORDER BY DateTimeOffset in SQL, so order on the client.
        var tokens = await _db.EmailVerificationTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return tokens.OrderByDescending(t => t.CreatedAtUtc).FirstOrDefault();
    }
}
