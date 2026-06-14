using Platform.Contracts.Realtime;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class RoomLeaveHandler : IRealtimeMessageHandler
{
    private readonly RoomService _rooms;

    public RoomLeaveHandler(RoomService rooms) => _rooms = rooms;

    public string Type => RealtimeMessageTypes.RoomLeave;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<RoomLeaveRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.RoomId))
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A 'roomId' is required.");
        }

        return _rooms.LeaveRoomAsync(context, request.RoomId);
    }
}
