using Microsoft.EntityFrameworkCore;
using Platform.Application.Storage;
using Platform.Domain.Players;
using Platform.Infrastructure.Database;

namespace Platform.Infrastructure.Repositories;

internal sealed class PlayerDataRepository : IPlayerDataRepository
{
    private readonly PlatformDbContext _db;

    public PlayerDataRepository(PlatformDbContext db) => _db = db;

    public Task<PlayerDataEntry?> GetAsync(string projectId, string playerId, string key, CancellationToken ct = default)
        => _db.PlayerData.FirstOrDefaultAsync(
            e => e.ProjectId == projectId && e.PlayerId == playerId && e.Key == key, ct);

    public async Task<IReadOnlyList<string>> ListKeysAsync(string projectId, string playerId, CancellationToken ct = default)
        => await _db.PlayerData
            .Where(e => e.ProjectId == projectId && e.PlayerId == playerId)
            .Select(e => e.Key)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public Task<int> CountKeysAsync(string projectId, string playerId, CancellationToken ct = default)
        => _db.PlayerData.CountAsync(e => e.ProjectId == projectId && e.PlayerId == playerId, ct);

    public async Task AddAsync(PlayerDataEntry entry, CancellationToken ct = default)
        => await _db.PlayerData.AddAsync(entry, ct).ConfigureAwait(false);

    public async Task<bool> DeleteAsync(string projectId, string playerId, string key, CancellationToken ct = default)
    {
        var entry = await GetAsync(projectId, playerId, key, ct).ConfigureAwait(false);
        if (entry is null)
        {
            return false;
        }

        _db.PlayerData.Remove(entry);
        return true;
    }
}
