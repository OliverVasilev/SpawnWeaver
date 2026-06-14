using Microsoft.EntityFrameworkCore;
using Platform.Application.Projects;
using Platform.Domain.Projects;
using Platform.Infrastructure.Database;

namespace Platform.Infrastructure.Repositories;

internal sealed class ProjectRepository : IProjectRepository
{
    private readonly PlatformDbContext _db;

    public ProjectRepository(PlatformDbContext db) => _db = db;

    public async Task AddAsync(Project project, CancellationToken ct = default)
        => await _db.Projects.AddAsync(project, ct);

    public Task<Project?> GetByIdAsync(string id, CancellationToken ct = default)
        => _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default)
        => _db.Projects.FirstOrDefaultAsync(p => p.PublicKey == publicKey, ct);

    public async Task<IReadOnlyList<Project>> ListAsync(int limit, CancellationToken ct = default)
    {
        // SQLite can't ORDER BY DateTimeOffset in SQL, so order on the client (few projects).
        var projects = await _db.Projects.ToListAsync(ct).ConfigureAwait(false);
        return projects
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(limit)
            .ToArray();
    }

    public async Task<IReadOnlyList<Project>> ListByOrganizationAsync(string organizationId, CancellationToken ct = default)
    {
        var projects = await _db.Projects
            .Where(p => p.OrganizationId == organizationId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return projects
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToArray();
    }
}
