# SpawnWeaver — Example Projects

Complete, runnable Godot examples that demonstrate real multiplayer use cases end-to-end.
Each one runs with **two local clients** (Godot's *Debug → Run Multiple Instances*) and has
its own README with setup and troubleshooting.

All examples live in this one Godot project (`sdk/godot-gdscript`) so they share the bundled
`MultiplayerService` addon. Open the folder in Godot 4.2+, then run any scene below.

| Example | Teaches | Backend features | Folder |
|---|---|---|---|
| **Realtime Chat Room** | connect, guest auth, rooms, room events, player list | rooms, event relay | [`chat_room/`](./chat_room/) |
| **Lobby + Ready Check** | lobbies, ready check, host-starts-game | lobbies, event relay | [`lobby_ready/`](./lobby_ready/) |
| **1v1 Matchmaking Arena** | matchmaking, generated rooms, movement | matchmaking, rooms, events | [`matchmaking_arena/`](./matchmaking_arena/) |
| **Simple State Sync Demo** | entity/room state, snapshot on join, delete | rooms, state sync | [`state_sync/`](./state_sync/) |

Also bundled (earlier, smaller demos):

- [`basic_lobby.tscn`](./basic_lobby.gd) — bare connect / create / join / send-event UI.
- [`movement_demo.tscn`](./movement_demo.gd) — shared-movement dots over `game.event`.

## Quick start (any example)

1. Run the backend: `dotnet run --project src/Platform.Api`.
2. Create a project and copy its **public key** (`pk_…`) from the dashboard.
3. **Set it once:** in the Godot editor's **SpawnWeaver dock**, paste your public key + server
   URL and click **Save credentials**. Every example then **pre-fills those fields** — open a
   scene and just click **Connect** (no retyping the key or fixing the port).
4. Run the example scene; use *Debug → Run Multiple Instances* for a second player.

> No dock config? The examples still work — just type the key + URL into the on-screen fields.

The dashboard's onboarding recommends the best example for your game type (e.g. a 1v1 arena
project points you at **1v1 Matchmaking Arena**).

## Not yet included

These map to backend features that land in later milestones, so they're intentionally
deferred rather than shipped half-working:

- **Persistent Player Profile** — needs player-scoped storage auth so a game client can read/
  write its own data without the project **secret** key (a later persistence pass).
