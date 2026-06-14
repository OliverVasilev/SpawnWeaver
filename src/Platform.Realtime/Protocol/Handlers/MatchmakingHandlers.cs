using Platform.Contracts.Realtime;
using Platform.Realtime.Matchmaking;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class MatchmakingJoinHandler : IRealtimeMessageHandler
{
    private readonly MatchmakingService _matchmaking;

    public MatchmakingJoinHandler(MatchmakingService matchmaking) => _matchmaking = matchmaking;

    public string Type => RealtimeMessageTypes.MatchmakingJoin;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<MatchmakingJoinRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.GameMode))
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A 'gameMode' is required.");
        }

        var matchSize = request.MatchSize ?? MatchmakingService.DefaultMatchSize;
        if (matchSize < 2)
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "'matchSize' must be at least 2.");
        }

        var region = string.IsNullOrWhiteSpace(request.Region)
            ? MatchmakingService.DefaultRegion
            : request.Region.Trim();

        return _matchmaking.JoinAsync(context, request.GameMode.Trim(), region, matchSize, request.PlayerName);
    }
}

internal sealed class MatchmakingLeaveHandler : IRealtimeMessageHandler
{
    private readonly MatchmakingService _matchmaking;

    public MatchmakingLeaveHandler(MatchmakingService matchmaking) => _matchmaking = matchmaking;

    public string Type => RealtimeMessageTypes.MatchmakingLeave;

    public Task HandleAsync(MessageContext context) => _matchmaking.LeaveAsync(context);
}
