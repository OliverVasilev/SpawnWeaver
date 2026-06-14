using Microsoft.EntityFrameworkCore;
using Platform.Application.Feedback;
using Platform.Domain.Feedback;
using Platform.Infrastructure.Database;

namespace Platform.Infrastructure.Repositories;

internal sealed class FeedbackRepository : IFeedbackRepository
{
    private readonly PlatformDbContext _db;

    public FeedbackRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(FeedbackEntry entry, CancellationToken ct = default)
        => await _db.Feedback.AddAsync(entry, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<FeedbackEntry>> ListAsync(int limit, CancellationToken ct = default)
    {
        // SQLite can't ORDER BY DateTimeOffset in SQL, so order on the client.
        var all = await _db.Feedback.ToListAsync(ct).ConfigureAwait(false);
        return all.OrderByDescending(f => f.CreatedAtUtc).Take(limit).ToArray();
    }
}
