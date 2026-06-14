using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Platform.Realtime.Protocol;

namespace Platform.Benchmarks;

/// <summary>Inbound parse and outbound serialize cost for a typical game-event message.</summary>
[ShortRunJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class EnvelopeBenchmarks
{
    private static readonly byte[] GameEventJson = Encoding.UTF8.GetBytes(
        """{"type":"game.event","payload":{"roomId":"room_1","event":"player_moved","data":{"x":10,"y":5}}}""");

    private readonly object _payload = new
    {
        roomId = "room_1",
        @event = "player_moved",
        data = new { x = 10, y = 5 },
        fromPlayerId = "player_abc123",
    };

    [Benchmark]
    public bool ParseEnvelope() => EnvelopeReader.TryParse(GameEventJson, out _, out _);

    [Benchmark]
    public int SerializeOutbound() => RealtimeMessageSender.Serialize("game.event", null, _payload).Length;
}
