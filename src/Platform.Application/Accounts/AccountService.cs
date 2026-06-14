using Platform.Application.Abstractions;
using Platform.Application.Security;
using Platform.Domain.Accounts;

namespace Platform.Application.Accounts;

/// <summary>Outcome codes for account operations.</summary>
public enum SignUpStatus
{
    Success,
    InvalidEmail,
    WeakPassword,
    EmailAlreadyInUse,
}

/// <summary>Result of a sign-up attempt. <see cref="User"/>/<see cref="Organization"/> are set on success.</summary>
public sealed record SignUpResult(SignUpStatus Status, User? User = null, Organization? Organization = null)
{
    public bool Succeeded => Status == SignUpStatus.Success;
}

/// <summary>
/// Account use cases: registration (which also provisions a personal organization),
/// authentication, and profile/password updates (Milestone 19.1 / 19.2).
/// </summary>
public sealed class AccountService
{
    /// <summary>Minimum password length enforced at registration and password change.</summary>
    public const int MinPasswordLength = 8;

    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwords;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public AccountService(
        IUserRepository users,
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwords,
        IIdGenerator ids,
        IClock clock)
    {
        _users = users;
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _passwords = passwords;
        _ids = ids;
        _clock = clock;
    }

    /// <summary>
    /// Registers a new account and a personal organization. Email is normalized and must
    /// be unique; the password must meet the minimum length policy.
    /// </summary>
    public async Task<SignUpResult> SignUpAsync(
        string email, string? displayName, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > User.MaxEmailLength)
        {
            return new SignUpResult(SignUpStatus.InvalidEmail);
        }

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return new SignUpResult(SignUpStatus.WeakPassword);
        }

        var normalizedEmail = User.NormalizeEmail(email);
        if (await _users.EmailExistsAsync(normalizedEmail, ct))
        {
            return new SignUpResult(SignUpStatus.EmailAlreadyInUse);
        }

        var now = _clock.UtcNow;
        var userId = _ids.NewId("user");
        var passwordHash = _passwords.Hash(password);
        var user = User.Create(userId, normalizedEmail, displayName ?? string.Empty, passwordHash, now);

        var orgName = $"{user.DisplayName}'s workspace";
        var organization = Organization.Create(_ids.NewId("org"), orgName, userId, now);

        await _users.AddAsync(user, ct);
        await _organizations.AddAsync(organization, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new SignUpResult(SignUpStatus.Success, user, organization);
    }

    /// <summary>
    /// Verifies an email/password pair. On success records the login time. Returns
    /// <c>null</c> on any failure (unknown email or wrong password) — callers must not
    /// distinguish the two, to avoid account enumeration.
    /// </summary>
    public async Task<User?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = await _users.GetByEmailAsync(User.NormalizeEmail(email), ct);
        if (user is null || !_passwords.Verify(password, user.PasswordHash))
        {
            return null;
        }

        user.RecordLogin(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return user;
    }

    public Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
        => _users.GetByIdAsync(id, ct);

    /// <summary>Looks up a user by email (normalizes first). Null if none.</summary>
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(email)
            ? Task.FromResult<User?>(null)
            : _users.GetByEmailAsync(User.NormalizeEmail(email), ct);

    /// <summary>Marks a user's email as verified. No-op if the user no longer exists.</summary>
    public async Task MarkEmailVerifiedAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return;
        }

        user.MarkEmailVerified(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Returns the user for an email, creating a passwordless account (+ personal organization)
    /// on first use. Used by magic-link / SSO sign-in where there is no password.
    /// </summary>
    public async Task<User> GetOrCreateByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        var existing = await _users.GetByEmailAsync(normalizedEmail, ct);
        if (existing is not null)
        {
            existing.RecordLogin(_clock.UtcNow);
            // Receiving the emailed link proves ownership, so this also confirms the address.
            existing.MarkEmailVerified(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return existing;
        }

        var now = _clock.UtcNow;
        var userId = _ids.NewId("user");
        // A random, never-revealed password hash — password login stays disabled until they set one.
        var passwordHash = _passwords.Hash($"{Guid.NewGuid():N}{Guid.NewGuid():N}");
        var user = User.Create(userId, normalizedEmail, string.Empty, passwordHash, now);
        user.RecordLogin(now);
        // Provisioned via an emailed link, so the address is verified by construction.
        user.MarkEmailVerified(now);

        var organization = Organization.Create(_ids.NewId("org"), $"{user.DisplayName}'s workspace", userId, now);

        await _users.AddAsync(user, ct);
        await _organizations.AddAsync(organization, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> UpdateDisplayNameAsync(string userId, string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return false;
        }

        user.UpdateDisplayName(displayName, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Changes a password after verifying the current one. Returns false if either check fails.</summary>
    public async Task<bool> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength)
        {
            return false;
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !_passwords.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.SetPasswordHash(_passwords.Hash(newPassword), _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
