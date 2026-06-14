using Platform.Contracts.Realtime;
using Platform.Realtime.Lobbies;

namespace Platform.Realtime.Protocol.Handlers;

internal sealed class LobbyCreateHandler : IRealtimeMessageHandler
{
    private readonly LobbyService _lobbies;

    public LobbyCreateHandler(LobbyService lobbies) => _lobbies = lobbies;

    public string Type => RealtimeMessageTypes.LobbyCreate;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<LobbyCreateRequest>()
            ?? new LobbyCreateRequest(Name: null, Visibility: null, MaxPlayers: null, Metadata: null, PlayerName: null);

        if (request.MaxPlayers is int max && max < 1)
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "'maxPlayers' must be at least 1.");
        }

        return _lobbies.CreateLobbyAsync(context, request);
    }
}

internal sealed class LobbyListHandler : IRealtimeMessageHandler
{
    private readonly LobbyService _lobbies;

    public LobbyListHandler(LobbyService lobbies) => _lobbies = lobbies;

    public string Type => RealtimeMessageTypes.LobbyList;

    public Task HandleAsync(MessageContext context) => _lobbies.ListLobbiesAsync(context);
}

internal sealed class LobbyJoinHandler : IRealtimeMessageHandler
{
    private readonly LobbyService _lobbies;

    public LobbyJoinHandler(LobbyService lobbies) => _lobbies = lobbies;

    public string Type => RealtimeMessageTypes.LobbyJoin;

    public Task HandleAsync(MessageContext context)
    {
        var request = context.PayloadAs<LobbyJoinRequest>();
        if (request is null ||
            (string.IsNullOrWhiteSpace(request.LobbyId) && string.IsNullOrWhiteSpace(request.Code)))
        {
            return context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A 'lobbyId' or 'code' is required.");
        }

        return _lobbies.JoinLobbyAsync(context, request);
    }
}
