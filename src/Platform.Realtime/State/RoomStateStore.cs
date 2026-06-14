using System.Text.Json;

namespace Platform.Realtime.State;

/// <summary>Outcome of a state mutation.</summary>
internal enum StateResult
{
    Ok,
    Forbidden,
    EntityNotFound,
    LimitExceeded,
    TooLarge,
}

/// <summary>One entity's state (snapshot copy, safe to broadcast).</summary>
internal readonly record struct EntityStateData(
    string EntityId, string OwnerId, IReadOnlyDictionary<string, JsonElement> State, DateTimeOffset UpdatedAtUtc);

/// <summary>The full state of a room (room-level state + all entities).</summary>
internal readonly record struct RoomStateSnapshot(
    IReadOnlyDictionary<string, JsonElement> RoomState, IReadOnlyList<EntityStateData> Entities);

/// <summary>The result of a state mutation, with the resulting full state on success.</summary>
internal readonly record struct StateMutationResult(StateResult Result, IReadOnlyDictionary<string, JsonElement> FullState)
{
    public bool Ok => Result == StateResult.Ok;
}

/// <summary>
/// Holds one room's live state (Milestone 23): a room-level key-value map plus owned entities.
/// All mutations are serialized by its own lock and enforce ownership-independent limits
/// (entity count and serialized size); ownership is checked here too. Pure in-memory.
/// </summary>
internal sealed class RoomStateStore
{
    private sealed class EntityRecord
    {
        public required string OwnerId { get; set; }
        public required Dictionary<string, JsonElement> State { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private static readonly IReadOnlyDictionary<string, JsonElement> Empty = new Dictionary<string, JsonElement>();

    private readonly object _gate = new();
    private readonly Dictionary<string, JsonElement> _room = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntityRecord> _entities = new(StringComparer.Ordinal);

    public StateMutationResult PatchRoom(IReadOnlyDictionary<string, JsonElement> patch, int maxBytes)
    {
        lock (_gate)
        {
            var merged = new Dictionary<string, JsonElement>(_room, StringComparer.Ordinal);
            foreach (var (key, value) in patch)
            {
                merged[key] = value;
            }

            if (Size(merged) > maxBytes)
            {
                return new StateMutationResult(StateResult.TooLarge, Empty);
            }

            _room.Clear();
            foreach (var (key, value) in merged)
            {
                _room[key] = value;
            }

            return new StateMutationResult(StateResult.Ok, merged);
        }
    }

    public StateMutationResult SetEntity(
        string entityId, string ownerId, IReadOnlyDictionary<string, JsonElement> state,
        int maxEntities, int maxBytes, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_entities.TryGetValue(entityId, out var existing) &&
                !string.Equals(existing.OwnerId, ownerId, StringComparison.Ordinal))
            {
                return new StateMutationResult(StateResult.Forbidden, Empty);
            }

            if (existing is null && _entities.Count >= maxEntities)
            {
                return new StateMutationResult(StateResult.LimitExceeded, Empty);
            }

            var copy = new Dictionary<string, JsonElement>(state, StringComparer.Ordinal);
            if (Size(copy) > maxBytes)
            {
                return new StateMutationResult(StateResult.TooLarge, Empty);
            }

            _entities[entityId] = new EntityRecord { OwnerId = ownerId, State = copy, UpdatedAtUtc = now };
            return new StateMutationResult(StateResult.Ok, copy);
        }
    }

    public StateMutationResult PatchEntity(
        string entityId, string ownerId, IReadOnlyDictionary<string, JsonElement> patch, int maxBytes, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                return new StateMutationResult(StateResult.EntityNotFound, Empty);
            }

            if (!string.Equals(entity.OwnerId, ownerId, StringComparison.Ordinal))
            {
                return new StateMutationResult(StateResult.Forbidden, Empty);
            }

            var merged = new Dictionary<string, JsonElement>(entity.State, StringComparer.Ordinal);
            foreach (var (key, value) in patch)
            {
                merged[key] = value;
            }

            if (Size(merged) > maxBytes)
            {
                return new StateMutationResult(StateResult.TooLarge, Empty);
            }

            entity.State = merged;
            entity.UpdatedAtUtc = now;
            return new StateMutationResult(StateResult.Ok, merged);
        }
    }

    public StateResult DeleteEntity(string entityId, string ownerId)
    {
        lock (_gate)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                return StateResult.EntityNotFound;
            }

            if (!string.Equals(entity.OwnerId, ownerId, StringComparison.Ordinal))
            {
                return StateResult.Forbidden;
            }

            _entities.Remove(entityId);
            return StateResult.Ok;
        }
    }

    public RoomStateSnapshot Snapshot()
    {
        lock (_gate)
        {
            var room = new Dictionary<string, JsonElement>(_room, StringComparer.Ordinal);
            var entities = _entities
                .Select(kv => new EntityStateData(
                    kv.Key, kv.Value.OwnerId,
                    new Dictionary<string, JsonElement>(kv.Value.State, StringComparer.Ordinal),
                    kv.Value.UpdatedAtUtc))
                .ToArray();
            return new RoomStateSnapshot(room, entities);
        }
    }

    public int EntityCount
    {
        get
        {
            lock (_gate)
            {
                return _entities.Count;
            }
        }
    }

    private static int Size(Dictionary<string, JsonElement> map)
        => JsonSerializer.SerializeToUtf8Bytes(map).Length;
}
