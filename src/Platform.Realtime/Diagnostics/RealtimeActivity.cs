using Platform.Application.Abstractions;
using Platform.Contracts.Admin;

namespace Platform.Realtime.Diagnostics;

/// <summary>
/// Central debugger recorder (Milestone 22): keeps a bounded, per-connection event timeline
/// plus connection metadata, and aggregates protocol errors by code. Read by the dashboard's
/// session inspector and error explorer. Thread-safe; timestamps come from the injected clock.
/// </summary>
public sealed class RealtimeActivity
{
    private const int SessionCapacity = 200;
    private const int TimelineCapacity = 60;
    private const int AffectedSessionsCapacity = 50;

    /// <summary>Actionable guidance per error code, shown in the error explorer.</summary>
    private static readonly IReadOnlyDictionary<string, string> SuggestedFixes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["malformed_message"] = "Send valid JSON envelopes: { \"type\", \"requestId\"?, \"payload\"? }.",
            ["unknown_message_type"] = "Check the message type spelling; see docs/protocol.md for the list.",
            ["invalid_payload"] = "A required field is missing or invalid — verify the payload for this message type.",
            ["room_not_found"] = "The room/lobby code is wrong or the room expired. Create or re-join it.",
            ["room_full"] = "The room/lobby hit its max players. Raise the cap or use another room.",
            ["payload_too_large"] = "Reduce the message size; the default limit is 16 KB.",
            ["rate_limited"] = "Slow down sends and back off before retrying (this error is retryable).",
            ["state_forbidden"] = "Only an entity's owner can update it, and only the room host can set room state.",
            ["entity_not_found"] = "Create the entity with state.entity.set before patching or deleting it.",
            ["state_limit_exceeded"] = "This room hit its entity cap — reuse or delete entities, or raise the plan limit.",
            ["state_too_large"] = "Trim the entity/room state; the default caps are 4 KB per entity and 16 KB per room.",
        };

    private sealed class Record
    {
        public required string ConnectionId { get; init; }
        public required string ProjectId { get; init; }
        public required string PlayerId { get; init; }
        public DateTimeOffset StartedAtUtc { get; init; }
        public DateTimeOffset? EndedAtUtc { get; set; }
        public string? IpAddress { get; set; }
        public string? SdkVersion { get; set; }
        public string? Engine { get; set; }
        public string AuthStatus { get; set; } = "anonymous";
        public string? DisconnectReason { get; set; }
        public DateTimeOffset LastActivityAtUtc { get; set; }
        public List<TimelineEntry> Timeline { get; } = [];
    }

    private sealed class ErrorState
    {
        public long Count { get; set; }
        public DateTimeOffset LastOccurrenceUtc { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public HashSet<string> AffectedSessions { get; } = new(StringComparer.Ordinal);
    }

    private readonly object _gate = new();
    private readonly LinkedList<Record> _order = new();
    private readonly Dictionary<string, Record> _byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ErrorState> _errors = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public RealtimeActivity(IClock clock) => _clock = clock;

    public void Started(
        string connectionId, string projectId, string playerId,
        string? ipAddress, string? sdkVersion, string? engine)
    {
        var now = _clock.UtcNow;
        var record = new Record
        {
            ConnectionId = connectionId,
            ProjectId = projectId,
            PlayerId = playerId,
            StartedAtUtc = now,
            LastActivityAtUtc = now,
            IpAddress = ipAddress,
            SdkVersion = string.IsNullOrWhiteSpace(sdkVersion) ? null : sdkVersion,
            Engine = string.IsNullOrWhiteSpace(engine) ? null : engine,
            AuthStatus = "authenticated",
        };

        lock (_gate)
        {
            _byConnection[connectionId] = record;
            _order.AddFirst(record);
            AddEntryNoLock(record, "connected", $"player {playerId}", now);
            while (_order.Count > SessionCapacity)
            {
                var oldest = _order.Last!.Value;
                _order.RemoveLast();
                _byConnection.Remove(oldest.ConnectionId);
            }
        }
    }

    public void Authenticated(string connectionId, string playerId)
    {
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (_byConnection.TryGetValue(connectionId, out var record))
            {
                record.AuthStatus = "authenticated";
                AddEntryNoLock(record, "authenticated", $"as {playerId}", now);
            }
        }
    }

    /// <summary>Records an action the client took (e.g. an inbound message type).</summary>
    public void Action(string connectionId, string kind, string detail)
    {
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (_byConnection.TryGetValue(connectionId, out var record))
            {
                AddEntryNoLock(record, kind, detail, now);
            }
        }
    }

    public void Ended(string connectionId, string? reason)
    {
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (_byConnection.TryGetValue(connectionId, out var record))
            {
                record.EndedAtUtc = now;
                record.DisconnectReason = reason;
                AddEntryNoLock(record, "disconnected", reason ?? "closed", now);
            }
        }
    }

    /// <summary>Records a rejected message: adds it to the session timeline and the error buckets.</summary>
    public void Error(string connectionId, string code, string message)
    {
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (_byConnection.TryGetValue(connectionId, out var record))
            {
                AddEntryNoLock(record, "rejected", $"{code}: {message}", now);
            }

            if (!_errors.TryGetValue(code, out var state))
            {
                state = new ErrorState();
                _errors[code] = state;
            }

            state.Count++;
            state.LastOccurrenceUtc = now;
            state.LastMessage = message;
            if (state.AffectedSessions.Count < AffectedSessionsCapacity)
            {
                state.AffectedSessions.Add(connectionId);
            }
        }
    }

    public SessionDetail? GetSession(string connectionId)
    {
        lock (_gate)
        {
            return _byConnection.TryGetValue(connectionId, out var record)
                ? ToDetail(record, currentRoomId: null)
                : null;
        }
    }

    public IReadOnlyList<ErrorBucket> GetErrors()
    {
        lock (_gate)
        {
            return _errors
                .Select(kv => new ErrorBucket(
                    kv.Key,
                    kv.Value.Count,
                    kv.Value.LastOccurrenceUtc,
                    kv.Value.LastMessage,
                    kv.Value.AffectedSessions.Count,
                    SuggestedFixes.GetValueOrDefault(kv.Key, "See docs/protocol.md for this error code.")))
                .OrderByDescending(e => e.Count)
                .ToArray();
        }
    }

    private static SessionDetail ToDetail(Record record, string? currentRoomId) => new(
        record.ConnectionId,
        record.ProjectId,
        record.PlayerId,
        record.StartedAtUtc,
        record.EndedAtUtc,
        record.IpAddress,
        record.SdkVersion,
        record.Engine,
        record.AuthStatus,
        record.DisconnectReason,
        currentRoomId,
        record.LastActivityAtUtc,
        record.Timeline.ToArray());

    private static void AddEntryNoLock(Record record, string kind, string detail, DateTimeOffset now)
    {
        record.LastActivityAtUtc = now;
        record.Timeline.Add(new TimelineEntry(now, kind, detail));
        if (record.Timeline.Count > TimelineCapacity)
        {
            record.Timeline.RemoveAt(0);
        }
    }
}
