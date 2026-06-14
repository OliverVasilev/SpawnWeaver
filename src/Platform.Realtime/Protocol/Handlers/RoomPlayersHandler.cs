using Platform.Contracts.Realtime;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class RoomPlayersHandler : IRealtimeMessageHandler
{
    private readonly RoomService _rooms;

    public RoomPlayersHandler(RoomService rooms) => _rooms = rooms;

    public string Type => RealtimeMessageTypes.RoomPlayers;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<RoomPlayersRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.RoomId))
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A 'roomId' is required.");
        }

        return _rooms.ListPlayersAsync(context, request.RoomId);
    }
}
