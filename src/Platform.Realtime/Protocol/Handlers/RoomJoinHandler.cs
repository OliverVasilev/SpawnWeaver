using Platform.Contracts.Realtime;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class RoomJoinHandler : IRealtimeMessageHandler
{
    private readonly RoomService _rooms;

    public RoomJoinHandler(RoomService rooms) => _rooms = rooms;

    public string Type => RealtimeMessageTypes.RoomJoin;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<RoomJoinRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.RoomCode))
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A 'roomCode' is required.");
        }

        return _rooms.JoinRoomAsync(context, request.RoomCode.Trim(), request.PlayerName);
    }
}
