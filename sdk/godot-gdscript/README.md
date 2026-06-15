# SpawnWeaver — Godot GDScript SDK

A small Godot 4 client for the SpawnWeaver multiplayer backend. It wraps a WebSocket
connection, speaks the [realtime protocol](../../docs/protocol.md), and surfaces
connection/room/event activity as Godot **signals**.

> Status: v0.3.0 (Milestones 20–24). Connecting, guest auth, rooms, lobbies, matchmaking,
> game-event relay, and **state sync** are implemented. The SDK auto-reconnects, detects
> heartbeat timeouts, exposes a **structured error model** (`sdk_error`), a **debug mode**, a
> copyable **debug report**, and an **editor plugin** (configure, test, and generate scenes
> inside Godot). New here? Browse the full docs in your dashboard at **`/dashboard/docs`**.

## Install

### One-line install (recommended)

From the **root of your Godot project**, run the installer hosted by your SpawnWeaver server.
It downloads just the addon and extracts it into `res://addons/multiplayer_service` — no repo
clone needed:

```powershell
# Windows / PowerShell
iwr https://spawnweaver.dev/install.ps1 -UseBasicParsing | iex
```

```bash
# macOS / Linux (needs curl + unzip)
curl -fsSL https://spawnweaver.dev/install.sh | bash
```

Replace `spawnweaver.dev` with your server's address (e.g. `http://localhost:5159` when
running locally). Then enable **SpawnWeaver Multiplayer Service** in
*Project → Project Settings → Plugins*.

### Manual install

Copy `addons/multiplayer_service/` into your Godot project's `addons/` folder, then enable
**SpawnWeaver Multiplayer Service** in *Project → Project Settings → Plugins*. Enabling the
plugin registers an autoload singleton named `MultiplayerService`.

(If you prefer, add the autoload manually: *Project Settings → Autoload* →
`res://addons/multiplayer_service/multiplayer_service.gd`, name `MultiplayerService`.)

## Quick start

```gdscript
func _ready() -> void:
    MultiplayerService.connected.connect(func(): MultiplayerService.create_room("Alice"))
    MultiplayerService.room_created.connect(func(id, code, players): print("Room code: ", code))
    MultiplayerService.player_joined.connect(func(id, player): print("Joined: ", player))

    MultiplayerService.configure("pk_your_public_key")        # public key from POST /api/projects
    MultiplayerService.connect_to_server("ws://127.0.0.1:5000/connect")
```

## API

| Method | Description |
|---|---|
| `configure(project_key)` | Set the public project key (sent as `projectKey`). |
| `connect_to_server(url)` | Open the WebSocket (e.g. `ws://127.0.0.1:5000/connect`). |
| `connect_using_config()` | Connect using the credentials saved by the editor dock. |
| `load_config()` | Read the saved credentials `{ public_key, server_url, debug }`. |
| `disconnect_from_server()` | Close the connection. |
| `is_connected_to_server()` | `true` once the handshake completes. |
| `describe_error(code)` | Friendly message for an `error_received` code. |
| `create_room(player_name="")` | Create a room; replies via `room_created`. |
| `join_room(room_code, player_name="")` | Join by code; replies via `room_joined`. |
| `leave_room(room_id)` | Leave a room. |
| `list_players(room_id)` | Request the player list; replies via `room_players`. |
| `create_lobby(name, visibility, max_players, metadata, player_name)` | Create a lobby; replies via `lobby_created`. |
| `list_lobbies()` | List public lobbies; replies via `lobby_list`. |
| `join_lobby(code, player_name="")` | Join a lobby by code; replies via `lobby_joined`. |
| `join_lobby_by_id(lobby_id, player_name="")` | Join a public lobby by id; replies via `lobby_joined`. |
| `join_matchmaking(game_mode, region="", match_size=0, player_name="")` | Enter the matchmaking queue. |
| `leave_matchmaking()` | Leave the matchmaking queue. |
| `send_event(event, data={}, room_id="")` | Send a game event; relayed to other room members. |
| `ping(request_id="")` | Liveness check. |
| `set_debug_enabled(enabled)` | Turn verbose SDK logging on/off. |
| `is_debug_enabled()` | Whether debug logging is on. |
| `get_ping_ms()` | Most recent round-trip latency in ms (`-1` if unknown). |
| `create_debug_report()` | Diagnostic snapshot `Dictionary` (see *Debug report*). |
| `create_debug_report_string()` | The debug report as pretty JSON, ready to copy. |
| `patch_room_state(patch, room_id="")` | Patch shared room state (host only); broadcasts `room_state_changed`. |
| `set_entity_state(entity_id, state, room_id="")` | Create/replace an entity you own; broadcasts `entity_state_changed`. |
| `patch_entity_state(entity_id, patch, room_id="")` | Merge a patch into an entity you own. |
| `delete_entity_state(entity_id, room_id="")` | Delete an entity you own; broadcasts `entity_state_deleted`. |

## Signals

| Signal | Arguments |
|---|---|
| `connected` | — |
| `disconnected` | — |
| `connection_error` | `reason: String` |
| `welcomed` | `connection_id: String` |
| `authenticated` | `player_id, player_token` (store the token to reconnect) |
| `reconnecting` | `attempt: int` (auto-reconnect in progress) |
| `reconnected` | — (a reconnect attempt succeeded; `connected` also fires) |
| `reconnect_failed` | — (gave up after `max_reconnect_attempts`) |
| `room_created` | `room_id, room_code, players: Array` |
| `room_joined` | `room_id, room_code, player: Dictionary, players: Array` |
| `player_joined` | `room_id, player: Dictionary` |
| `player_left` | `room_id, player_id` |
| `room_players` | `room_id, players: Array` |
| `room_expired` | `room_id` |
| `lobby_created` | `lobby: Dictionary` |
| `lobby_list` | `lobbies: Array` |
| `lobby_joined` | `lobby_id, player: Dictionary, players: Array` |
| `lobby_closed` | `lobby_id` |
| `matchmaking_queued` | `game_mode, region, match_size` |
| `match_found` | `room_id, room_code, players: Array` |
| `matchmaking_left` | — |
| `matchmaking_timeout` | `game_mode, region` |
| `event_received` | `event, data: Dictionary, from_player_id` |
| `state_snapshot_received` | `snapshot: Dictionary` — full room + entity state on join |
| `room_state_changed` | `patch: Dictionary, state: Dictionary` |
| `entity_state_changed` | `entity_id, patch: Dictionary, full_state: Dictionary` |
| `entity_state_deleted` | `entity_id` |
| `state_update_rejected` | `error: Dictionary` — `{ code, message, target, retryable }` |
| `error_received` | `code, message` (use `describe_error(code)` for a message) |
| `sdk_error` | `error: Dictionary` — `{ code, message, details, retryable }` |

## Properties

| Property | Description |
|---|---|
| `player_id` | Stable player identity (set on connect). |
| `player_token` | Reconnect credential; stored and reused automatically. |
| `current_room_id` | The room/lobby/match you're in, or `""`. Maintained automatically. |
| `current_room_kind` | `"room"`, `"lobby"`, `"match"`, or `""`. Maintained automatically. |
| `auto_reconnect` | `true` by default — reconnect after an unexpected drop. |
| `max_reconnect_attempts` | Reconnect attempts before `reconnect_failed` (default 5). |
| `heartbeat_interval` | Seconds between heartbeat pings (also feeds latency stats; default 15). |
| `heartbeat_timeout` | Reconnect if no server message for this many seconds (default 45; `0` disables). |
| `rejoin_last_room_on_reconnect` | If `true`, re-join the last room/lobby (by code) after reconnect. |
| `last_disconnect_reason` | Why the last disconnect happened (debugging). |

## Error model

Server errors arrive on **`sdk_error(error)`** as a structured dictionary, and also on the
back-compat `error_received(code, message)`:

```gdscript
MultiplayerService.sdk_error.connect(func(error):
    # { "code": "room_not_found", "message": "...", "details": {}, "retryable": false }
    if error.retryable:
        await get_tree().create_timer(1.0).timeout
        retry()
    else:
        push_warning(error.message))
```

| Code | Retryable | Meaning |
|---|---|---|
| `malformed_message` | no | The message wasn't valid. |
| `unknown_message_type` | no | The server didn't recognize the message type. |
| `invalid_payload` | no | A required field was missing or invalid. |
| `room_not_found` | no | That room/lobby doesn't exist — check the code. |
| `room_full` | no | That room/lobby is full. |
| `payload_too_large` | no | The message was too large. |
| `rate_limited` | **yes** | You're sending too quickly; back off and retry. |

## Debug mode & report

Turn on verbose logging to trace the connection, auth, room/lobby/matchmaking actions,
messages, rejections, and disconnect reasons:

```gdscript
MultiplayerService.set_debug_enabled(true)
# [SpawnWeaver][connection] connected
# [SpawnWeaver][auth] authenticated as player_ab12…
# [SpawnWeaver][recv] room.joined
```

`create_debug_report()` returns a snapshot for support — SDK + Godot versions, connection
and player state, the last errors, the **last 50 protocol messages**, and ping/latency
stats. `create_debug_report_string()` gives copyable JSON you can paste into the dashboard:

```gdscript
DisplayServer.clipboard_set(MultiplayerService.create_debug_report_string())
```

## Reconnect

After an unexpected disconnect the SDK reconnects automatically with exponential backoff,
reusing `player_token` to resume the same `player_id`. You'll see `reconnecting(attempt)`
then `reconnected` and `connected`/`authenticated` again. If no message arrives from the
server for `heartbeat_timeout` seconds (default 45), the SDK treats the link as dropped and
reconnects — so half-open connections recover too. Set
`rejoin_last_room_on_reconnect = true` to automatically re-join the last room/lobby (by
code) after reconnecting. Calling `disconnect_from_server()` is treated as intentional and
does **not** auto-reconnect.

## Editor plugin

Enabling the plugin also adds a **SpawnWeaver dock** to the Godot editor (right panel) so you
can set things up without leaving Godot:

- **Configuration** — paste your public key + server URL, pick an environment, toggle debug
  logging, and **Save**. Saved to `addons/multiplayer_service/spawnweaver.cfg`.
- **Test connection / guest login** — verifies your credentials against the backend from the
  editor and shows your guest `playerId`.
- **Generate a starter scene** — creates a working **Room Chat**, **Lobby**, **Matchmaking**, or
  **State Sync Player** scene under `res://spawnweaver/` that auto-connects using your saved
  credentials (`MultiplayerService.connect_using_config()`).
- **Test with multiple players** — instructions for *Debug → Run Multiple Instances*.
- **Open** — buttons that open your dashboard, the session debugger, and docs.

The config file is also read at runtime: call `MultiplayerService.connect_using_config()` to
connect using the credentials you saved in the dock (no hard-coded keys in your scenes). The
bundled **examples auto-fill** their URL + key from the saved config too, so once you've saved
credentials in the dock you just open an example and click **Connect**.

## Drop-in sync node (`SpawnSync`)

Replicate a node's movement with **no send/receive code**. Add a **SpawnSync** node as a child of
a `Node2D`/`Node3D`, tick **local** on the copy this client controls, and remote copies move
smoothly via built-in interpolation.

```gdscript
# Your own avatar (this client controls it):
var me := PlayerScene.instantiate()
me.get_node("SpawnSync").local = true        # registers + streams transform; entity_id = player_id
add_child(me)

# A copy of another player (spawn one per entity id you see):
var other := PlayerScene.instantiate()
other.get_node("SpawnSync").entity_id = some_player_id   # set before adding to the tree
add_child(other)                              # interpolates that entity; frees itself on delete
```

Key inspector properties: `local`, `entity_id`, `sync_position`/`sync_rotation`/`sync_scale`,
`synced_properties` (extra parent vars — primitives only), `send_rate`, `interpolate`,
`interpolation_speed`, `free_parent_on_delete`. A local node also **deletes its entity** when it
leaves the tree, so despawns replicate automatically. Use `set_local(true)` to take control after
a node is already in the tree. Full guide: **`/dashboard/docs/sync-nodes`**.

## State sync

Keep connected players aligned with shared **room state** and per-player **entity state**.
Late joiners get a full snapshot automatically; every change is broadcast to the room.

```gdscript
# Room state (the room host sets this):
MultiplayerService.patch_room_state({ "phase": "combat", "round": 2 })
MultiplayerService.room_state_changed.connect(func(patch, state): print(state.phase))

# Entity state (you own entities you create):
MultiplayerService.set_entity_state("player_1", { "x": 120, "y": 80, "hp": 70 })
MultiplayerService.patch_entity_state("player_1", { "x": 121 })
MultiplayerService.entity_state_changed.connect(func(id, patch, full): move(id, full))
MultiplayerService.delete_entity_state("player_1")

# On join you receive everything currently in the room:
MultiplayerService.state_snapshot_received.connect(func(snap):
    for e in snap.entities: spawn(e.entityId, e.state))
```

Rules (v1): only an entity's **owner** can update/delete it; only the room **host** can patch
room state; updates are rate-limited and size-capped (defaults: 4 KB/entity, 16 KB/room, 50
entities/room, 10 updates/sec/client). Rejections arrive on `state_update_rejected`.

## Examples

Full, runnable example projects live in [`examples/`](./examples/README.md), each with its
own README and runnable with two local clients:

- **[Realtime Chat Room](./examples/chat_room/)** — rooms + live chat + roster.
- **[Lobby + Ready Check](./examples/lobby_ready/)** — lobbies, ready check, host starts game.
- **[1v1 Matchmaking Arena](./examples/matchmaking_arena/)** — queue, match, movement.

Smaller demos: `examples/movement_demo.tscn` (shared-movement dots via `game.event`) and
`examples/basic_lobby.tscn` (connect/create/join UI showing every signal).

## Example scene

`examples/basic_lobby.tscn` (the project's main scene) is a tiny UI that lets you connect,
create or join a room by code, see the player list update as others join/leave, and send a
test event. Its script (`examples/basic_lobby.gd`) shows how to wire every signal.

**Try it with two players:**

The fastest path is the repo-root **quickstart** — it starts the backend, provisions a
project, and writes this folder's `spawnweaver.cfg` for you (no key copy/paste):

```powershell
./quickstart.ps1      # Windows;  ./quickstart.sh on macOS/Linux
```

Then open this folder (`sdk/godot-gdscript`) in Godot 4.3+, *Debug → Run Multiple Instances
→ 2 instances*, run, and click **Connect** in both windows.

Or do it manually:

1. Start the backend (e.g. `dotnet run --project src/Platform.Api`) and note the port.
2. Sign up and create a project (`POST /api/auth/signup` then `POST /api/projects`) → copy
   the `publicKey` (`pk_...`). Project creation requires a signed-in account.
3. Open this folder (`sdk/godot-gdscript`) in Godot 4.3+.
4. *Debug → Run Multiple Instances → 2 instances*, then run the project.
5. In both windows: paste the project key, fix the URL/port, click **Connect**.
6. Window A: **Create Room** → note the code. Window B: type the code → **Join Room**.
   Both windows show the roster update; leaving/closing one notifies the other.
