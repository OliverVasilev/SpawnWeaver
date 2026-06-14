using Platform.Contracts.Realtime;
using Platform.Realtime.State;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class StateRoomPatchHandler : IRealtimeMessageHandler
{
    private readonly StateService _state;
    public StateRoomPatchHandler(StateService state) => _state = state;
    public string Type => RealtimeMessageTypes.StateRoomPatch;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<StateRoomPatchRequest>();
        return request is null
            ? context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A state.room.patch payload is required.")
            : _state.PatchRoomStateAsync(context, request);
    }
}

internal sealed class StateEntitySetHandler : IRealtimeMessageHandler
{
    private readonly StateService _state;
    public StateEntitySetHandler(StateService state) => _state = state;
    public string Type => RealtimeMessageTypes.StateEntitySet;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<StateEntitySetRequest>();
        return request is null
            ? context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A state.entity.set payload is required.")
            : _state.SetEntityStateAsync(context, request);
    }
}

internal sealed class StateEntityPatchHandler : IRealtimeMessageHandler
{
    private readonly StateService _state;
    public StateEntityPatchHandler(StateService state) => _state = state;
    public string Type => RealtimeMessageTypes.StateEntityPatch;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<StateEntityPatchRequest>();
        return request is null
            ? context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A state.entity.patch payload is required.")
            : _state.PatchEntityStateAsync(context, request);
    }
}

internal sealed class StateEntityDeleteHandler : IRealtimeMessageHandler
{
    private readonly StateService _state;
    public StateEntityDeleteHandler(StateService state) => _state = state;
    public string Type => RealtimeMessageTypes.StateEntityDelete;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<StateEntityDeleteRequest>();
        return request is null
            ? context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A state.entity.delete payload is required.")
            : _state.DeleteEntityStateAsync(context, request);
    }
}
