using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Platform.Realtime.Rooms;

/// <summary>Periodically removes rooms that have been empty past their TTL.</summary>
internal sealed partial class RoomExpiryService : BackgroundService
{
    private readonly RoomService _rooms;
    private readonly RealtimeOptions _options;
    private readonly ILogger<RoomExpiryService> _logger;

    public RoomExpiryService(RoomService rooms, IOptions<RealtimeOptions> options, ILogger<RoomExpiryService> logger)
    {
        _rooms = rooms;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ExpirySweepInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await _rooms.SweepExpiredAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    SweepFailed(_logger, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Error, Message = "Room expiry sweep failed")]
    private static partial void SweepFailed(ILogger logger, Exception exception);
}
