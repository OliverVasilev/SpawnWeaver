# Example: Simple State Sync Demo

Each player owns a colored dot. Positions are kept in sync with **entity state** (not raw
events), so a player who joins late immediately sees everyone via a **snapshot**. The host
controls a shared **round** counter via room state.

## What this teaches

- **Spawn & own** an entity (`set_entity_state`) keyed by your player id
- **Patch** entity state as you move (`patch_entity_state`)
- **Receive** other players' changes (`entity_state_changed`)
- **Late-join snapshot** (`state_snapshot_received`) — see everyone already present
- **Delete** an entity (`delete_entity_state` / `entity_state_deleted`)
- **Shared room state** (`patch_room_state`, host only) + `room_state_changed`

## Required platform features

- Realtime connection + guest auth
- Rooms
- State sync (room state + entity state)

## Dashboard setup

1. Start the backend: `dotnet run --project src/Platform.Api`.
2. Sign in to the dashboard, create a project, and copy the **public key** (`pk_…`).

## Godot setup

1. Open `sdk/godot-gdscript` in Godot 4.2+.
2. Run `examples/state_sync/state_sync.tscn`.

## Run two clients locally

1. **Debug → Run Multiple Instances → 2 instances**, then run the scene.
2. Both windows: paste the **public key**, fix the URL/port, click **Connect**.
3. Window A: **Create Room** (A becomes host). Window B: paste the code → **Join** — B's view
   immediately shows A's dot from the **snapshot**.
4. Move with the **arrow keys**; each dot moves on both screens. Press **Next round** (host) to
   bump the shared round; **Delete mine** removes your dot for everyone.

## Common errors & troubleshooting

| Symptom | Cause / fix |
|---|---|
| "Only the host can change room state" | Room state is host-only by design; entity state is owner-only. |
| "Rejected: You don't own this entity" | You can only update entities you created. |
| A dot doesn't appear for a late joiner | The owner must have set state before you joined; the snapshot only includes existing entities. |
| "Rejected: The state is too large" | Entity state is capped (4 KB default) — keep payloads small. |
| Movement feels steppy | Updates are throttled (~16/sec) and not interpolated; add interpolation client-side for smoothness. |

## Related docs

- [Realtime protocol](../../../../docs/protocol.md) — the `state.*` messages
- [SDK README](../../README.md) — the State sync section
