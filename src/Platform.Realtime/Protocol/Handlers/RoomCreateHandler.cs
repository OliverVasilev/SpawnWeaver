using Platform.Contracts.Realtime;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class RoomCreateHandler : IRealtimeMessageHandler
{
    private readonly RoomService _rooms;

    public RoomCreateHandler(RoomService rooms) => _rooms = rooms;

    public string Type => RealtimeMessageTypes.RoomCreate;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<RoomCreateRequest>();
        return _rooms.CreateRoomAsync(context, request?.PlayerName);
    }
}
