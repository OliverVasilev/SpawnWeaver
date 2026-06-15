# Example: Realtime Chat Room

A minimal multiplayer chat: connect, create or join a room by code, and exchange messages
that everyone in the room sees — with a live list of connected players.

## What this teaches

- Connecting to SpawnWeaver and automatic **guest authentication**
- Creating a room and **joining by code**
- Sending and receiving **room events** (`send_event` / `event_received`)
- Tracking the **connected players** as they join and leave

## Required platform features

- Realtime connection + guest auth
- Rooms
- Event relay

(No matchmaking, lobbies, persistence, or state sync needed.)

## Dashboard setup

1. Start the backend: `dotnet run --project src/Platform.Api`.
2. Create a project: `POST /api/projects` (or use the dashboard's **Quick create**), then copy
   the **public key** (`pk_…`). The secret key is not needed for this example.

## Godot setup

1. Open `sdk/godot-gdscript` in Godot 4.2+ (the `MultiplayerService` autoload is registered
   by the bundled addon).
2. Run `examples/chat_room/chat_room.tscn`.

## Run two clients locally

1. In Godot: **Debug → Run Multiple Instances → 2 instances**, then run the chat scene
   (or run it twice).
2. In both windows: paste your project's **public key** (the server URL defaults to
   `wss://spawnweaver.dev/connect`), click **Connect**.
3. Window A: **Create Room** and share the shown code. Window B: type the code, click **Join**.
4. Type a message and press **Enter** (or **Send**). Both windows see it, plus join/leave notices.

## Common errors & troubleshooting

| Symptom | Cause / fix |
|---|---|
| "Connection error: …" | Wrong URL/port, or the backend isn't running. Check the `connect_to_server` URL. |
| Connects then immediately drops | Bad or inactive **public key**. Re-copy `pk_…` from the dashboard. |
| "Error: That room or lobby does not exist" | Wrong room code, or the room expired (empty rooms are cleaned up). Create a fresh one. |
| Messages don't arrive | Both clients must be in the **same** room. Confirm the player count shows 2+. |
| "You are sending messages too quickly" | The per-connection rate limit kicked in — slow down (it's `retryable`). |

## Related docs

- [10-minute tutorial](../../../../docs/tutorial.md)
- [Realtime protocol](../../../../docs/protocol.md)
- [SDK README](../../README.md)
