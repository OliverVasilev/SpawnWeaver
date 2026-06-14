using Platform.Application.Abstractions;
using Platform.Infrastructure.Database;

namespace Platform.Infrastructure.Repositories;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly PlatformDbContext _db;

    public UnitOfWork(PlatformDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
