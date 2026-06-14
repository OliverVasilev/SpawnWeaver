namespace Platform.Realtime.Rooms;

/// <summary>Tunables for the realtime gateway (bound from the <c>Realtime</c> config section).</summary>
public sealed class RealtimeOptions
{
    /// <summary>How long a room may stay empty before it is expired and removed.</summary>
    public TimeSpan EmptyRoomTtl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>How often the background sweeper checks for expired rooms.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum size, in bytes, of a single inbound message. Larger messages are rejected.</summary>
    public int MaxMessageBytes { get; set; } = 16 * 1024;

    /// <summary>Sustained inbound message rate allowed per connection (messages/second).</summary>
    public double MaxMessagesPerSecond { get; set; } = 20;

    /// <summary>Burst capacity for the per-connection rate limiter (token bucket size).</summary>
    public double MessageBurst { get; set; } = 40;

    /// <summary>How long a player may wait in the matchmaking queue before timing out.</summary>
    public TimeSpan MatchmakingTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum concurrent connections per project (0 = unlimited).</summary>
    public int MaxConnectionsPerProject { get; set; }

    /// <summary>Maximum number of metadata entries on a lobby.</summary>
    public int MaxLobbyMetadataEntries { get; set; } = 16;

    /// <summary>Maximum length of a lobby metadata key or value.</summary>
    public int MaxMetadataValueLength { get; set; } = 256;
}
