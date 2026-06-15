# Example: 1v1 Matchmaking Arena

Two players each click **Find Match**, get paired into a generated room, and move colored
dots around an arena — positions are relayed in real time. **End Match** returns to the queue.

## What this teaches

- Automatic **guest login**
- Entering the **matchmaking queue** (`join_matchmaking`)
- Being **matched** with another player into a server-generated room (`match_found`)
- Exchanging **movement events** in the match (`send_event` / `event_received`)
- **Ending a match** and returning to matchmaking

## Required platform features

- Realtime connection + guest auth
- Matchmaking (2-player queue)
- Rooms + event relay (movement)

## Dashboard setup

1. Start the backend: `dotnet run --project src/Platform.Api`.
2. Create a project and copy the **public key** (`pk_…`).

## Godot setup

1. Open `sdk/godot-gdscript` in Godot 4.2+.
2. Run `examples/matchmaking_arena/matchmaking_arena.tscn`.

## Run two clients locally

1. **Debug → Run Multiple Instances → 2 instances**, then run the scene.
2. Both windows: paste the **public key**, fix the URL/port, click **Connect**.
3. Both windows: click **Find Match**. Once two players are queued for the same mode
   (`duel_1v1`), the server pairs them and both drop into the same room.
4. Move your dot with the **arrow keys**; the other window shows your dot moving.
5. **End Match** leaves the room and re-enables Find Match.

> Movement updates are throttled to ~20/sec to stay under the per-connection rate limit.

## Common errors & troubleshooting

| Symptom | Cause / fix |
|---|---|
| Stuck on "Searching…" | Both clients must queue for the **same** game mode. The default `match_size` here is 2. |
| "No opponent found — try again" | The queue timed out (default 30s) with no match. Click Find Match again. |
| Opponent dot doesn't move | Confirm both reached "Match found". Movement only sends while a key is held. |
| Match ends unexpectedly | If the opponent disconnects, the room empties and the match ends. |
| "You are sending messages too quickly" | Rate limit — the example already throttles; lower the send rate if you changed it. |

## Related docs

- [Realtime protocol](../../../../docs/protocol.md) — matchmaking and event messages
- [SDK README](../../README.md)
- [10-minute tutorial](../../../../docs/tutorial.md)
