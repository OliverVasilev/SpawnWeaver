using Platform.Contracts.Realtime;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class GameEventHandler : IRealtimeMessageHandler
{
    private readonly RoomService _rooms;

    public GameEventHandler(RoomService rooms) => _rooms = rooms;

    public string Type => RealtimeMessageTypes.GameEvent;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<GameEventRequest>();
        if (request is null ||
            string.IsNullOrWhiteSpace(request.RoomId) ||
            string.IsNullOrWhiteSpace(request.Event))
        {
            return context.RespondErrorAsync(
                ProtocolErrorCodes.InvalidPayload, "A 'roomId' and 'event' are required.");
        }

        return _rooms.SendGameEventAsync(context, request.RoomId, request.Event, request.Data);
    }
}
