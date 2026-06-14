using Platform.Realtime.Matchmaking;
using Platform.Tests.TestDoubles;
using Xunit;

namespace Platform.Tests.Unit;

public sealed class MatchQueueTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static MatchTicket Ticket(
        string gameMode, int size, DateTimeOffset at, string region = "global", string project = "proj_a")
    {
        var connection = TestConnections.Create(project);
        var bucket = $"{project}|{gameMode}|{region}|{size}";
        return new MatchTicket(connection, null, gameMode, region, size, bucket, at);
    }

    [Fact]
    public void Enqueue_below_match_size_keeps_waiting()
    {
        var queue = new MatchQueue();

        var result = queue.Enqueue(Ticket("duel", 2, T0));

        Assert.Null(result);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void Enqueue_reaching_match_size_returns_group_in_fifo_order()
    {
        var queue = new MatchQueue();
        var first = Ticket("duel", 2, T0);
        var second = Ticket("duel", 2, T0);

        Assert.Null(queue.Enqueue(first));
        var group = queue.Enqueue(second);

        Assert.NotNull(group);
        Assert.Equal(2, group!.Count);
        Assert.Same(first, group[0]);
        Assert.Same(second, group[1]);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Different_buckets_do_not_match()
    {
        var queue = new MatchQueue();

        Assert.Null(queue.Enqueue(Ticket("duel", 2, T0)));
        Assert.Null(queue.Enqueue(Ticket("coop", 2, T0)));      // different game mode
        Assert.Null(queue.Enqueue(Ticket("duel", 2, T0, "eu"))); // different region
        Assert.Equal(3, queue.Count);
    }

    [Fact]
    public void Remove_takes_a_player_out_of_the_queue()
    {
        var queue = new MatchQueue();
        var first = Ticket("duel", 2, T0);
        queue.Enqueue(first);

        Assert.True(queue.Remove(first.Connection.Id));

        // A new arrival should NOT match because the first player left.
        Assert.Null(queue.Enqueue(Ticket("duel", 2, T0)));
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void SweepTimeouts_returns_and_removes_aged_tickets()
    {
        var queue = new MatchQueue();
        queue.Enqueue(Ticket("duel", 2, T0));

        var beforeTimeout = queue.SweepTimeouts(T0.AddSeconds(10), TimeSpan.FromSeconds(30));
        Assert.Empty(beforeTimeout);
        Assert.Equal(1, queue.Count);

        var expired = queue.SweepTimeouts(T0.AddSeconds(31), TimeSpan.FromSeconds(30));
        Assert.Single(expired);
        Assert.Equal(0, queue.Count);
    }
}
