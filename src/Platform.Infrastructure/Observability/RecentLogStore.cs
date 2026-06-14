using Platform.Contracts.Admin;

namespace Platform.Infrastructure.Observability;

/// <summary>Bounded in-memory ring buffer of recent log records for the dashboard.</summary>
public sealed class RecentLogStore
{
    private readonly object _gate = new();
    private readonly LinkedList<LogRecord> _records = new();
    private readonly int _capacity;

    public RecentLogStore(int capacity = 500) => _capacity = capacity;

    public void Add(LogRecord record)
    {
        lock (_gate)
        {
            _records.AddFirst(record);
            while (_records.Count > _capacity)
            {
                _records.RemoveLast();
            }
        }
    }

    public IReadOnlyList<LogRecord> GetRecent(string? level = null, int count = 200)
    {
        lock (_gate)
        {
            IEnumerable<LogRecord> query = _records;
            if (!string.IsNullOrWhiteSpace(level))
            {
                query = query.Where(r => string.Equals(r.Level, level, StringComparison.OrdinalIgnoreCase));
            }

            return query.Take(count).ToArray();
        }
    }
}
