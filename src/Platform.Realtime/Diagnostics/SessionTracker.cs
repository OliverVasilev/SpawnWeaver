using Platform.Contracts.Admin;
using Platform.Realtime.Connections;

namespace Platform.Realtime.Diagnostics;

/// <summary>
/// Keeps a bounded history of recent connection sessions (start and end) for the dashboard.
/// </summary>
internal sealed class SessionTracker
{
    private sealed record Session(string ConnectionId, string ProjectId, string PlayerId, DateTimeOffset StartedAtUtc)
    {
        public DateTimeOffset? EndedAtUtc { get; set; }
    }

    private readonly object _gate = new();
    private readonly LinkedList<Session> _recent = new();
    private readonly Dictionary<string, Session> _active = new(StringComparer.Ordinal);
    private readonly int _capacity;

    public SessionTracker(int capacity = 200) => _capacity = capacity;

    public void Start(RealtimeConnection connection, DateTimeOffset now)
    {
        var session = new Session(connection.Id, connection.ProjectId, connection.PlayerId, now);
        lock (_gate)
        {
            _active[connection.Id] = session;
            _recent.AddFirst(session);
            while (_recent.Count > _capacity)
            {
                _recent.RemoveLast();
            }
        }
    }

    public void End(string connectionId, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_active.Remove(connectionId, out var session))
            {
                session.EndedAtUtc = now;
            }
        }
    }

    public IReadOnlyList<SessionSummary> Recent()
    {
        lock (_gate)
        {
            return _recent
                .Select(s => new SessionSummary(s.ConnectionId, s.ProjectId, s.PlayerId, s.StartedAtUtc, s.EndedAtUtc))
                .ToArray();
        }
    }
}
