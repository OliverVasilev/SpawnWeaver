using Platform.Realtime.Connections;

namespace Platform.Realtime.Matchmaking;

/// <summary>A player waiting in the matchmaking queue.</summary>
internal sealed record MatchTicket(
    RealtimeConnection Connection,
    string? PlayerName,
    string GameMode,
    string Region,
    int MatchSize,
    string BucketKey,
    DateTimeOffset EnqueuedAtUtc);
