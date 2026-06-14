using System.Diagnostics.Metrics;
using Platform.Contracts.Admin;
using Platform.Realtime.Connections;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Diagnostics;

/// <summary>
/// Realtime metrics published via a <see cref="Meter"/> (consumed by OpenTelemetry) and also
/// kept as in-process totals for the <c>/api/admin/metrics</c> snapshot and the dashboard.
/// </summary>
public sealed class RealtimeMetrics : IDisposable
{
    public const string MeterName = "SpawnWeaver";

    private readonly Meter _meter;
    private readonly Counter<long> _connectionsOpened;
    private readonly Counter<long> _connectionsClosed;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _errors;
    private readonly ConnectionManager _connections;
    private readonly RoomManager _rooms;

    private long _openedTotal;
    private long _closedTotal;
    private long _messagesTotal;
    private long _errorsTotal;

    internal RealtimeMetrics(ConnectionManager connections, RoomManager rooms)
    {
        _connections = connections;
        _rooms = rooms;
        _meter = new Meter(MeterName);

        _connectionsOpened = _meter.CreateCounter<long>("spawnweaver.connections.opened");
        _connectionsClosed = _meter.CreateCounter<long>("spawnweaver.connections.closed");
        _messagesReceived = _meter.CreateCounter<long>("spawnweaver.messages.received");
        _errors = _meter.CreateCounter<long>("spawnweaver.errors");

        _meter.CreateObservableGauge("spawnweaver.connections.active", () => _connections.Count);
        _meter.CreateObservableGauge("spawnweaver.rooms.active", () => _rooms.RoomCount);
    }

    public void ConnectionOpened()
    {
        _connectionsOpened.Add(1);
        Interlocked.Increment(ref _openedTotal);
    }

    public void ConnectionClosed()
    {
        _connectionsClosed.Add(1);
        Interlocked.Increment(ref _closedTotal);
    }

    public void MessageReceived()
    {
        _messagesReceived.Add(1);
        Interlocked.Increment(ref _messagesTotal);
    }

    public void ErrorOccurred()
    {
        _errors.Add(1);
        Interlocked.Increment(ref _errorsTotal);
    }

    public MetricsSnapshot GetSnapshot() => new(
        _connections.Count,
        _rooms.RoomCount,
        Interlocked.Read(ref _openedTotal),
        Interlocked.Read(ref _closedTotal),
        Interlocked.Read(ref _messagesTotal),
        Interlocked.Read(ref _errorsTotal));

    public void Dispose() => _meter.Dispose();
}
