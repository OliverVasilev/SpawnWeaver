using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Matchmaking;

/// <summary>Periodically times out players who have waited too long in the queue.</summary>
internal sealed partial class MatchmakingTimeoutService : BackgroundService
{
    private readonly MatchmakingService _matchmaking;
    private readonly RealtimeOptions _options;
    private readonly ILogger<MatchmakingTimeoutService> _logger;

    public MatchmakingTimeoutService(
        MatchmakingService matchmaking, IOptions<RealtimeOptions> options, ILogger<MatchmakingTimeoutService> logger)
    {
        _matchmaking = matchmaking;
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
                    await _matchmaking.SweepTimeoutsAsync(stoppingToken).ConfigureAwait(false);
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

    [LoggerMessage(EventId = 1402, Level = LogLevel.Error, Message = "Matchmaking timeout sweep failed")]
    private static partial void SweepFailed(ILogger logger, Exception exception);
}
