namespace Platform.Realtime.Matchmaking;

/// <summary>
/// Pure, thread-safe matchmaking queue (no I/O). Tickets are bucketed by
/// project+gameMode+region+matchSize; a bucket forms a match once it reaches its size.
/// FIFO within a bucket. A connection has at most one ticket.
/// </summary>
internal sealed class MatchQueue
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<MatchTicket>> _buckets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MatchTicket> _byConnection = new(StringComparer.Ordinal);

    /// <summary>
    /// Enqueues a ticket (replacing any existing ticket for the same connection). Returns the
    /// matched group if the bucket reached its size, otherwise null (still waiting).
    /// </summary>
    public IReadOnlyList<MatchTicket>? Enqueue(MatchTicket ticket)
    {
        lock (_gate)
        {
            RemoveNoLock(ticket.Connection.Id);

            if (!_buckets.TryGetValue(ticket.BucketKey, out var bucket))
            {
                bucket = [];
                _buckets[ticket.BucketKey] = bucket;
            }

            bucket.Add(ticket);
            _byConnection[ticket.Connection.Id] = ticket;

            if (bucket.Count < ticket.MatchSize)
            {
                return null;
            }

            var group = bucket.GetRange(0, ticket.MatchSize);
            bucket.RemoveRange(0, ticket.MatchSize);
            foreach (var matched in group)
            {
                _byConnection.Remove(matched.Connection.Id);
            }

            if (bucket.Count == 0)
            {
                _buckets.Remove(ticket.BucketKey);
            }

            return group;
        }
    }

    public bool Remove(string connectionId)
    {
        lock (_gate)
        {
            return RemoveNoLock(connectionId);
        }
    }

    /// <summary>Removes and returns tickets that have waited at least <paramref name="timeout"/>.</summary>
    public IReadOnlyList<MatchTicket> SweepTimeouts(DateTimeOffset now, TimeSpan timeout)
    {
        lock (_gate)
        {
            var expired = _byConnection.Values
                .Where(t => now - t.EnqueuedAtUtc >= timeout)
                .ToList();

            foreach (var ticket in expired)
            {
                RemoveNoLock(ticket.Connection.Id);
            }

            return expired;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byConnection.Count;
            }
        }
    }

    /// <summary>Per-bucket waiting counts for diagnostics. Bucket key is project|mode|region|size.</summary>
    public IReadOnlyList<(string BucketKey, int Waiting)> Snapshot()
    {
        lock (_gate)
        {
            return _buckets
                .Where(kv => kv.Value.Count > 0)
                .Select(kv => (kv.Key, kv.Value.Count))
                .ToArray();
        }
    }

    private bool RemoveNoLock(string connectionId)
    {
        if (!_byConnection.Remove(connectionId, out var ticket))
        {
            return false;
        }

        if (_buckets.TryGetValue(ticket.BucketKey, out var bucket))
        {
            bucket.Remove(ticket);
            if (bucket.Count == 0)
            {
                _buckets.Remove(ticket.BucketKey);
            }
        }

        return true;
    }
}
