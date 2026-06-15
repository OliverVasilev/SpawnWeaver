# Multiplayer in 10 minutes (Godot)

Add real-time multiplayer to a Godot game with SpawnWeaver. No backend code required.

## 1. Run the backend (2 min)

```bash
# From the repo root:
docker compose -f deploy/docker-compose.test.yml up --build -d
curl https://spawnweaver.dev/health      # { "status": "ok", ... }
```

(Or run it directly: `dotnet run --project src/Platform.Api` — note the port it prints.)

## 2. Create a project & copy your public key (1 min)

```bash
curl -X POST https://spawnweaver.dev/api/projects \
  -H "Content-Type: application/json" -d '{"name":"My Game"}'
# -> { "id":"proj_…", "publicKey":"pk_…", "secretKey":"sk_…", ... }
```

Copy the **`publicKey`** (`pk_…`). It's safe to ship in your game client.

## 3. Install the SDK (1 min)

Copy `sdk/godot-gdscript/addons/multiplayer_service/` into your Godot project's `addons/`
folder and enable **SpawnWeaver Multiplayer Service** in *Project → Project Settings →
Plugins*. This registers a `MultiplayerService` autoload.

## 4. Connect and make a room (3 min)

```gdscript
func _ready() -> void:
    MultiplayerService.connected.connect(_on_connected)
    MultiplayerService.room_created.connect(func(id, code, players):
        print("Share this room code with a friend: ", code))
    MultiplayerService.player_joined.connect(func(room, player):
        print("A player joined: ", player.get("playerId")))
    MultiplayerService.event_received.connect(_on_event)
    MultiplayerService.error_received.connect(func(code, msg):
        push_warning(MultiplayerService.describe_error(code)))

    MultiplayerService.configure("pk_your_public_key")          # from step 2
    MultiplayerService.connect_to_server("wss://spawnweaver.dev/connect")

func _on_connected() -> void:
    MultiplayerService.create_room("Alice")                    # or join_room("CODE")
```

## 5. Send and receive game events (3 min)

```gdscript
# Send your position to everyone else in the room:
func _physics_process(_delta):
    MultiplayerService.send_event("player_moved",
        {"x": position.x, "y": position.y},
        MultiplayerService.current_room_id)

# React to other players' events:
func _on_event(event: String, data: Dictionary, from_player_id: String):
    if event == "player_moved" and from_player_id != MultiplayerService.player_id:
        # move that player's avatar to (data.x, data.y)
        pass
```

That's it — two clients in the same room now see each other move in real time.

## Try the demo

Open `sdk/godot-gdscript` in Godot 4.3+, set the main scene to
`examples/movement_demo.tscn`, run **two instances** (Debug → Run Multiple Instances),
Connect in both, Create a room in one and Join with the code in the other, then move with
the arrow keys.

## What you get for free

- **Stable identity + reconnect:** the SDK stores a `player_token` and, on an unexpected
  drop, automatically reconnects and resumes the same `player_id`.
- **Lobbies:** `create_lobby` / `list_lobbies` / `join_lobby` for public/private rooms.
- **Matchmaking:** `join_matchmaking("duel")` → `match_found` when enough players are waiting.
- **Friendly errors:** `MultiplayerService.describe_error(code)` turns a code into a message.

See the [SDK README](../sdk/godot-gdscript/README.md) and the
[protocol reference](./protocol.md) for the full API.
