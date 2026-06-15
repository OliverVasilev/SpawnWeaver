# SpawnWeaver

A multiplayer Backend-as-a-Service for indie **Godot** developers — add multiplayer to a
game with minimal backend knowledge.

```gdscript
MultiplayerService.connect("game-id")
MultiplayerService.create_lobby()
MultiplayerService.join_lobby("lobby-code")
MultiplayerService.send("player_moved", data)
```

Built as a **modular monolith** on .NET / ASP.NET Core. See
[`godot_multiplayer_platform_milestone_plan.md`](./godot_multiplayer_platform_milestone_plan.md)
for the full product direction, architecture, and milestone plan.

> **Toolchain note:** the plan targets .NET 10 LTS. This repo currently targets **`net9.0`**
> (the installed SDK); retarget via `Directory.Build.props` once .NET 10 is available.

## Quickstart (one command)

From a fresh clone, get from zero to **two players moving** with a single command. It
starts the backend, creates a developer account and project for you, and writes the public
key into the Godot SDK config so the bundled examples connect with **no copy/paste**:

```powershell
# Windows / PowerShell
./quickstart.ps1
```

```bash
# macOS / Linux
./quickstart.sh
```

Then open `sdk/godot-gdscript` in **Godot 4.3+**, choose *Debug → Run Multiple Instances → 2
instances*, and press **Play** — both windows are pre-configured; just click **Connect**.

The script is idempotent (re-running reuses the same account and project, so the public key
stays stable) and prints the API URL, public key, and dashboard link when it finishes. Stop
it with `Ctrl+C`. Prefer containers? Run it against Docker Compose instead:

```powershell
./quickstart.ps1 -Docker      # or: ./quickstart.sh --docker
```

Only need provisioning (e.g. in CI), not a foreground server? Add `-NoServe` / `--no-serve`.
Generated credentials are kept in the git-ignored `.quickstart/` folder — **local dev only**.

Prefer to do it by hand? Follow [Build & test](#build--test) and the steps below.

## Status

Milestone progress (one milestone at a time — see the plan, §9 Rule 1):

- [x] **Milestone 1 — Repository Bootstrap**
- [x] **Milestone 2 — Domain Model & Project Registration**
- [x] **Milestone 3 — WebSocket Connection Gateway**
- [x] **Milestone 4 — Protocol Envelope**
- [x] **Milestone 5 — Basic Rooms**
- [x] **Milestone 6 — Godot GDScript SDK MVP**
- [x] **Milestone 7 — Game Event Relay**
- [x] **Milestone 8 — Local Playtest Infrastructure**
- [x] **Milestone 9 — Authentication v1**
- [x] **Milestone 10 — Lobby System**
- [x] **Milestone 11 — Matchmaking v1**
- [x] **Milestone 12 — Persistence v1**
- [x] **Milestone 13 — Dashboard v1**
- [x] **Milestone 14 — Observability and Diagnostics**
- [x] **Milestone 15 — Performance Pass v1**
- [x] **Milestone 16 — SDK Developer Experience Pass**
- [x] **Milestone 17 — Security Pass v1**
- [x] **Milestone 18 — Public Alpha** ← MVP complete 🎉

Differentiation phase (see [`godot_multiplayer_platform_milestone_plan.md`](./godot_multiplayer_platform_milestone_plan.md)):

- [x] **Milestone 19 — Account System & Product Onboarding**
- [x] **Milestone 20 — Godot-Native SDK Polish**
- [x] **Milestone 21 — Full Working Example Projects**
- [x] **Milestone 22 — Multiplayer Debugger Dashboard**
- [x] **Milestone 23 — Simple State Sync v1**
- [x] **Milestone 24 — Godot Editor Plugin**
- [x] **Milestone 25 — Documentation & Tutorials v1**

## Solution layout

```
src/
  Platform.Api/            ASP.NET Core host (HTTP control plane) — has /health
  Platform.Realtime/       Realtime gateway (WebSocket) — rooms, lobbies, matchmaking
  Platform.Dashboard/      Blazor (static SSR) admin dashboard, hosted at /dashboard
  Platform.Application/    Use cases / orchestration
  Platform.Domain/         Entities and domain rules
  Platform.Infrastructure/ Persistence, IDs, time, observability
  Platform.Contracts/      Public HTTP + realtime DTOs (shared with SDK & tests)
  Platform.Tests/          Unit / integration / protocol / load tests
sdk/                       Godot GDScript SDK (and C# later)
samples/                   Example Godot projects
deploy/                    Docker Compose profiles
docs/                      Architecture, protocol, SDK, infra, milestones
```

Reference direction: `Api → Application + Infrastructure + Contracts`,
`Infrastructure → Application + Domain`, `Application → Domain`. The hot realtime
path is kept separate from the control plane (see plan §2.3).

## Prerequisites

- [.NET SDK 9.0+](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (optional — only for the container flow)

## Build & test

```bash
dotnet build
dotnet test
```

## Run the API

Locally:

```bash
dotnet run --project src/Platform.Api
# then in another shell:
curl http://localhost:5000/health    # or the port printed on startup
```

Via Docker Compose (listens on `:8080`):

```bash
docker compose -f deploy/docker-compose.yml up --build
curl http://localhost:8080/health
```

For a near-free **playtest** (single container, persisted SQLite) use the test profile and
see the [infrastructure & playtest guide](./docs/infrastructure.md):

```bash
docker compose -f deploy/docker-compose.test.yml up --build -d
```

Generate load with the included tool:

```bash
dotnet run --project tools/Platform.LoadTest -- --api http://localhost:8080 --clients 40 --seconds 15
```

Expected response:

```json
{ "status": "ok", "service": "Platform.Api", "version": "1.0.0.0" }
```

## API

### Health

`GET /health` → `200` `{ "status": "ok", "service": "Platform.Api", "version": "…" }`

### Projects (Milestone 2)

Register a project and receive a public key (safe to embed in the game) plus a
secret key (server-side only, **shown once**):

```bash
curl -X POST http://localhost:5000/api/projects \
  -H "Content-Type: application/json" -d '{"name":"My Game"}'
# 201 -> { "id":"proj_…", "name":"My Game", "publicKey":"pk_…", "secretKey":"sk_…", "createdAtUtc":"…" }

curl http://localhost:5000/api/projects/proj_xxxxxxxx
# 200 -> { "id":"proj_…", "name":"My Game", "publicKey":"pk_…", "isActive":true, "createdAtUtc":"…" }   (no secret)
```

The secret key is stored only as a SHA-256 hash; it is never persisted or returned again.

### Player storage (Milestone 12)

Project-scoped key-value storage for players. Operations require the project's **secret**
key as a bearer token:

```bash
curl -X PUT http://localhost:5000/api/storage/proj_xxx/players/player_1/keys/score \
  -H "Authorization: Bearer sk_xxx" -d '42'

curl http://localhost:5000/api/storage/proj_xxx/players/player_1/keys/score \
  -H "Authorization: Bearer sk_xxx"
# 200 -> { "key": "score", "value": "42", "updatedAtUtc": "…" }
```

`GET …/keys` lists a player's keys; `DELETE …/keys/{key}` removes one. Data is isolated per
project; values and key counts are quota-limited (`Storage__*` env vars).

### Accounts & onboarding (Milestone 19)

Developers sign up for a dashboard account and get a personal **workspace**
(organization). Sign-up provisions the account, hashes the password with PBKDF2, and
starts a server-side session carried in an HttpOnly cookie:

```bash
curl -c jar.txt -X POST http://localhost:5000/api/auth/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"you@studio.com","displayName":"You","password":"supersecret123"}'

curl -b jar.txt http://localhost:5000/api/account     # signed-in profile + workspace
```

Project creation captures an **onboarding profile** (game type, multiplayer mode,
persistence needs) and returns a tailored **recommended setup** plus the best-matching
example project. When authenticated, the project is attached to your workspace:

```bash
curl -b jar.txt -X POST http://localhost:5000/api/projects \
  -H "Content-Type: application/json" \
  -d '{"name":"Duel Arena","gameType":"Arena1v1","multiplayerMode":"MatchmakingAndRooms","persistenceFeatures":["PlayerProfile"]}'
# 201 -> { …, "slug":"duel-arena", "organizationId":"org_…",
#          "recommendedSetup": { "exampleProject":"1v1 Matchmaking Arena", "steps":[…] } }
```

The dashboard adds **Sign in / Sign up / Account** pages and a guided onboarding wizard at
`/dashboard/onboarding`. Account settings shows active sessions and supports
"sign out all other devices". `GET /api/onboarding/options` lists the wizard's choices.

Creating a project requires a signed-in developer account: `POST /api/projects` returns
`401` when anonymous, and the project is attached to your workspace. (The
[quickstart script](#quickstart-one-command) automates the whole sign-up → create-project →
copy-key chain for local dev.)

### Admin dashboard (Milestone 13)

The dashboard at **`/dashboard`** requires a developer account. Signed-in navigation is
**Home · Projects · Debugger · Help**; the technical debugger pages (live activity, sessions,
errors, debug bundle, logs) live under the **Debugger** hub. Creating a project requires
signing in (`POST /api/projects` → 401 when anonymous) and attaches it to your workspace.

Logged-out visitors can only see the polished **landing page** (`/`) and a public
**Getting Started** page (`/dashboard/getting-started`) with the sign-up prompt and short
tutorials — every other `/dashboard/*` page redirects there until you sign in.

The read-only admin JSON API under `/api/admin/*` is open by default for local/internal use —
set `Admin__ApiKey` to require `Authorization: Bearer <key>` and keep it behind your
tunnel/proxy.

### Multiplayer debugger (Milestone 22)

The dashboard explains *why* a multiplayer session failed:

- **Session inspector** (`/dashboard/sessions/{id}`) — per-connection timeline (connected →
  authenticated → actions → rejections → disconnected) plus IP, SDK + Godot versions, current
  room, auth status, and disconnect reason.
- **Error explorer** (`/dashboard/errors`) — protocol errors aggregated by code with counts,
  affected sessions, and a suggested fix each.
- **Room/lobby + matchmaking inspectors** — members, host, metadata, and queue contents.
- **Debug bundle viewer** (`/dashboard/debug`) — paste the Godot SDK's
  `create_debug_report_string()` to inspect a player's state offline.

Backed by `GET /api/admin/{sessions/{id}, errors, matchmaking, rooms/{id}}`. The Godot SDK
reports its `sdkVersion`/`engine` on connect so the inspector can show them.

### Observability (Milestone 14)

Every HTTP response carries an `X-Correlation-Id`; realtime connections are traceable by
`connectionId`. Metrics (connections, rooms, messages, errors) are available at
`GET /api/admin/metrics` and published via an OpenTelemetry `Meter` ("SpawnWeaver") —
`AddOpenTelemetry` exports them (console exporter locally) alongside ASP.NET Core metrics.

### Realtime gateway (Milestone 3)

Open a WebSocket connection using a project's **public** key:

```text
ws://localhost:5000/connect?projectKey=pk_xxxxxxxx
```

On success the server sends a welcome message and tracks the connection:

```json
{ "connectionId": "conn_…", "serverTimeUtc": "…", "type": "connection.welcome" }
```

- Missing/unknown/inactive key → the handshake is rejected with `401`.
- `GET /connect/stats` → `{ "activeConnections": N }` (diagnostics).
- Heartbeats use WebSocket keep-alive ping/pong; disconnects are detected and logged.

Messages use a small JSON envelope — `{ "type": …, "requestId": …, "payload": … }`.
Send `{"type":"ping"}` and the server replies `{"type":"pong"}`; unknown types and
malformed frames return a structured `error`.

Players can create and join **rooms** by code (`room.create` / `room.join` /
`room.leave` / `room.players`); members are notified of joins, leaves, and disconnects.
Clients in a room relay **game events** to each other (`game.event`), subject to
per-connection size and rate limits. See [`docs/protocol.md`](./docs/protocol.md) for the
full message reference.

### Database

SQLite (`spawnweaver.db`) for local/MVP. Migrations live in
`src/Platform.Infrastructure/Database/Migrations` and are applied automatically on
API startup. The `dotnet-ef` CLI is pinned as a local tool:

```bash
dotnet tool restore                       # after a fresh clone
dotnet ef migrations add <Name> --project src/Platform.Infrastructure --startup-project src/Platform.Api --output-dir Database/Migrations
```

## Godot SDK

A Godot 4 GDScript client lives in [`sdk/godot-gdscript`](./sdk/godot-gdscript). It wraps
the WebSocket connection and exposes connection/room/event activity as Godot signals:

```gdscript
MultiplayerService.configure("pk_your_public_key")
MultiplayerService.connect_to_server("ws://127.0.0.1:5000/connect")
MultiplayerService.create_room("Alice")
MultiplayerService.join_room("ABCD12", "Bob")
```

It auto-reconnects after drops, tracks the current room, and ships a movement demo. Start
with the **[Multiplayer in 10 minutes](./docs/tutorial.md)** tutorial, or the
[SDK README](./sdk/godot-gdscript/README.md) for the full API.

**Install it without cloning the repo** — run the one-line installer from your Godot project
root (the server packages the addon and serves it):

```powershell
iwr https://spawnweaver.example/install.ps1 -UseBasicParsing | iex   # Windows
curl -fsSL https://spawnweaver.example/install.sh | bash             # macOS/Linux
```

Use `http://localhost:5159` instead of `spawnweaver.example` when running locally.

## Documentation

- [Multiplayer in 10 minutes](./docs/tutorial.md) — fastest path to two players moving.
- [Developer onboarding (alpha)](./docs/onboarding.md) — create a project, install the SDK, connect.
- [Realtime protocol](./docs/protocol.md) — the message envelope and every message type.
- [Infrastructure & playtest guide](./docs/infrastructure.md) — modes, env vars, Docker, load testing.
- [Hosting on AWS Lightsail](./docs/hosting-aws-lightsail.md) — full production stack (Caddy HTTPS + Postgres) in one command, plus [automatic CI/CD deploys](./docs/hosting-aws-lightsail.md#automatic-deploys-cicd) from GitHub.
- [Hosting the alpha](./docs/alpha-hosting.md) — deploy, lock down, and monitor an alpha.
- [Performance baseline](./docs/performance.md) · [Known limitations](./docs/known-limitations.md) · [Milestone progress](./docs/milestones.md)

## License

TBD.
