using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Contracts.Realtime;
using Platform.Realtime.Protocol;
using Platform.Realtime.Rooms;

namespace Platform.Realtime.Lobbies;

/// <summary>
/// Developer-facing lobby surface on top of <see cref="RoomManager"/>: create, list, and
/// join lobbies with visibility, a max-player cap, and metadata. A lobby is a room, so once
/// joined, player join/leave notifications reuse <c>room.left</c> and game events work.
/// </summary>
internal sealed class LobbyService
{
    private readonly RoomManager _rooms;
    private readonly RealtimeOptions _options;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(RoomManager rooms, IOptions<RealtimeOptions> options, ILogger<LobbyService> logger)
    {
        _rooms = rooms;
        _options = options.Value;
        _logger = logger;
    }

    public async Task CreateLobbyAsync(MessageContext context, LobbyCreateRequest request)
    {
        var metadata = request.Metadata ?? new Dictionary<string, string>();
        if (!IsMetadataValid(metadata, out var reason))
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, reason).ConfigureAwait(false);
            return;
        }

        var visibility = ParseVisibility(request.Visibility);
        var attributes = new LobbyAttributes(request.Name?.Trim(), visibility, request.MaxPlayers, metadata);

        var connection = context.Connection;
        var lobby = _rooms.CreateLobby(connection.ProjectId, connection, request.PlayerName, attributes);
        LobbyLog.Created(_logger, lobby.Id, VisibilityText(lobby.Visibility), connection.ProjectId);

        var payload = new LobbyCreatedPayload(
            lobby.Id, lobby.Code, lobby.Name, VisibilityText(lobby.Visibility), lobby.MaxPlayers,
            lobby.Metadata, connection.PlayerId, RoomBroadcast.ToPlayers(lobby.MembersSnapshot()));

        await context.RespondAsync(RealtimeMessageTypes.LobbyCreated, payload).ConfigureAwait(false);
    }

    public async Task ListLobbiesAsync(MessageContext context)
    {
        var lobbies = _rooms.ListPublicLobbies(context.Connection.ProjectId)
            .Select(l => new LobbySummary(
                l.Id, l.Name, VisibilityText(l.Visibility), l.MemberCount, l.MaxPlayers, l.Metadata))
            .ToArray();

        await context.RespondAsync(RealtimeMessageTypes.LobbyList, new LobbyListPayload(lobbies)).ConfigureAwait(false);
    }

    public async Task JoinLobbyAsync(MessageContext context, LobbyJoinRequest request)
    {
        var connection = context.Connection;

        RoomJoinOutcome outcome;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            outcome = _rooms.Join(connection.ProjectId, request.Code.Trim(), connection, request.PlayerName);
        }
        else if (!string.IsNullOrWhiteSpace(request.LobbyId))
        {
            // Joining by id is only allowed for public lobbies; private lobbies need the code.
            var lobby = _rooms.Find(request.LobbyId);
            outcome = lobby is { IsLobby: true, Visibility: LobbyVisibility.Public }
                ? _rooms.JoinById(connection.ProjectId, request.LobbyId, connection, request.PlayerName)
                : RoomJoinOutcome.NotFound;
        }
        else
        {
            await context.RespondErrorAsync(ProtocolErrorCodes.InvalidPayload, "A 'lobbyId' or 'code' is required.")
                .ConfigureAwait(false);
            return;
        }

        switch (outcome.Status)
        {
            case RoomJoinStatus.NotFound:
                await context.RespondErrorAsync(ProtocolErrorCodes.RoomNotFound, "Lobby was not found.")
                    .ConfigureAwait(false);
                return;
            case RoomJoinStatus.Full:
                await context.RespondErrorAsync(ProtocolErrorCodes.RoomFull, "Lobby is full.")
                    .ConfigureAwait(false);
                return;
        }

        var lobbyRoom = outcome.Room!;
        var roster = RoomBroadcast.ToPlayers(outcome.Members);
        LobbyLog.Joined(_logger, connection.PlayerId, lobbyRoom.Id, roster.Length);

        var payload = new LobbyJoinedPayload(
            lobbyRoom.Id, lobbyRoom.Code, lobbyRoom.Name, VisibilityText(lobbyRoom.Visibility),
            lobbyRoom.MaxPlayers, lobbyRoom.Metadata, new RoomPlayer(connection.PlayerId, request.PlayerName), roster);

        await context.RespondAsync(RealtimeMessageTypes.LobbyJoined, payload).ConfigureAwait(false);
        await State.StateService.SendSnapshotAsync(connection, lobbyRoom, context.CancellationToken).ConfigureAwait(false);
        await RoomBroadcast.SendToMembersAsync(
            outcome.Members, excludePlayerId: connection.PlayerId,
            RealtimeMessageTypes.LobbyJoined, payload, context.CancellationToken).ConfigureAwait(false);
    }

    private bool IsMetadataValid(IReadOnlyDictionary<string, string> metadata, out string reason)
    {
        if (metadata.Count > _options.MaxLobbyMetadataEntries)
        {
            reason = $"Too many metadata entries (max {_options.MaxLobbyMetadataEntries}).";
            return false;
        }

        foreach (var (key, value) in metadata)
        {
            if (key.Length > _options.MaxMetadataValueLength || (value?.Length ?? 0) > _options.MaxMetadataValueLength)
            {
                reason = $"Metadata key/value exceeds {_options.MaxMetadataValueLength} characters.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static LobbyVisibility ParseVisibility(string? value)
        => string.Equals(value, LobbyVisibilities.Private, StringComparison.OrdinalIgnoreCase)
            ? LobbyVisibility.Private
            : LobbyVisibility.Public;

    private static string VisibilityText(LobbyVisibility visibility)
        => visibility == LobbyVisibility.Private ? LobbyVisibilities.Private : LobbyVisibilities.Public;
}
