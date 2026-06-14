# Example: Lobby + Ready Check

A pre-game lobby: players join, mark themselves **ready**, and the **host** starts the game
once everyone is ready — then everyone transitions out of the lobby together.

## What this teaches

- Creating and **listing public lobbies**, and joining by code or from the list
- Tracking the **player roster** in a lobby
- A **ready check**: each player toggles ready; everyone sees the updated state
- **Host-starts-game** flow and transitioning out of the lobby

Ready-state and the start signal are sent as **room events** between lobby members
(lobbies are rooms, so `send_event` / `event_received` work inside them). A late joiner
gets caught up because each ready player re-announces when someone new joins.

## Required platform features

- Realtime connection + guest auth
- Lobbies (create / list / join)
- Event relay (for ready-state + start)

## Dashboard setup

1. Start the backend: `dotnet run --project src/Platform.Api`.
2. Create a project and copy the **public key** (`pk_…`).

## Godot setup

1. Open `sdk/godot-gdscript` in Godot 4.3+.
2. Run `examples/lobby_ready/lobby_ready.tscn`.

## Run two clients locally

1. **Debug → Run Multiple Instances → 2 instances**, then run the scene.
2. Both windows: paste the **public key**, fix the URL/port, click **Connect**.
3. Window A: **Create Lobby** — A becomes the **host** and sees a **Start Game** button.
4. Window B: **List Lobbies** then double-click the lobby (or paste the code and **Join**).
5. Each player clicks **Ready** (✓ appears next to their name). When *everyone* is ready,
   the host's **Start Game** enables — click it; both windows show the game starting.

## Common errors & troubleshooting

| Symptom | Cause / fix |
|---|---|
| Lobby list is empty | Only **public** lobbies are listed; create one first, or join by code. |
| "That room or lobby is full" | The lobby hit its max players. Create another or raise `max_players`. |
| Start Game stays disabled | It only enables for the **host** once **all** members (2+) are ready. |
| Ready ticks don't show for a late joiner | Existing players re-announce on join; if you raced it, toggle ready again. |
| "That room or lobby does not exist" | Wrong code, or the lobby expired while empty. Create a fresh one. |

## Related docs

- [Realtime protocol](../../../../docs/protocol.md) — lobby and event messages
- [SDK README](../../README.md)
- [10-minute tutorial](../../../../docs/tutorial.md)
