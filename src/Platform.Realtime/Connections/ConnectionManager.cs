using System.Collections.Concurrent;

namespace Platform.Realtime.Connections;

/// <summary>
/// Tracks all live realtime connections in memory (single-node MVP). Thread-safe.
/// </summary>
public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, RealtimeConnection> _connections =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _perProject = new(StringComparer.Ordinal);

    public int Count => _connections.Count;

    public void Add(RealtimeConnection connection)
    {
        _connections[connection.Id] = connection;
        _perProject.AddOrUpdate(connection.ProjectId, 1, static (_, count) => count + 1);
    }

    public void Remove(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            _perProject.AddOrUpdate(connection.ProjectId, 0, static (_, count) => Math.Max(0, count - 1));
        }
    }

    public bool TryGet(string connectionId, out RealtimeConnection? connection)
        => _connections.TryGetValue(connectionId, out connection);

    /// <summary>Number of live connections belonging to a project.</summary>
    public int CountForProject(string projectId) => _perProject.GetValueOrDefault(projectId);

    /// <summary>Snapshot of all live connections (diagnostics).</summary>
    public IReadOnlyList<RealtimeConnection> Snapshot() => _connections.Values.ToArray();
}
