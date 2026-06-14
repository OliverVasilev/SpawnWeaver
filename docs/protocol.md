# Realtime Protocol (v1)

The realtime gateway speaks a small JSON message protocol over a single WebSocket
connection (`GET /connect?projectKey=pk_…`). This document describes **protocol v1**
as implemented in Milestone 4.

> Transport: WebSocket text frames, UTF-8 JSON. A binary protocol may be added later
> only if benchmarks show JSON is a bottleneck (see the milestone plan).

## Envelope

Every message — in both directions — is a single JSON object with this shape:

```json
{
  "type": "message.type",
  "requestId": "optional-correlation-id",
  "payload": { }
}
```

| Field       | Type            | Required | Notes                                                            |
|-------------|-----------------|----------|------------------------------------------------------------------|
| `type`      | string          | yes      | Identifies the message. Unknown types are rejected (see Errors). |
| `requestId` | string \| null  | no       | If set on a request, the server echoes it on the response.       |
| `payload`   | object \| null  | no       | Type-specific body. Interpreted per `type`.                      |

Fields that are `null` are omitted from server-sent messages.

### Request/response correlation

When a client includes a `requestId`, the server's response to that message carries
the **same** `requestId`. This lets a client match a reply to the request that caused
it. Server-initiated messages (e.g. the welcome) have no `requestId`.

## Authentication

Connect with the project's **public** key:

```text
ws://host/connect?projectKey=pk_…
```

On connect the server establishes a **player identity**:

- If no `playerToken` is supplied, a new anonymous player is created and a `playerId` +
  `playerToken` are returned in the `connection.welcome` message.
- The client stores the `playerToken` and, to **reconnect as the same player**, supplies it:

  ```text
  ws://host/connect?projectKey=pk_…&playerToken=<token>
  ```

The token is a stateless, HMAC-signed value carrying the player id, project id, and an
expiry. A token that is invalid, tampered, expired, or for a different project causes the
connection to be **rejected** (HTTP 401). A fresh token is issued on every successful
connect (sliding expiration), so always store the most recent one.

`playerId` is the stable identity used in all room and game-event messages (it replaces
the connection id used in earlier milestones).

## Messages

### `connection.welcome` (server → client)

Sent once, immediately after a successful connection. Carries the player's stable identity
and the token to reuse on reconnect (see [Authentication](#authentication)).

```json
{
  "type": "connection.welcome",
  "payload": {
    "connectionId": "conn_…",
    "playerId": "player_…",
    "playerToken": "player_….proj_….1750000000.<sig>",
    "tokenExpiresAtUtc": "2026-06-10T09:45:31Z",
    "serverTimeUtc": "2026-06-03T09:45:31.89Z"
  }
}
```

### `ping` (client → server) / `pong` (server → client)

Application-level liveness check. The server replies with `pong`, echoing `requestId`.

```json
// client → server
{ "type": "ping", "requestId": "req_42" }
// server → client
{ "type": "pong", "requestId": "req_42" }
```

> Note: WebSocket-level keep-alive ping/pong frames are also used by the host for
> transport liveness; the `ping`/`pong` messages above are an additional
> application-level check.

### `error` (server → client)

Returned when a message cannot be processed.

```json
{
  "type": "error",
  "requestId": "req_7",
  "payload": { "code": "unknown_message_type", "message": "Unknown message type 'does.not.exist'." }
}
```

If the offending message could be parsed enough to recover a `requestId`, it is echoed;
malformed messages have no `requestId`.

#### Error codes

| Code                   | Meaning                                                        |
|------------------------|---------------------------------------------------------------|
| `malformed_message`    | The frame was not valid JSON, or was missing a `type`.        |
| `unknown_message_type` | The `type` has no registered handler.                         |
| `invalid_payload`      | The payload was missing or failed validation.                 |
| `room_not_found`       | No room matches the code/id (or it belongs to another project).|
| `payload_too_large`    | The message exceeded the maximum size (default 16 KB).        |
| `rate_limited`         | Messages were sent faster than the allowed rate.              |
| `room_full`            | The room/lobby is at its maximum player count.                |

## Limits

Inbound messages are subject to per-connection limits (configurable via the `Realtime`
config section):

- **Size** — messages larger than `MaxMessageBytes` (default 16 KB) are rejected with
  `payload_too_large`.
- **Rate** — a token bucket (`MaxMessagesPerSecond` sustained, `MessageBurst` burst)
  throttles spammy clients with `rate_limited`.

## Rooms (Milestone 5)

Rooms are in-memory, scoped to a project, and identified by a short code (e.g. `4V8772`).
A connection may belong to multiple rooms. Empty rooms expire after an inactivity TTL.

### `room.create` → `room.created`

```json
// client → server
{ "type": "room.create", "requestId": "c1", "payload": { "playerName": "Alice" } }
// server → creator
{ "type": "room.created", "requestId": "c1",
  "payload": { "roomId": "room_…", "roomCode": "4V8772", "playerId": "conn_…",
               "players": [ { "playerId": "conn_…", "playerName": "Alice" } ] } }
```

### `room.join` → `room.joined`

`room.joined` is sent both to the joiner (as the response, echoing `requestId`) and
broadcast to existing members (no `requestId`). `player` is who just joined; `players`
is the full roster.

```json
// client → server
{ "type": "room.join", "requestId": "j1", "payload": { "roomCode": "4V8772", "playerName": "Bob" } }
// server → all members
{ "type": "room.joined", "requestId": "j1",
  "payload": { "roomId": "room_…", "roomCode": "4V8772",
               "player": { "playerId": "conn_…", "playerName": "Bob" },
               "players": [ /* … */ ] } }
```

### `room.leave` → `room.left`

`room.left` is sent to the leaver (ack, echoing `requestId`) and broadcast to remaining
members. It is also broadcast when a member **disconnects**.

```json
{ "type": "room.leave", "requestId": "l1", "payload": { "roomId": "room_…" } }
{ "type": "room.left", "payload": { "roomId": "room_…", "playerId": "conn_…" } }
```

### `room.players`

```json
{ "type": "room.players", "requestId": "p1", "payload": { "roomId": "room_…" } }
{ "type": "room.players", "requestId": "p1",
  "payload": { "roomId": "room_…", "players": [ /* … */ ] } }
```

### `room.expired` (server → members)

Broadcast if a room is removed while it still has members (inactivity expiry). Empty
rooms are removed silently.

```json
{ "type": "room.expired", "payload": { "roomId": "room_…" } }
```

> `playerId` is the stable player identity established at connect (see Authentication),
> not the connection id. It survives reconnects when the `playerToken` is reused.

## Game events (Milestone 7)

### `game.event`

Client → server, relayed to the **other** members of the room (the sender is excluded).
`data` is opaque application JSON, relayed unchanged. The relayed message adds
`fromPlayerId`.

```json
// client → server
{ "type": "game.event", "payload": { "roomId": "room_…", "event": "player_moved", "data": { "x": 10, "y": 5 } } }
// server → other members
{ "type": "game.event",
  "payload": { "roomId": "room_…", "event": "player_moved", "data": { "x": 10, "y": 5 },
               "fromPlayerId": "conn_…" } }
```

The sender must be a member of the room, otherwise `room_not_found` is returned.

## Lobbies (Milestone 10)

A **lobby** is a room with developer-friendly attributes: a name, a visibility
(`public` = listed, `private` = join by code only), an optional `maxPlayers` cap, and
arbitrary string `metadata`. A lobby is still a room, so once joined, member join/leave
notifications reuse `room.left` and `game.event` works as usual.

### `lobby.create` → `lobby.created`

```json
// client → server
{ "type": "lobby.create", "requestId": "c1",
  "payload": { "name": "Arena", "visibility": "public", "maxPlayers": 4, "metadata": { "mode": "ffa" } } }
// server → creator
{ "type": "lobby.created", "requestId": "c1",
  "payload": { "lobbyId": "lobby_…", "code": "AB12CD", "name": "Arena", "visibility": "public",
               "maxPlayers": 4, "metadata": { "mode": "ffa" }, "playerId": "player_…", "players": [ … ] } }
```

`visibility` defaults to `public`; `maxPlayers` omitted/null = unlimited.

### `lobby.list`

Returns the **public** lobbies for the connection's project.

```json
{ "type": "lobby.list", "requestId": "l1" }
{ "type": "lobby.list", "requestId": "l1",
  "payload": { "lobbies": [ { "lobbyId": "lobby_…", "name": "Arena", "visibility": "public",
                              "playerCount": 1, "maxPlayers": 4, "metadata": { "mode": "ffa" } } ] } }
```

### `lobby.join` → `lobby.joined`

Provide `lobbyId` (public lobbies only — from the list) **or** `code` (works for public and
private). `lobby.joined` is sent to the joiner (response) and broadcast to existing members.

```json
{ "type": "lobby.join", "requestId": "j1", "payload": { "code": "AB12CD" } }
{ "type": "lobby.joined", "requestId": "j1",
  "payload": { "lobbyId": "lobby_…", "code": "AB12CD", "name": "Arena", "visibility": "public",
               "maxPlayers": 4, "metadata": { … }, "player": { "playerId": "player_…" }, "players": [ … ] } }
```

Join rules:
- A **private** lobby joined by `lobbyId` is rejected with `room_not_found` (use the code).
- A **full** lobby (`maxPlayers` reached) is rejected with `room_full`.

### `lobby.closed` (server → members)

Broadcast when a lobby is removed (currently on empty-room expiry).

```json
{ "type": "lobby.closed", "payload": { "lobbyId": "lobby_…" } }
```

## Matchmaking (Milestone 11)

Players queue for a match; when enough players are waiting with the **same project,
`gameMode`, `region`, and `matchSize`**, the server creates a room and notifies them.

### `matchmaking.join` → `matchmaking.queued` / `match.found`

```json
// client → server
{ "type": "matchmaking.join", "requestId": "m1",
  "payload": { "gameMode": "duel", "region": "global", "matchSize": 2 } }
// server → client (still waiting)
{ "type": "matchmaking.queued", "requestId": "m1",
  "payload": { "gameMode": "duel", "region": "global", "matchSize": 2 } }
// server → all matched players (when the bucket fills)
{ "type": "match.found",
  "payload": { "roomId": "match_…", "roomCode": "AB12CD", "gameMode": "duel",
               "region": "global", "players": [ … ] } }
```

`region` defaults to `global`; `matchSize` defaults to 2 (minimum 2). The matched players
are placed into the room identified by `roomId` — send `game.event` there.

### `matchmaking.leave` → `matchmaking.left`

```json
{ "type": "matchmaking.leave", "requestId": "m2" }
{ "type": "matchmaking.left", "requestId": "m2" }
```

### `matchmaking.timeout` (server → client)

Sent if no match is found within the configured timeout (`Realtime__MatchmakingTimeout`,
default 30s). Leaving the queue, disconnecting, or matching all cancel the timeout.

```json
{ "type": "matchmaking.timeout", "payload": { "gameMode": "duel", "region": "global" } }
```

## State sync (Milestone 23)

Room-scoped live state: a room-level key-value map, plus per-entity state owned by its
creator. `roomId` defaults to the caller's current room when omitted. Only the room **host**
may patch room state; only an entity's **owner** may patch/delete it. Updates are
rate-limited and size-capped (`State__*`).

```json
// Client → server
{ "type": "state.room.patch",   "payload": { "roomId": "room_…", "patch": { "phase": "combat" } } }
{ "type": "state.entity.set",   "payload": { "roomId": "room_…", "entityId": "p1", "state": { "x": 1, "y": 2 } } }
{ "type": "state.entity.patch", "payload": { "roomId": "room_…", "entityId": "p1", "patch": { "x": 5 } } }
{ "type": "state.entity.delete","payload": { "roomId": "room_…", "entityId": "p1" } }

// Server → members (broadcast)
{ "type": "state.room.changed",   "payload": { "roomId": "room_…", "patch": {…}, "state": {…} } }
{ "type": "state.entity.changed", "payload": { "roomId": "room_…", "entityId": "p1", "ownerId": "player_…", "patch": {…}, "state": {…} } }
{ "type": "state.entity.deleted", "payload": { "roomId": "room_…", "entityId": "p1" } }

// Server → joiner (late-join snapshot)
{ "type": "state.snapshot", "payload": { "roomId": "room_…", "roomState": {…}, "entities": [ { "entityId": "p1", "ownerId": "player_…", "state": {…} } ] } }

// Server → caller (rejection)
{ "type": "state.update.rejected", "payload": { "code": "state_forbidden", "message": "…", "target": "p1" } }
```

Rejection codes: `state_forbidden`, `entity_not_found`, `state_limit_exceeded`,
`state_too_large`, `rate_limited`, `room_not_found`.

## Versioning & stability

Protocol DTOs live in `Platform.Contracts` (`Platform.Contracts.Realtime`). A breaking
change to the wire shape must update the backend, the SDK, the tests, and this document
together. New message types are added by registering an `IRealtimeMessageHandler`.

## Not yet defined (later milestones)

Room, lobby, matchmaking, and game-event message types arrive in later milestones and
will reuse this envelope. Inbound application messages with unknown types are rejected
today rather than silently ignored.
