namespace Platform.Domain.Players;

/// <summary>
/// A single project-scoped key-value entry for a player. The composite identity is
/// (<see cref="ProjectId"/>, <see cref="PlayerId"/>, <see cref="Key"/>).
/// </summary>
public sealed class PlayerDataEntry
{
    public string ProjectId { get; private set; }
    public string PlayerId { get; private set; }
    public string Key { get; private set; }
    public string Value { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    // EF Core materialization.
    private PlayerDataEntry()
    {
        ProjectId = null!;
        PlayerId = null!;
        Key = null!;
        Value = null!;
    }

    private PlayerDataEntry(string projectId, string playerId, string key, string value, DateTimeOffset updatedAtUtc)
    {
        ProjectId = projectId;
        PlayerId = playerId;
        Key = key;
        Value = value;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static PlayerDataEntry Create(
        string projectId, string playerId, string key, string value, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        return new PlayerDataEntry(projectId, playerId, key, value, now);
    }

    public void Update(string value, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        UpdatedAtUtc = now;
    }
}
