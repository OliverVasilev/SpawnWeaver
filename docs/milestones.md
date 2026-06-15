# Milestone Progress

Tracking implementation against the plan in
[`../ai-development-milestones-godot-multiplayer-saas.md`](../ai-development-milestones-godot-multiplayer-saas.md).

Rule: **one milestone at a time**, each ending with `dotnet build` + `dotnet test` green.

---

## ✅ Milestone 1 — Repository Bootstrap

**Goal:** base solution structure and local development environment.

Delivered:

- .NET solution `SpawnWeaver.sln` with the 7 `Platform.*` projects.
- Project references wired per the architecture (control plane vs domain layers).
- `Directory.Build.props`: `net9.0`, nullable reference types, implicit usings,
  .NET analyzers enabled (Recommended).
- `Platform.Api` ASP.NET Core host with:
  - `GET /health` → `200 OK` returning `HealthResponse` (from `Platform.Contracts`).
  - Basic structured console logging.
- `deploy/docker-compose.yml` + `src/Platform.Api/Dockerfile` (multi-stage) for local mode.
- `Platform.Tests` integration tests proving `/health` returns 200 and the expected payload,
  via `WebApplicationFactory<Program>`.
- `nuget.config` pinned to nuget.org; `.gitignore`; `.dockerignore`; README.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| `dotnet build` passes | ✅ |
| `dotnet test` passes (2 tests) | ✅ |
| `docker compose up` starts the backend | ⏳ compose + Dockerfile provided; not run in this env |
| `/health` returns HTTP 200 | ✅ (covered by integration test) |

Not implemented (deferred per Rule 1): rooms, matchmaking, auth, database, dashboard,
Redis, Kubernetes, SDK.

---

## ✅ Milestone 2 — Domain Model & Project Registration

**Goal:** let a developer register a game project and receive a public project ID
plus a server-side API key.

Delivered:

- **Domain** `Project` aggregate (`Id`, `Name`, `PublicKey`, `SecretKeyHash`,
  `CreatedAtUtc`, `IsActive`) with a guarded `Create` factory.
- **Application** `ProjectService` (create + lookup) over repository / unit-of-work /
  clock / id-generator / key abstractions.
- **Infrastructure**: EF Core + **SQLite** `PlatformDbContext`, `ProjectRepository`,
  `UnitOfWork`, `IdGenerator`, `SystemClock`, and security:
  - `ApiKeyGenerator` — URL-safe random `pk_…` / `sk_…` keys.
  - `ApiKeyHasher` — SHA-256, fixed-time `Verify`. **Only the hash is persisted.**
- **Contracts**: `CreateProjectRequest`, `CreateProjectResponse` (secret shown once),
  `ProjectResponse` (no secret).
- **Endpoints**: `POST /api/projects`, `GET /api/projects/{projectId}`.
- **Migration**: `InitialCreate` (table `projects`, unique index on `PublicKey`);
  applied automatically on API startup. `dotnet-ef` pinned as a local tool.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| A project can be created | ✅ `POST` → 201 with id + keys |
| Secret key returned only once | ✅ present in create response, absent from `GET` contract |
| Secret stored hashed, never plaintext | ✅ unit test + verified plaintext absent from the SQLite file |
| Tests verify creation and lookup | ✅ |
| `dotnet build` / `dotnet test` pass | ✅ (10 tests) |

Database deltas (`dotnet ef migrations add …`) require the local tool — restore it with
`dotnet tool restore` after a fresh clone.

---

## ✅ Milestone 3 — WebSocket Connection Gateway

**Goal:** let a Godot/client app open a WebSocket connection to the backend.

Delivered (in `Platform.Realtime` — data plane, kept separate from the control plane):

- `GET /connect` WebSocket endpoint (`ws://host/connect?projectKey=pk_…`), hosted by the
  API process and enabled via `UseWebSockets` with a 30s keep-alive interval.
- **Public-key validation** before the handshake: missing key → `401`, unknown/inactive
  project → `401` (added `ProjectService.GetByPublicKeyAsync`).
- `ConnectionManager` (thread-safe, in-memory) tracking live connections; `RealtimeConnection`
  wraps the socket and serializes writes.
- `RealtimeConnectionHandler`: generates a `conn_…` id, sends a **welcome** message
  (`RealtimeWelcome` contract), runs the receive loop, and detects disconnects.
- **Lifecycle logging** via source-generated `LoggerMessage` (connected / disconnected /
  rejected, event ids 1000–1002).
- Ping/pong heartbeat via WebSocket keep-alive frames.
- `GET /connect/stats` — active connection count (early observability + testability).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Client can connect | ✅ live + TestServer |
| Invalid project key rejected | ✅ `401`, handshake fails |
| Server sends welcome message | ✅ `connection.welcome` with `conn_…` id |
| Server detects disconnect | ✅ stats drop 1 → 0; disconnect log emitted |
| Tests cover valid + invalid connection | ✅ 4 realtime tests |
| `dotnet build` / `dotnet test` pass | ✅ (14 tests) |

Inbound application messages are accepted but not yet interpreted — the message-envelope
protocol is **Milestone 4**.

---

## ✅ Milestone 4 — Protocol Envelope

**Goal:** define and implement protocol v1 for realtime messages.

Delivered:

- **`RealtimeEnvelope`** contract (`Platform.Contracts.Realtime`): `{ type, requestId?, payload? }`,
  payload kept as raw `JsonElement` for per-type interpretation.
- **Message-type registry**: `IRealtimeMessageHandler` implementations registered in DI;
  `MessageDispatcher` routes by `type`. First handler: `ping` → `pong`.
- **JSON (de)serialization**: `EnvelopeReader` (inbound, tolerant parsing) and
  `RealtimeMessageSender` (outbound, null-omitting, relaxed escaping).
- **Structured errors**: `error` envelope carrying `RealtimeError { code, message }` with
  stable codes `malformed_message`, `unknown_message_type`.
- **Request/response correlation**: responses echo the inbound `requestId`
  (via `MessageContext.RespondAsync`).
- Welcome message realigned to the envelope shape (`connection.welcome` +
  `ConnectionWelcomePayload`); M3 tests updated accordingly.
- **`docs/protocol.md`** documents the envelope, messages, and error codes.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Unknown message types return structured errors | ✅ `error` / `unknown_message_type` |
| Malformed payloads return structured errors | ✅ `error` / `malformed_message` |
| Protocol tests verify serialization compatibility | ✅ 5 unit + 5 integration |
| Protocol documentation exists | ✅ `docs/protocol.md` |
| `dotnet build` / `dotnet test` pass | ✅ (24 tests) |

---

## ✅ Milestone 5 — Basic Rooms

**Goal:** let players create and join rooms.

Delivered (in `Platform.Realtime.Rooms` — in-memory, single-node MVP):

- **`Room`** — a small actor that owns its members; mutations serialized by a per-room
  lock, broadcasts done outside the lock from returned snapshots.
- **`RoomManager`** — rooms by id and by code, a connection→rooms reverse index for cheap
  disconnect handling, unique short codes (unambiguous alphabet), and `SweepExpired`.
- **`RoomService`** — orchestrates create/join/leave/list + disconnect and broadcasts to
  members.
- **Message handlers** for `room.create`, `room.join`, `room.leave`, `room.players`
  (registered in the M4 dispatcher).
- **Events**: `room.created`, `room.joined` (to joiner + broadcast to members),
  `room.left` (ack + broadcast, also on disconnect), `room.expired`.
- **Expiry**: `RoomExpiryService` (`BackgroundService` + `PeriodicTimer`) removes rooms
  empty past `RealtimeOptions.EmptyRoomTtl`. Tunable via the `Realtime` config section.
- Project-scoped: joins across projects are rejected (returns `room_not_found`).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| One client can create a room | ✅ `room.created` with code |
| Another client can join by code | ✅ `room.joined` |
| All clients receive player joined/left events | ✅ broadcast on join, leave, and disconnect |
| Empty rooms expire | ✅ unit-tested with a controllable clock |
| Tests cover room lifecycle | ✅ 8 unit + 4 integration |
| `dotnet build` / `dotnet test` pass | ✅ (36 tests) |

---

## ✅ Milestone 6 — Godot GDScript SDK MVP

**Goal:** the first usable Godot plugin.

Delivered (in `sdk/godot-gdscript/`, a Godot 4.3 project):

- **Addon** `addons/multiplayer_service/` — `plugin.cfg`, `plugin.gd` (registers the
  `MultiplayerService` autoload), and `multiplayer_service.gd` (the client).
- **Connection wrapper** over `WebSocketPeer` with project-key query, polled in `_process`.
- **Send/receive abstraction** — JSON envelope encode/decode matching the realtime protocol.
- **Signals**: `connected`, `disconnected`, `connection_error`, `welcomed`,
  `room_created`, `room_joined`, `player_joined`, `player_left`, `room_players`,
  `room_expired`, `event_received`, `error_received`.
- **API**: `configure`, `connect_to_server`, `create_room`, `join_room`, `leave_room`,
  `list_players`, `send_event` (relay in M7), `ping`.
- **Example scene** `examples/basic_lobby.tscn` (+ `.gd`) — connect, create/join by code,
  live roster, send a test event. Documented in `sdk/godot-gdscript/README.md`.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Godot project can connect to local backend | ✅ via `WebSocketPeer` to `/connect` |
| Godot project can create a room | ✅ `create_room` → `room_created` |
| Two Godot clients can join the same room | ✅ verified headless end-to-end against the live backend |
| Events can be sent and received | ✅ SDK surface (`send_event`/`event_received`); end-to-end relay lands in M7 |
| Example scene is documented | ✅ SDK README "Try it with two players" |

> Verified with **Godot 4.6.1** headless: the project imports and runs with no script
> errors, and `tests/headless_smoke.tscn` drives two SDK clients against a running backend
> (client A creates a room, client B joins → `SMOKE: PASS`). Run it yourself with the
> backend up: write `sdk/godot-gdscript/tests/test_config.json` (`{ "url", "key" }`) then
> `Godot --headless --path sdk/godot-gdscript res://tests/headless_smoke.tscn`.

---

## ✅ Milestone 7 — Game Event Relay

**Goal:** let clients in a room send game events to each other.

Delivered:

- **`game.event`** message type + `GameEventHandler`; `RoomService.SendGameEventAsync`
  relays to other room members (sender excluded).
- **Server-side validation**: `roomId` + `event` required; sender must be a member of the
  room (else `room_not_found`).
- **Payload size limit**: messages over `RealtimeOptions.MaxMessageBytes` (default 16 KB)
  rejected with `payload_too_large` (enforced while reading, without buffering the excess).
- **Per-connection rate limit**: lock-free token bucket in the receive loop
  (`MaxMessagesPerSecond` / `MessageBurst`) → `rate_limited`.
- `data` is opaque JSON relayed unchanged; relayed message adds `fromPlayerId`.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Client sends event | ✅ `game.event` |
| Other room clients receive event | ✅ relayed with `fromPlayerId`; verified end-to-end via Godot SDK |
| Oversized messages are rejected | ✅ `payload_too_large` |
| Spammy clients are throttled | ✅ `rate_limited` |
| Tests cover broadcast behavior | ✅ 4 integration tests |
| `dotnet build` / `dotnet test` pass | ✅ (40 tests) |

> End-to-end verified with Godot 4.6.1: the headless smoke test now also relays a
> `player_moved` event from client A to client B via the SDK (`SMOKE: PASS`).

---

## ✅ Milestone 8 — Local Playtest Infrastructure

**Goal:** run closed testing with real players at almost no cost.

Delivered:

- **`deploy/docker-compose.test.yml`** — single .NET container, SQLite persisted to a
  named volume, full env-var configuration, healthcheck, `${PORT}` override.
- **Single-container deployment** — multi-stage `Dockerfile` builds and publishes the API;
  validated end-to-end (image build → run → `/health`, project creation, WebSocket load).
- **Env-var config** — `Database__Provider`, `ConnectionStrings__Default`,
  `Realtime__*` all bindable from the environment.
- **SQLite mode** (default) and **optional PostgreSQL mode**
  (`deploy/docker-compose.postgres.yml` + Npgsql provider; schema via `EnsureCreated`).
- **Observability for playtests** — room lifecycle logs (created/joined/left/expired) and
  `/connect/stats` now reports `activeConnections` **and** `activeRooms`.
- **Load-test tool** `tools/Platform.LoadTest` — connects N clients, groups them into
  rooms, relays game events, reports throughput.
- **Deployment/playtest guide** `ops/infrastructure.md` (modes, Cloudflare Tunnel for
  external testers, env vars, invite flow, load testing).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Backend runs on a single small machine | ✅ single container, validated via Docker |
| External Godot clients can connect | ✅ WS works through the container; tunnel guide documented |
| Logs show active connections and rooms | ✅ connection + room lifecycle logs; `/connect/stats` |
| Test guide explains how to run a playtest | ✅ `ops/infrastructure.md` |
| `dotnet build` / `dotnet test` pass | ✅ (40 tests) |

> Validated: Docker image built and run; `/health`, `POST /api/projects`, and a 12-client
> WebSocket load test all succeeded against the container. A 24-client local load test
> relayed 1,742 events → 5,249 received (≈3× fan-out for rooms of 4).

---

## ✅ Milestone 9 — Authentication v1

**Goal:** add lightweight, stable player identity.

Delivered:

- **Anonymous player auth** — connecting with just a project key mints a new `player_…`
  identity and returns a token in `connection.welcome`.
- **Player / reconnect token** — one stateless, HMAC-SHA256-signed token
  (`{playerId}.{projectId}.{exp}.{sig}`) that both identifies the player and is the
  reconnect credential (presented as `?playerToken=…`). Fresh token issued each connect
  (sliding expiration).
- **Session validation** — signature + project match checked at connect; invalid/tampered/
  wrong-project tokens are rejected with `401`.
- **Token expiry** — embedded expiry enforced; expired tokens rejected at connect.
- **Stable identity everywhere** — `RealtimeConnection.PlayerId` now flows into all room
  and game-event messages (replacing the connection id). Rooms still key membership by
  connection, but report the stable `playerId`.
- **SDK** — stores `player_token` automatically and reuses it on the next
  `connect_to_server()`; new `authenticated(player_id, player_token)` signal.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Anonymous player can reconnect | ✅ verified end-to-end via Godot (same `playerId` after reconnect) |
| Expired token is rejected | ✅ unit-tested (deterministic clock) + endpoint rejects with 401 |
| Player identity stable during a session | ✅ `playerId` constant across connection id changes |
| Tests cover token lifecycle | ✅ 5 token unit tests + 4 auth integration tests |
| `dotnet build` / `dotnet test` pass | ✅ (49 tests) |

> Validated with Godot 4.6.1 headless: a client connected, captured its `playerId` +
> token, disconnected, reconnected (token reused automatically), and resumed the **same**
> `playerId` (`SMOKE: PASS`). The event-relay smoke test still passes.

---

## ✅ Milestone 10 — Lobby System

**Goal:** move from raw rooms to developer-friendly lobbies.

Delivered (lobbies are rooms with attributes — `Room` gained `IsLobby`, `Name`,
`Visibility`, `MaxPlayers`, `Metadata`):

- **Public/private lobbies** — `lobby.create` with `visibility`; public lobbies are listed,
  private are join-by-code only.
- **Lobby metadata** — arbitrary string map carried through create/list/join.
- **Max player count** — capacity enforced atomically in `Room.TryAddMember`; joins past the
  cap return `room_full`. Applies to `room.join` too.
- **Lobby list** — `lobby.list` returns public lobbies for the project (`LobbyService`,
  `RoomManager.ListPublicLobbies`).
- **Join rules** — by `code` (public + private) or by `lobbyId` (public only; private-by-id
  → `room_not_found`).
- **`lobby.closed`** on lobby expiry (vs `room.expired` for plain rooms).
- Shared `RoomBroadcast` helper; `RoomManager.Join` now returns a status (Joined/NotFound/Full).
- **SDK**: `create_lobby`, `list_lobbies`, `join_lobby` (by code), `join_lobby_by_id`, and
  `lobby_created` / `lobby_list` / `lobby_joined` / `lobby_closed` signals.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Create public/private lobby | ✅ |
| Players can list public lobbies | ✅ private lobbies excluded |
| Private lobbies require code | ✅ by-id rejected, by-code works |
| Full lobbies reject new players | ✅ `room_full` |
| `dotnet build` / `dotnet test` pass | ✅ (55 tests) |

> Validated end-to-end via Godot 4.6.1: host creates a public lobby → second client lists
> it → joins by id → both see 2 players (`SMOKE: PASS`).

---

## ✅ Milestone 11 — Matchmaking v1

**Goal:** simple queue-based matchmaking.

Delivered:

- **Matchmaking queue** — `MatchQueue` (pure, thread-safe): tickets bucketed by
  project+gameMode+region+matchSize, FIFO within a bucket, one ticket per connection.
- **Queue join/leave** — `matchmaking.join` → `matchmaking.queued`; `matchmaking.leave`
  → `matchmaking.left`. Disconnect removes the ticket.
- **Auto match creation** — when a bucket reaches its size, `MatchmakingService` creates a
  room (`RoomManager.CreateMatchRoom`), adds the players, and sends `match.found` to each.
- **Region + game-mode fields** — part of the bucket key; only same-bucket players match.
- **Timeout** — `MatchmakingTimeoutService` (`PeriodicTimer`) sweeps tickets older than
  `Realtime__MatchmakingTimeout` (default 30s) and sends `matchmaking.timeout`.
- **SDK** — `join_matchmaking`, `leave_matchmaking` + `matchmaking_queued`, `match_found`,
  `matchmaking_left`, `matchmaking_timeout` signals.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Two or more players can enter queue | ✅ |
| Server creates room/match automatically | ✅ `match.found` with a `roomId` |
| Matched players receive match-found event | ✅ both players, same room |
| Timeout returns clear status | ✅ `matchmaking.timeout` (unit-tested sweep) |
| `dotnet build` / `dotnet test` pass | ✅ (64 tests) |

> Validated end-to-end via Godot 4.6.1: two clients queued for `duel` and were matched
> into the **same** room (`SMOKE: PASS`). Queue/timeout logic is deterministically
> unit-tested via `MatchQueue`.

---

## ✅ Milestone 12 — Persistence v1

**Goal:** simple key-value player/game persistence.

Delivered:

- **`PlayerDataEntry`** domain entity (composite key project+player+key) + EF config +
  `AddPlayerData` migration (table `player_data`).
- **Project-scoped key-value storage** — `PlayerStorageService` + `IPlayerDataRepository`.
- **Endpoints** (control plane, HTTP):
  - `PUT /api/storage/{projectId}/players/{playerId}/keys/{key}` (body = value)
  - `GET …/keys/{key}` → value, `DELETE …/keys/{key}`, `GET …/keys` → key list
- **Authorization** — operations require the project's **secret** key as a bearer token
  (verified against the stored hash), making project isolation enforceable.
- **Quota limits** — `Storage__MaxValueBytes` (64 KB), `Storage__MaxKeyLength` (128),
  `Storage__MaxKeysPerPlayer` (100): oversized → `413`, quota exceeded → `409`,
  invalid key → `400`.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Player data can be saved | ✅ `PUT` → 200 |
| Player data can be loaded | ✅ `GET` → value; 404 if missing |
| Data is scoped by project | ✅ isolation test + secret bound to project |
| Size limits enforced | ✅ `413` for oversized values |
| Tests cover isolation between projects | ✅ 8 integration tests |
| `dotnet build` / `dotnet test` pass | ✅ (72 tests) |

> Validated live: PUT/GET round-trip, 401 without/with wrong secret, 404 for missing key,
> key listing.

---

## ✅ Milestone 13 — Dashboard v1

**Goal:** a minimal developer/admin dashboard.

Delivered:

- **Read-model providers** — `RealtimeDiagnostics` (public facade over the in-memory
  connection/room managers + `SessionTracker`), and `RecentLogStore` + `RecentLogProvider`
  (an `ILoggerProvider` capturing Information+ logs into a ring buffer).
- **Admin JSON API** (`/api/admin/*`, optionally gated by `Admin:ApiKey`):
  `projects`, `projects/{id}`, `realtime` (connections + rooms), `sessions`, `logs`.
- **Blazor dashboard** (`Platform.Dashboard`, static SSR, hosted in the API at `/dashboard`):
  Overview (counts + projects), Realtime (rooms + connections), Sessions, Logs.
- Added `ProjectService.ListAsync` for the project list.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Developer can view project status | ✅ Overview lists projects; details endpoint |
| Active rooms are visible | ✅ Realtime page + `/api/admin/realtime` |
| Active connections are visible | ✅ Realtime page + admin API |
| Recent errors are visible | ✅ Logs page (filter by level) + `/api/admin/logs` |
| `dotnet build` / `dotnet test` pass | ✅ (80 tests) |

> Validated: 5 admin-API + 3 dashboard integration tests, plus live — `/dashboard` renders
> the title and the live project name from the database; admin endpoints return data.
> **Security note:** the dashboard/admin API are open by default for local/internal use;
> set `Admin:ApiKey` to require a bearer token. Full hardening is Milestone 17.

---

## ✅ Milestone 14 — Observability and Diagnostics

**Goal:** make the system debuggable before scaling.

Delivered:

- **Structured logs** (source-generated) plus connection/room/lobby/matchmaking lifecycle
  logging from earlier milestones.
- **Correlation ids** — `CorrelationIdMiddleware` sets/echoes `X-Correlation-Id` and adds it
  to the logging scope. Connections remain traceable by `connectionId`.
- **Metrics** — `RealtimeMetrics` publishes a `Meter` ("SpawnWeaver") with counters
  (connections opened/closed, messages received, errors) and observable gauges (active
  connections/rooms), plus in-process totals.
- **Metrics endpoint** — `GET /api/admin/metrics` (snapshot JSON); also shown on the
  dashboard Overview.
- **Basic OpenTelemetry** — `AddOpenTelemetry().WithMetrics(...)` exports the SpawnWeaver
  meter + ASP.NET Core/Kestrel instrumentation (console exporter outside the test env).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Every connection has a traceable id | ✅ `connectionId` in logs; `X-Correlation-Id` for HTTP |
| Room lifecycle can be followed in logs | ✅ created/joined/left/expired logs |
| Metrics are visible locally | ✅ `/api/admin/metrics`, dashboard, OTel console exporter |
| Load test produces useful metrics | ✅ 16-client load → 16 connections, 880 messages, 0 errors |
| `dotnet build` / `dotnet test` pass | ✅ (83 tests) |

> Validated live: correlation header returned; load test produced
> `connectionsOpened=16, messagesReceived=880, errors=0`; OpenTelemetry console exporter
> emitted `spawnweaver.*` meters alongside `kestrel.*` / `http.server.*`.

---

## ✅ Milestone 15 — Performance Pass v1

**Goal:** reduce unnecessary allocations and establish baseline capacity (measure first).

Delivered:

- **Benchmark project** `tools/Platform.Benchmarks` (BenchmarkDotNet, MemoryDiagnoser):
  envelope parse/serialize + room-broadcast (serialize-per-member vs once).
- **Load test** (existing `tools/Platform.LoadTest`) used as the many-client WebSocket
  benchmark.
- **Measured allocation fix** — broadcasts now **serialize the envelope once** and reuse the
  bytes for all recipients. Allocation/CPU per broadcast is now constant instead of linear
  in room size (32× fewer allocations for a 32-player room — benchmarked before/after).
- **Baseline performance report** — [`docs/performance.md`](./performance.md): micro-benchmarks,
  load-test sweep, max stable connections, messages/sec, and the documented next bottleneck.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Benchmark can simulate many clients | ✅ 200 concurrent, 0 errors |
| Report shows max stable connections | ✅ ≥200 locally (load-generator bound, not server) |
| Report shows messages per second | ✅ ~1.8k in / ~5.4k out per second at 200 clients |
| Clear next bottleneck documented | ✅ sequential per-recipient sends → bounded outbound channels |
| `dotnet build` / `dotnet test` pass | ✅ (83 tests) |

> Findings: JSON parse/serialize is ~0.5–0.7 µs/message (kept, per the rules). The
> serialize-once broadcast fix cut a 32-player broadcast from 16,128 B → 504 B. Load scaled
> linearly 50→200 clients with zero errors.

---

## ✅ Milestone 16 — SDK Developer Experience Pass

**Goal:** make the Godot integration feel simple and polished.

Delivered (in the GDScript SDK):

- **Better errors** — `describe_error(code)` + an `ERROR_DESCRIPTIONS` map; friendlier
  connection-error messages.
- **Reconnect handling** — automatic reconnect with exponential backoff after an
  unexpected drop, reusing the token (`auto_reconnect`, `max_reconnect_attempts`,
  `reconnecting`/`reconnect_failed` signals). Intentional `disconnect_from_server()` does
  not reconnect.
- **Auto room cleanup** — the SDK tracks `current_room_id` and clears it on self-leave,
  room/lobby close, and disconnect.
- **Godot demo project** — `examples/movement_demo.tscn`: a shared-movement game using
  rooms + `game.event`.
- **Tutorial** — `docs/tutorial.md` ("Multiplayer in 10 minutes"); SDK README updated with
  copy-paste examples, the full signal/property tables, and a reconnect section.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| New developer can run the example without reading backend code | ✅ demo + tutorial |
| Common errors produce understandable messages | ✅ `describe_error` |
| SDK README includes copy-paste examples | ✅ quick start, tutorial, demo |
| `dotnet build` / `dotnet test` pass | ✅ (83 tests; SDK validated via Godot) |

> Validated with Godot 4.6.1 headless: clean parse, all four existing e2e tests still pass
> (smoke/reconnect/lobby/matchmaking), and a new **auto-reconnect** test — simulate an
> unexpected drop → `reconnecting attempt 1` → resumed the same `player_id` (`SMOKE: PASS`).

---

## ✅ Milestone 17 — Security Pass v1

**Goal:** prevent obvious abuse before public testing.

Already in place (verified): **API key hashing** (SHA-256, M2), **project-key validation**
(M3), **message size + rate limits** (M7), **cross-project isolation** (rooms M5, storage M12).

Added this milestone:

- **Connection limit per project** — `Realtime__MaxConnectionsPerProject` (0 = unlimited);
  over-limit connections rejected with `429` (`ConnectionManager.CountForProject`).
- **Abuse logging** — Warning-level logs for rejections, oversized messages, and rate
  limiting (rate-limit logged once per connection to avoid spam); visible in the dashboard.
- **Origin allowlist** — optional `Security__AllowedOrigins` (CSWSH protection for browser
  clients); default allows all (native game clients send no Origin).
- **Input validation hardening** — lobby metadata is bounded (`MaxLobbyMetadataEntries`,
  `MaxMetadataValueLength`); oversized/too-many entries rejected with `invalid_payload`.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Invalid project key cannot connect | ✅ `401`, handshake fails |
| Oversized messages are rejected | ✅ `payload_too_large` + abuse log |
| Excessive messages are throttled | ✅ `rate_limited` + abuse log |
| Project data cannot leak across projects | ✅ rooms/lobbies/storage isolation tested |
| `dotnet build` / `dotnet test` pass | ✅ (88 tests) |

> Validated: 5-test security suite (invalid key, per-project connection cap, oversized +
> too-many metadata, cross-project lobby isolation), plus live abuse logs for oversized
> messages and bad-key connects.
> **Note:** the admin API/dashboard remain open by default — set `Admin__ApiKey` before any
> public/exposed deployment (Milestone 18).

---

## ✅ Milestone 18 — Public Alpha

**Goal:** prepare for external indie developer testing.

Delivered:

- **Landing page** at `/` — placeholder with getting-started, links to the dashboard, and a
  feedback form (`Platform.Api.Landing`).
- **Feedback collection** — durable: `FeedbackEntry` domain entity + `feedback` table
  (`AddFeedback` migration) + `FeedbackService`; `POST /api/feedback` (open, validated) and
  `GET /api/admin/feedback` (admin-gated).
- **Developer onboarding** — `docs/onboarding.md` (create project → download SDK → connect →
  feedback), building on the [10-minute tutorial](./tutorial.md).
- **SDK download instructions** — copy/zip the `addons/multiplayer_service` folder (in
  onboarding).
- **Sample Godot project** — the `sdk/godot-gdscript` project with `examples/movement_demo`
  and `examples/basic_lobby`.
- **Known-limitations page** — `docs/known-limitations.md`.
- **Hosted alpha guide** — `ops/alpha-hosting.md` (deploy, lock-down config, public WSS via
  Cloudflare Tunnel, monitoring, feedback) with an operator checklist.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| A developer outside your machine can create a project | ✅ `POST /api/projects` (validated live) |
| They can download the SDK | ✅ addon folder + zip instructions |
| They can connect a Godot sample to your backend | ✅ SDK + demo, proven across milestones |
| You can monitor their session | ✅ dashboard + admin API (sessions/realtime/metrics/logs) |
| Feedback can be collected | ✅ `POST /api/feedback` → stored → `/api/admin/feedback` |
| `dotnet build` / `dotnet test` pass | ✅ (91 tests) |

> Validated live end-to-end: landing page served, project created, feedback submitted and
> listed in the admin API, empty feedback rejected.

---

## MVP complete 🎉

All 18 milestones are done. The MVP definition (plan §12) is met: a developer can create a
project, copy a public key, install the Godot plugin, connect two players, create/join a
room, exchange realtime events, and monitor active rooms/connections — running locally or on
one cheap test machine. Beyond rooms, the platform also has lobbies, matchmaking,
player storage, an admin dashboard, observability/metrics, a security pass, and a public-alpha
surface (landing + feedback).

---

# Differentiation phase

Tracking the second milestone plan
([`../godot_multiplayer_platform_milestone_plan.md`](../godot_multiplayer_platform_milestone_plan.md))
— turning the alpha into a self-serve Godot-native product.

## ✅ Milestone 19 — Account System & Product Onboarding

**Goal:** a real product entry point — developers sign up, get a workspace, create a
project by answering a few onboarding questions, and receive a tailored setup path.

Delivered:

- **User accounts (19.1)** — `User` aggregate (email/displayName/passwordHash + timestamps);
  email is normalized + unique. `AccountService` for sign-up, authentication, display-name
  and password changes. Passwords hashed with **PBKDF2-HMAC-SHA256** (per-password salt,
  100k iterations) in `PasswordHasher` — only the hash is stored.
- **Sessions (19.1)** — server-side `UserSession` rows behind a cookie-auth scheme
  (`DashboardAuth`); each request re-validates the session, so sign-out and
  "sign out all other devices" take effect immediately. Active sessions are listed in
  account settings.
- **Organizations / workspaces (19.2)** — `Organization` aggregate; every sign-up provisions
  a personal workspace. Projects gained a nullable `OrganizationId` and are scoped to the
  signed-in developer's workspace in the dashboard.
- **Project creation v2 (19.3–19.6)** — `Project` extended with `Slug`, `GameType`,
  `MultiplayerMode`, `PersistenceFeatures`, `TargetPlatform`, `Environment`, `UpdatedAtUtc`.
  `POST /api/projects` now accepts the onboarding profile (name-only still works) and
  attaches the developer's organization when authenticated.
- **Recommended setup (19.7)** — `SetupRecommendation` turns the onboarding selections into a
  tailored checklist + best-matching example project; returned in the create response and
  rendered by the onboarding wizard.
- **Endpoints** — `POST /api/auth/{signup,signin,signout}`, `GET/PUT /api/account`,
  `POST /api/account/password`, `GET /api/account/sessions`,
  `POST /api/account/sessions/revoke-all`, `GET /api/onboarding/options`.
- **Dashboard** — sign-in / sign-up / account pages, an onboarding wizard
  (`/dashboard/onboarding`), an auth-aware top bar, and workspace-scoped project lists.
- **Migration** — `AddAccountsAndOnboarding` (tables `users`, `organizations`,
  `user_sessions`; new project columns with safe defaults for existing rows).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| A developer can create an account | ✅ `POST /api/auth/signup` → cookie session |
| A developer can create a project under an organization | ✅ authenticated create attaches the workspace |
| Project creation asks game type, multiplayer mode, persistence | ✅ onboarding wizard + API fields |
| Dashboard recommends a setup path after creation | ✅ tailored checklist + example |
| Recommendation changes by game type | ✅ unit-tested (`SetupRecommendationTests`) |
| Project shows API credentials + SDK setup | ✅ keys (secret once) + Godot snippet |
| `dotnet build` / `dotnet test` pass | ✅ (117 tests) |

> Validated live end-to-end: sign up → workspace created → cookie auth → account retrievable
> → project created under the org with an Arena1v1 setup plan (matchmaking + entity state
> sync, example "1v1 Matchmaking Arena") → session listed → sign-out returns 401 afterwards.

---

## ✅ Milestone 20 — Godot-Native SDK Polish

**Goal:** make the SDK feel like a native Godot addon — signals, an autoload client, clear
errors, robust reconnection, and built-in debugging — rather than a generic WebSocket client.

Delivered (GDScript SDK, bumped to **v0.2.0**; evolved the cohesive `MultiplayerService`
autoload in place rather than splitting into sub-client nodes, to keep the existing,
validated API stable):

- **Error model (20.4)** — structured errors `{ code, message, details, retryable }` on a new
  `sdk_error` signal (back-compat `error_received` retained); `rate_limited` is flagged
  retryable. Recent errors are kept for the debug report.
- **Reconnection (20.5)** — existing auto-reconnect + backoff, plus **heartbeat timeout
  detection** (`heartbeat_timeout`, default 45s — recovers half-open links), a new
  `reconnected` signal, re-auth via the stored token, and optional room/lobby re-join
  (`rejoin_last_room_on_reconnect`). `last_disconnect_reason` is exposed for debugging.
- **Debug mode (20.6)** — `set_debug_enabled(true)` logs connection, auth, room/lobby/
  matchmaking actions, messages, rejections, and disconnect reasons.
- **Debug report (20.7)** — `create_debug_report()` / `create_debug_report_string()` return a
  copyable snapshot: SDK + Godot versions, connection/player state, last errors, the **last
  50 protocol messages**, and ping/latency stats (app-level heartbeat pings feed `get_ping_ms()`).
- **Signals (20.3)** — consistent signal surface for every implemented feature. State-sync
  and persistence signals are intentionally deferred to their milestones (23 / later) so the
  SDK only advertises what the backend supports (honest scope).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| SDK installs as a Godot addon | ✅ `plugin.cfg` + `MultiplayerService` autoload |
| Connect, auth, join room, send + receive with minimal code | ✅ headless smoke test |
| SDK uses Godot signals consistently | ✅ incl. new `sdk_error`, `reconnected` |
| SDK exposes a debug mode | ✅ `set_debug_enabled` |
| SDK supports reconnect behavior | ✅ auto-reconnect, heartbeat timeout, re-join |
| SDK can generate a copyable debug report | ✅ `create_debug_report[_string]()` |
| `dotnet build` / `dotnet test` pass | ✅ (120 tests; backend unchanged) |

> Validated with **Godot 4.6.1 headless** against a live backend: all six headless tests
> pass — `smoke`, `reconnect`, `autoreconnect`, `lobby`, `matchmaking`, and the new
> `headless_debug` (triggers a `room_not_found`, asserts the structured `sdk_error`, and
> verifies the debug report contains the player id, recorded errors, and recent messages).

---

## ✅ Milestone 21 — Full Working Example Projects

**Goal:** complete Godot examples that demonstrate real use cases end-to-end — clone, paste a
public key, run two clients.

Delivered (in `sdk/godot-gdscript/examples/`, each a self-contained scene + script + README,
all runnable with two local clients):

- **Realtime Chat Room (21.1)** — connect, guest auth, create/join a room by code, send and
  receive room events (chat), live player roster.
- **Lobby + Ready Check (21.2)** — create/list/join lobbies, per-player ready toggle, host
  "Start Game" once everyone is ready, transition out of the lobby. Ready-state and start are
  relayed as room events inside the lobby; late joiners are re-announced to.
- **1v1 Matchmaking Arena (21.3)** — guest login, enter the queue, get matched into a
  generated room, exchange throttled movement events, end match → back to queue.
- **Example READMEs (21.6)** — each example documents what it teaches, required features,
  dashboard + Godot setup, how to run two clients, and common errors/troubleshooting; plus an
  [`examples/README.md`](../sdk/godot-gdscript/examples/README.md) index.
- **SDK ergonomics** — `send_event` now defaults `room_id` to `current_room_id`, so events
  "just work" inside the room/lobby you're in (explicit `room_id` still honored).

The dashboard onboarding already names the best example per game type (via
`SetupRecommendation`), and those names match these examples (Milestone 19.7).

**Deferred (honest scope):** the **State Sync Demo (21.4)** needed the state-sync backend, so
it shipped after Milestone 23 (see `examples/state_sync/`). The **Persistent Player Profile
(21.5)** needs player-scoped storage auth (today storage requires the project *secret* key,
which must not ship in a client) and is still deferred to a later persistence pass.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| At least 3 example projects are complete | ✅ chat, lobby+ready, matchmaking arena |
| Each example runs with two local clients | ✅ validated headless (2 SDK clients each) |
| Each example has a README | ✅ per-example + index |
| Each example links to relevant docs | ✅ protocol / tutorial / SDK README |
| Onboarding points to the correct example by game type | ✅ `SetupRecommendation` names match |
| `dotnet build` / `dotnet test` pass | ✅ (120 tests; backend unchanged) |

> Validated with **Godot 4.6.1 headless** against a live backend: all three example scenes
> load and build their UI with no script/runtime errors; the underlying flows pass as headless
> tests — `smoke` (rooms + events, chat's basis), `matchmaking` (arena's basis), and a new
> `headless_lobby_ready` proving room events relay between lobby members (ready → start). Full
> seven-test headless suite green.

---

## ✅ Milestone 22 — Multiplayer Debugger Dashboard

**Goal:** make multiplayer failures easy to understand — answer "why did this player fail to
connect, join, match, or sync?" from the dashboard.

Delivered:

- **Central recorder `RealtimeActivity`** — a thread-safe, bounded per-connection event
  timeline plus connection metadata, and a by-code error aggregator. Fed from two chokepoints:
  the connection handler (connected / authenticated / disconnected + oversized/rate-limit
  rejections) and the message dispatcher (every inbound action + malformed/unknown/handler
  rejections via `MessageContext.RespondErrorAsync`).
- **Connection metadata** — client IP (from the request) and the SDK + Godot engine versions,
  which the SDK now reports as `sdkVersion`/`engine` connect params.
- **Session inspector (22.2/22.3)** — `/dashboard/sessions/{id}`: full connection detail
  (player, project, IP, SDK/engine, auth status, current room, disconnect reason) and a
  chronological **timeline** (connected → authenticated → actions → rejections → disconnected).
- **Error explorer (22.8)** — `/dashboard/errors`: aggregated error codes with count, affected
  sessions, last occurrence/message, and an actionable **suggested fix** per code.
- **Room/lobby inspector (22.4/22.5)** — `/dashboard/rooms/{id}`: members, host, metadata,
  lobby attributes (name/visibility/max).
- **Matchmaking inspector (22.6)** — active queues (mode/region/size) and waiting counts.
- **Project overview (22.1)** — Overview adds active lobbies + matchmaking-queue stats; rooms,
  connections, and sessions link through to their inspectors.
- **Debug bundle viewer (22.9)** — `/dashboard/debug`: paste the SDK's
  `create_debug_report_string()` to render its connection state, recent messages, errors, and
  latency entirely client-side.
- **Admin API** — `GET /api/admin/{sessions/{id}, errors, matchmaking, rooms/{id}}`.

**Deferred (honest scope):** room/entity **state** and rejected **state** updates with
state-sync rates are part of Simple State Sync (Milestone 23); until then "rejected updates"
are surfaced via the error explorer + session timeline (which already show rejection reasons).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Dashboard shows active sessions, rooms, lobbies, matchmaking queues | ✅ Overview + Realtime |
| Developer can inspect a player session timeline | ✅ session inspector |
| Developer can inspect room state | ✅ room/lobby inspector (entity state → M23) |
| Developer can see rejected updates and reasons | ✅ timeline `rejected` + error explorer |
| Developer can see common errors and suggested fixes | ✅ error explorer |
| SDK debug report can be viewed in dashboard | ✅ debug bundle viewer |
| `dotnet build` / `dotnet test` pass | ✅ (126 tests) |

> Validated live with **Godot 4.6.1**: an SDK client connected (reporting `sdkVersion=0.2.0`,
> `engine=Godot 4.6.1-stable`) and triggered a `room_not_found`. The session inspector showed
> the full timeline (connected → authenticated → room.join → rejected → disconnected) with the
> IP and SDK/engine; the error explorer aggregated `room_not_found` with its suggested fix.
> Five new debugger integration tests + the headless suite are green.

---

## ✅ Milestone 23 — Simple State Sync v1

**Goal:** a room-scoped live state layer so connected players stay aligned right now, and late
joiners receive the current state instead of missing past events.

Delivered (in `Platform.Realtime/State`, on top of the in-memory rooms):

- **Room state (23.1)** — shared room-level key-value state, patched by the room host.
- **Entity state (23.2/23.3)** — per-entity key-value state owned by its creator; full `set`
  and partial `patch`.
- **Snapshot on join (23.4)** — joining a room/lobby with state sends a `state.snapshot` of the
  room state + all entities (+ owners). Empty rooms send nothing.
- **Ownership (23.5)** — only an entity's owner can update/delete it; only the host can patch
  room state (match rooms, which have no host, allow any member). Unauthorized → `state_forbidden`.
- **Deletion (23.6)** — `state.entity.delete` → broadcast `state.entity.deleted`.
- **Limits (23.7)** — `StateOptions` (Free-tier defaults): 50 entities/room, 4 KB/entity,
  16 KB/room, 10 updates/sec/client (token bucket). Violations → `state_limit_exceeded` /
  `state_too_large` / `rate_limited`.
- **Protocol (23.8)** — `state.room.patch/changed`, `state.entity.set/patch/changed/delete/
  deleted`, `state.snapshot`, `state.update.rejected`.
- **Dashboard inspector (23.9)** — the room inspector now shows live room state + the entity
  list (id, owner, state, last updated); rejected state updates appear in the error explorer
  (completing Milestone 22's deferred "rejected updates" item).
- **SDK** — `patch_room_state`, `set_entity_state`, `patch_entity_state`, `delete_entity_state`,
  plus `state_snapshot_received`, `room_state_changed`, `entity_state_changed`,
  `entity_state_deleted`, `state_update_rejected` signals. Updates are authoritative: the SDK
  applies on the broadcast, not optimistically.

**Known limitation (v1):** entities persist until the room expires (no auto-cleanup of a
disconnected player's entities yet); no prediction/rollback/interpolation — that's by design
(the plan scopes those out of v1).

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Room state can be patched and broadcast to members | ✅ |
| Entity state can be created, patched, and deleted | ✅ |
| Late joiners receive a full current snapshot | ✅ |
| Unauthorized entity updates are rejected | ✅ `state_forbidden` |
| Rate limits exist | ✅ per-client token bucket + size/count caps |
| Dashboard can inspect current state | ✅ room inspector state + entities |
| SDK exposes simple methods and signals | ✅ |
| `dotnet build` / `dotnet test` pass | ✅ (133 tests) |

> Validated with **Godot 4.6.1** against a live backend: host A seeded room state + an entity,
> B joined and received the snapshot (with the owner), A patched the entity and B saw the
> merged value, and B's attempt to patch a non-owned entity was rejected with `state_forbidden`
> — which then showed up in the dashboard error explorer. Six state-sync integration tests +
> the full eight-test headless suite are green.

---

## ✅ Milestone 24 — Godot Editor Plugin

**Goal:** set up and test SpawnWeaver from inside the Godot editor instead of jumping between
the dashboard, docs, and code.

Delivered (SDK bumped to **v0.3.0**; the plugin now adds an editor dock alongside the autoload):

- **Plugin install (24.1)** — enabling *SpawnWeaver Multiplayer Service* registers the
  `MultiplayerService` autoload **and** a dock in the editor's right panel.
- **Configuration panel (24.2)** — public key, server URL, environment, and a debug toggle,
  saved to `addons/multiplayer_service/spawnweaver.cfg` (a `ConfigFile`).
- **Test connection / guest login (24.2)** — an edit-time `WebSocketPeer` test that confirms the
  credentials work and shows the guest `playerId`, with results in a log pane.
- **Scene generator (24.3)** — generates a working **Room Chat**, **Lobby**, **Matchmaking**, or
  **State Sync Player** scene under `res://spawnweaver/`. Generated scenes auto-connect via the
  new SDK helper `connect_using_config()`, so there are no hard-coded keys.
- **Multi-client (24.4)** — clear in-dock instructions for *Debug → Run Multiple Instances*
  (generated scenes auto-connect as separate guests).
- **Log viewer (24.5)** — a pane showing the editor connection-test log.
- **Deep links (24.6)** — buttons that open the dashboard, the session debugger, and docs
  (derived from the configured server URL), plus the local examples folder.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Plugin can be enabled in Godot | ✅ autoload + dock |
| Configure project credentials inside Godot | ✅ dock + saved config |
| Test connection from the editor | ✅ edit-time WebSocket test |
| Generate at least one working example scene | ✅ four templates, all validated |
| Launch multiple local clients or clear instructions | ✅ in-dock instructions + auto-connect |
| Plugin links back to dashboard and docs | ✅ deep-link buttons |
| `dotnet build` / `dotnet test` pass | ✅ (133 tests; backend unchanged) |

> Validated with **Godot 4.6.1**: a headless **editor** import with the plugin enabled loaded
> the dock and templates with no script errors; then each of the four generated templates was
> run as a scene against a live backend — all compiled and auto-connected via the saved config
> (`connect_using_config()`). `_set` was renamed to `_status_text` in two templates after the
> run surfaced a clash with `Object._set`.

---

## ✅ Milestone 25 — Documentation & Tutorials v1

**Goal:** docs that make developers successful without needing support — browsable inside the
product.

Delivered as a **public, in-dashboard Docs section** (`/dashboard/docs`, logged-out accessible,
matching the dark theme) with a sticky sidebar — a `DocsPage` component wraps every page:

- **Getting started** — Overview, a Quickstart (account → project → SDK → connect → auth → join
  → first event), and Core concepts (incl. **persistence vs state sync**).
- **Six tutorials (25.1–25.6)** — Online in 10 minutes, Lobby + ready check, Add matchmaking,
  Simple state sync, Save a player profile, Debug a failed connection.
- **SDK reference** — install/config, key methods, the full signal list, the **error-code table**
  (with retryable flags), reconnection, and debug reports.
- **Limits & pricing** — the limits enforced today (message/state/storage caps) and a preview of
  the planned Free/Indie/Studio/Growth tiers (metering lands in Milestone 26).
- **Decision guides** — which model fits each game type, and an honest "not yet for" list.
- **Linked everywhere** — a **Docs** nav item (logged in and out), plus links from the landing
  page, the Help page, and the SDK README. The Docs section is exempted from the dashboard
  auth gate.

**Acceptance criteria**

| Criterion | Status |
|---|---|
| Complete getting-started path | ✅ Quickstart |
| SDK install + first connection | ✅ Quickstart + SDK reference |
| At least 5 tutorials | ✅ six tutorials |
| Error codes documented | ✅ SDK reference table |
| Persistence vs state sync explained | ✅ Core concepts + tutorials |
| What the product is not suitable for | ✅ Decision guides |
| Dashboard and SDK link to docs | ✅ nav + landing + Help + SDK README |
| `dotnet build` / `dotnet test` pass | ✅ (134 tests) |

> Validated live: the docs index, quickstart, SDK reference (showing `state_forbidden` /
> `rate_limited`), and decision guides all render **anonymously** (200) with the sidebar, the
> landing page links to them, while the rest of `/dashboard` still redirects logged-out visitors.

---

## ✅ Beta readiness — Passwordless (magic-link) sign-in

**Goal:** make signing up for the free beta one-click and low-friction, without creating an
abuse-magnet open sandbox — every user is still identified (by email) and rate-limitable.

Delivered:

- **Magic-link auth** — `POST /api/auth/magic/request` issues a single-use, 15-minute token
  (only its hash is stored; the raw token rides the emailed link); `GET /api/auth/magic?token=…`
  consumes it, **provisions the account + personal workspace on first use**, and signs in.
- **Passwordless accounts** — `AccountService.GetOrCreateByEmailAsync` creates users with an
  unusable random password hash, so password login stays disabled until they set one.
- **Pluggable email** — `IEmailSender` with a `DevEmailSender` (logs the link; the API returns a
  `devLink` in non-production so you can click it locally). Wire Postmark/Resend/SES for prod.
- **Abuse guards** — single-use tokens, short expiry, and per-email request throttling (one link
  per 30s); invalid emails return "sent" with no token (no account enumeration).
- **UI** — "Email me a sign-in/up link (no password)" on the sign-in and sign-up pages; the
  existing email+password flow stays as a fallback.
- **Migration** — `AddLoginTokens` (table `login_tokens`).

**Next (documented, not built):** "Sign in with GitHub" (OAuth) — the natural one-click option
for a dev audience; it just needs a registered GitHub OAuth app + client id/secret.

> Validated live: requesting a link returned a `devLink`; following it set the auth cookie,
> auto-created the user **and** workspace (`newdev's workspace`), and `GET /api/account` returned
> the signed-in profile. Three integration tests cover provisioning + single-use, throttling, and
> invalid tokens. 137 tests green.
