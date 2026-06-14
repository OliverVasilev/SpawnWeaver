using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Platform.Realtime.Protocol;

namespace Platform.Benchmarks;

/// <summary>
/// Serialization work to broadcast one game event to a room of N members:
/// serialize-per-member (old) vs serialize-once (new).
/// </summary>
[ShortRunJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class BroadcastBenchmarks
{
    [Params(2, 8, 32)]
    public int MemberCount;

    private readonly object _payload = new
    {
        roomId = "room_1",
        @event = "player_moved",
        data = new { x = 10, y = 5 },
        fromPlayerId = "player_abc123",
    };

    [Benchmark(Baseline = true)]
    public long SerializePerMember()
    {
        long bytes = 0;
        for (var i = 0; i < MemberCount; i++)
        {
            bytes += RealtimeMessageSender.Serialize("game.event", null, _payload).Length;
        }

        return bytes;
    }

    [Benchmark]
    public long SerializeOnce()
    {
        var serialized = RealtimeMessageSender.Serialize("game.event", null, _payload);
        long bytes = 0;
        for (var i = 0; i < MemberCount; i++)
        {
            // The same bytes are sent to every recipient (socket I/O excluded).
            bytes += serialized.Length;
        }

        return bytes;
    }
}
