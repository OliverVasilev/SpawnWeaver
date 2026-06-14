namespace Platform.Contracts.Realtime;

/// <summary>Well-known realtime message type identifiers (protocol v1).</summary>
public static class RealtimeMessageTypes
{
    /// <summary>Server → client, sent once on connect. Payload: <see cref="ConnectionWelcomePayload"/>.</summary>
    public const string ConnectionWelcome = "connection.welcome";

    /// <summary>Client → server liveness check. Server replies with <see cref="Pong"/>.</summary>
    public const string Ping = "ping";

    /// <summary>Server → client reply to <see cref="Ping"/>.</summary>
    public const string Pong = "pong";

    /// <summary>Server → client structured error. Payload: <see cref="RealtimeError"/>.</summary>
    public const string Error = "error";

    // --- Rooms (Milestone 5) ---

    /// <summary>Client → server: create a room. Payload: <see cref="RoomCreateRequest"/>.</summary>
    public const string RoomCreate = "room.create";

    /// <summary>Server → creator: room created. Payload: <see cref="RoomCreatedPayload"/>.</summary>
    public const string RoomCreated = "room.created";

    /// <summary>Client → server: join a room by code. Payload: <see cref="RoomJoinRequest"/>.</summary>
    public const string RoomJoin = "room.join";

    /// <summary>Server → members: a player joined. Payload: <see cref="RoomJoinedPayload"/>.</summary>
    public const string RoomJoined = "room.joined";

    /// <summary>Client → server: leave a room. Payload: <see cref="RoomLeaveRequest"/>.</summary>
    public const string RoomLeave = "room.leave";

    /// <summary>Server → members: a player left. Payload: <see cref="RoomLeftPayload"/>.</summary>
    public const string RoomLeft = "room.left";

    /// <summary>Client → server / server → client: room player list. Payload: <see cref="RoomPlayersPayload"/>.</summary>
    public const string RoomPlayers = "room.players";

    /// <summary>Server → members: the room expired. Payload: <see cref="RoomExpiredPayload"/>.</summary>
    public const string RoomExpired = "room.expired";

    // --- Game events (Milestone 7) ---

    /// <summary>
    /// Client → server: send a game event to a room (payload <see cref="GameEventRequest"/>).
    /// Server → other members: the relayed event (payload <see cref="GameEventPayload"/>).
    /// </summary>
    public const string GameEvent = "game.event";

    // --- Lobbies (Milestone 10) ---

    /// <summary>Client → server: create a lobby. Payload: <see cref="LobbyCreateRequest"/>.</summary>
    public const string LobbyCreate = "lobby.create";

    /// <summary>Server → creator: lobby created. Payload: <see cref="LobbyCreatedPayload"/>.</summary>
    public const string LobbyCreated = "lobby.created";

    /// <summary>Client → server: list public lobbies. Server → client list. Payload: <see cref="LobbyListPayload"/>.</summary>
    public const string LobbyList = "lobby.list";

    /// <summary>Client → server: join a lobby by id or code. Payload: <see cref="LobbyJoinRequest"/>.</summary>
    public const string LobbyJoin = "lobby.join";

    /// <summary>Server → members: a player joined the lobby. Payload: <see cref="LobbyJoinedPayload"/>.</summary>
    public const string LobbyJoined = "lobby.joined";

    /// <summary>Server → members: the lobby closed. Payload: <see cref="LobbyClosedPayload"/>.</summary>
    public const string LobbyClosed = "lobby.closed";

    // --- Matchmaking (Milestone 11) ---

    /// <summary>Client → server: enter the matchmaking queue. Payload: <see cref="MatchmakingJoinRequest"/>.</summary>
    public const string MatchmakingJoin = "matchmaking.join";

    /// <summary>Server → client: queued, waiting for a match. Payload: <see cref="MatchmakingQueuedPayload"/>.</summary>
    public const string MatchmakingQueued = "matchmaking.queued";

    /// <summary>Client → server: leave the matchmaking queue.</summary>
    public const string MatchmakingLeave = "matchmaking.leave";

    /// <summary>Server → client: confirmation of leaving the queue.</summary>
    public const string MatchmakingLeft = "matchmaking.left";

    /// <summary>Server → matched players: a match was found. Payload: <see cref="MatchFoundPayload"/>.</summary>
    public const string MatchFound = "match.found";

    /// <summary>Server → client: timed out without a match. Payload: <see cref="MatchmakingTimeoutPayload"/>.</summary>
    public const string MatchmakingTimeout = "matchmaking.timeout";

    // --- State sync (Milestone 23) ---

    /// <summary>Client → server: patch shared room state. Payload: <see cref="StateRoomPatchRequest"/>.</summary>
    public const string StateRoomPatch = "state.room.patch";

    /// <summary>Server → members: room state changed. Payload: <see cref="StateRoomChangedPayload"/>.</summary>
    public const string StateRoomChanged = "state.room.changed";

    /// <summary>Client → server: set (create/replace) an entity's state. Payload: <see cref="StateEntitySetRequest"/>.</summary>
    public const string StateEntitySet = "state.entity.set";

    /// <summary>Client → server: patch an entity's state. Payload: <see cref="StateEntityPatchRequest"/>.</summary>
    public const string StateEntityPatch = "state.entity.patch";

    /// <summary>Server → members: an entity's state changed. Payload: <see cref="StateEntityChangedPayload"/>.</summary>
    public const string StateEntityChanged = "state.entity.changed";

    /// <summary>Client → server: delete an entity. Payload: <see cref="StateEntityDeleteRequest"/>.</summary>
    public const string StateEntityDelete = "state.entity.delete";

    /// <summary>Server → members: an entity was deleted. Payload: <see cref="StateEntityDeletedPayload"/>.</summary>
    public const string StateEntityDeleted = "state.entity.deleted";

    /// <summary>Server → joiner: full current room + entity state. Payload: <see cref="StateSnapshotPayload"/>.</summary>
    public const string StateSnapshot = "state.snapshot";

    /// <summary>Server → caller: a state update was rejected. Payload: <see cref="StateRejectedPayload"/>.</summary>
    public const string StateUpdateRejected = "state.update.rejected";
}
