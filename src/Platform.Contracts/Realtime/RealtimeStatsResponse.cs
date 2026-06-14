namespace Platform.Contracts.Realtime;

/// <summary>Diagnostics snapshot of the realtime gateway (early observability).</summary>
public sealed record RealtimeStatsResponse(int ActiveConnections, int ActiveRooms);
