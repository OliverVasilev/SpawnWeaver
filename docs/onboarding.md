# Developer Onboarding (Alpha)

Welcome to the SpawnWeaver alpha. This gets you from zero to two players moving in the same
room. It assumes you have an alpha endpoint URL (e.g. `https://alpha.example.com`); if you're
running it yourself, see [alpha-hosting.md](./alpha-hosting.md).

> **Running it locally?** The repo-root `quickstart.ps1` / `quickstart.sh` does steps 1–3
> for you (start backend → create project → wire up the SDK config). See the
> [README quickstart](../README.md#quickstart-one-command).

## 1. Create a project

Project creation requires a developer account. Sign up (once), keeping the session cookie,
then create a project:

```bash
curl -c jar.txt -X POST https://alpha.example.com/api/auth/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"you@studio.com","displayName":"You","password":"supersecret123"}'

curl -b jar.txt -X POST https://alpha.example.com/api/projects \
  -H "Content-Type: application/json" -d '{"name":"My Game"}'
# -> { "id":"proj_…", "publicKey":"pk_…", "secretKey":"sk_…", ... }
```

- **`publicKey`** (`pk_…`): safe to ship in your game client. Used to connect.
- **`secretKey`** (`sk_…`): server-side only; shown **once**. Used for player storage.

(You can also do all of this in the dashboard: sign up at `/dashboard`, then create a
project in the onboarding wizard.)

## 2. Download the SDK

The Godot 4 GDScript SDK is the `addons/multiplayer_service/` folder in
[`sdk/godot-gdscript`](../sdk/godot-gdscript). Copy that folder into your project's
`addons/`, or package it:

```bash
# From the repo root, make a distributable zip:
cd sdk/godot-gdscript && zip -r ../../multiplayer_service.zip addons/multiplayer_service
```

Enable **SpawnWeaver Multiplayer Service** in *Project → Project Settings → Plugins* (this
registers the `MultiplayerService` autoload).

## 3. Connect and play

Use a secure `wss://` URL for the hosted alpha:

```gdscript
MultiplayerService.configure("pk_your_public_key")
MultiplayerService.connect_to_server("wss://alpha.example.com/connect")
# on `connected`: MultiplayerService.create_room("Alice")   # share the room code
# the other player: MultiplayerService.join_room("CODE", "Bob")
# then: MultiplayerService.send_event("player_moved", {"x":10,"y":5}, MultiplayerService.current_room_id)
```

Full walkthrough: [Multiplayer in 10 minutes](./tutorial.md). Try the bundled
**movement demo** (`sdk/godot-gdscript/examples/movement_demo.tscn`) with two instances.

## 4. Send feedback

It's an alpha — please report bugs and requests:

- Use the feedback form on the landing page (`/`), **or**
- `POST /api/feedback` with `{ "email": "you@example.com", "message": "…" }`.

## Good to know

- Player identity is anonymous and **stable across reconnects** (the SDK stores a token and
  auto-reconnects).
- Rooms are in-memory and expire when empty; lobbies add visibility/metadata/max-players;
  matchmaking forms a room when enough players queue for the same `gameMode`/`region`.
- See [known limitations](./known-limitations.md) before you build something big.
