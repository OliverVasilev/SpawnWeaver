namespace Platform.Realtime.State;

/// <summary>
/// Limits for Simple State Sync v1 (Milestone 23.7), bound from the <c>State</c> config section.
/// Defaults match the Free tier.
/// </summary>
public sealed class StateOptions
{
    /// <summary>Maximum number of entities a single room may hold.</summary>
    public int MaxEntitiesPerRoom { get; set; } = 50;

    /// <summary>Maximum serialized size, in bytes, of one entity's state.</summary>
    public int MaxEntityStateBytes { get; set; } = 4 * 1024;

    /// <summary>Maximum serialized size, in bytes, of the room-level state.</summary>
    public int MaxRoomStateBytes { get; set; } = 16 * 1024;

    /// <summary>Sustained state updates allowed per second, per client.</summary>
    public double MaxStateUpdatesPerSecondPerClient { get; set; } = 10;

    /// <summary>Burst capacity for the per-client state-update rate limiter.</summary>
    public double StateUpdateBurst { get; set; } = 20;
}
