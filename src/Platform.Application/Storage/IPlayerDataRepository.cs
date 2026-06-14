using Platform.Domain.Players;

namespace Platform.Application.Storage;

/// <summary>Persistence boundary for project-scoped player key-value data.</summary>
public interface IPlayerDataRepository
{
    Task<PlayerDataEntry?> GetAsync(string projectId, string playerId, string key, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListKeysAsync(string projectId, string playerId, CancellationToken ct = default);

    Task<int> CountKeysAsync(string projectId, string playerId, CancellationToken ct = default);

    Task AddAsync(PlayerDataEntry entry, CancellationToken ct = default);

    Task<bool> DeleteAsync(string projectId, string playerId, string key, CancellationToken ct = default);
}
