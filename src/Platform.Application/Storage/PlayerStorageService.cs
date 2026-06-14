using System.Text;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Domain.Players;

namespace Platform.Application.Storage;

public enum StorageSaveStatus
{
    Saved,
    InvalidKey,
    ValueTooLarge,
    QuotaExceeded,
}

public sealed record StorageSaveResult(StorageSaveStatus Status, PlayerDataEntry? Entry);

/// <summary>Application use cases for project-scoped player key-value storage (with quotas).</summary>
public sealed class PlayerStorageService
{
    private readonly IPlayerDataRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly StorageOptions _options;

    public PlayerStorageService(
        IPlayerDataRepository repository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IOptions<StorageOptions> options)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<StorageSaveResult> SaveAsync(
        string projectId, string playerId, string key, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > _options.MaxKeyLength)
        {
            return new StorageSaveResult(StorageSaveStatus.InvalidKey, null);
        }

        if (Encoding.UTF8.GetByteCount(value) > _options.MaxValueBytes)
        {
            return new StorageSaveResult(StorageSaveStatus.ValueTooLarge, null);
        }

        var existing = await _repository.GetAsync(projectId, playerId, key, ct).ConfigureAwait(false);
        if (existing is null)
        {
            var count = await _repository.CountKeysAsync(projectId, playerId, ct).ConfigureAwait(false);
            if (count >= _options.MaxKeysPerPlayer)
            {
                return new StorageSaveResult(StorageSaveStatus.QuotaExceeded, null);
            }

            var entry = PlayerDataEntry.Create(projectId, playerId, key, value, _clock.UtcNow);
            await _repository.AddAsync(entry, ct).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            return new StorageSaveResult(StorageSaveStatus.Saved, entry);
        }

        existing.Update(value, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return new StorageSaveResult(StorageSaveStatus.Saved, existing);
    }

    public Task<PlayerDataEntry?> GetAsync(string projectId, string playerId, string key, CancellationToken ct = default)
        => _repository.GetAsync(projectId, playerId, key, ct);

    public Task<IReadOnlyList<string>> ListKeysAsync(string projectId, string playerId, CancellationToken ct = default)
        => _repository.ListKeysAsync(projectId, playerId, ct);

    public async Task<bool> DeleteAsync(string projectId, string playerId, string key, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteAsync(projectId, playerId, key, ct).ConfigureAwait(false);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return deleted;
    }
}
