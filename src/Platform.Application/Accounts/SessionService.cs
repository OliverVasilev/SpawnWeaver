using Platform.Application.Abstractions;
using Platform.Domain.Accounts;

namespace Platform.Application.Accounts;

/// <summary>
/// Creates and validates server-side dashboard sessions. The session id is the opaque
/// handle carried in the auth cookie; keeping sessions in the database makes them
/// revocable and lets us show "active sessions" in account settings.
/// </summary>
public sealed class SessionService
{
    /// <summary>How long a session stays valid before re-authentication is required.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(14);

    private readonly IUserSessionRepository _sessions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public SessionService(
        IUserSessionRepository sessions,
        IUnitOfWork unitOfWork,
        IIdGenerator ids,
        IClock clock)
    {
        _sessions = sessions;
        _unitOfWork = unitOfWork;
        _ids = ids;
        _clock = clock;
    }

    public async Task<UserSession> CreateAsync(string userId, CancellationToken ct = default)
    {
        var session = UserSession.Create(_ids.NewId("sess"), userId, _clock.UtcNow, DefaultLifetime);
        await _sessions.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    /// <summary>
    /// Returns the session if it exists and has not expired; expired sessions are
    /// removed as a side effect. Touches <c>LastSeenAtUtc</c> on valid sessions.
    /// </summary>
    public async Task<UserSession?> ValidateAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var session = await _sessions.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            return null;
        }

        if (session.IsExpired(_clock.UtcNow))
        {
            _sessions.Remove(session);
            await _unitOfWork.SaveChangesAsync(ct);
            return null;
        }

        session.Touch(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    public async Task RevokeAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct);
        if (session is not null)
        {
            _sessions.Remove(session);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<int> RevokeAllAsync(string userId, CancellationToken ct = default)
    {
        var removed = await _sessions.RemoveAllForUserAsync(userId, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return removed;
    }

    public Task<IReadOnlyList<UserSession>> ListForUserAsync(string userId, CancellationToken ct = default)
        => _sessions.ListByUserAsync(userId, ct);
}
