using Platform.Contracts.Realtime;

namespace Platform.Realtime.Protocol.Handlers;

/// <summary>Replies to a <c>ping</c> with a <c>pong</c>, echoing the request id.</summary>
internal sealed class PingMessageHandler : IRealtimeMessageHandler
{
    public string Type => RealtimeMessageTypes.Ping;

    public Task HandleAsync(MessageContext context) => context.RespondAsync(RealtimeMessageTypes.Pong);
}
