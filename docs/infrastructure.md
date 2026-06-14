# Infrastructure & Playtest Guide

SpawnWeaver is a modular monolith that runs as a **single container**. This guide covers
running it for local development and for near-free closed playtests with real players.

## Modes

| Mode | What runs | Database | Use |
|---|---|---|---|
| **A — Local dev** | `dotnet run` or `docker-compose.yml` | SQLite | development |
| **B — Playtest (near-free)** | one container (`docker-compose.test.yml`) | SQLite (persisted volume) | closed testing with friends |
| **C — Postgres** | one container + Postgres (`docker-compose.postgres.yml`) | PostgreSQL | when SQLite is outgrown |

The recommended first test setup is **Mode B**: one .NET container, a SQLite file, and one
public WSS endpoint — no managed database, Redis, or Kubernetes.

## Run a playtest (Mode B)

```bash
# From the repo root:
docker compose -f deploy/docker-compose.test.yml up --build -d
curl http://localhost:8080/health          # { "status": "ok", ... }
docker compose -f deploy/docker-compose.test.yml logs -f api
```

The SQLite database is stored in the `spawnweaver-data` volume, so it survives restarts.
Change the published port with `PORT=9000 docker compose ... up`.

### Let external players connect

The backend listens on plain HTTP/WS inside the container. For external testers you need a
public `wss://` endpoint. Two cheap options:

- **Cloudflare Tunnel** (no public IP needed):
  `cloudflared tunnel --url http://localhost:8080` → gives a `https://<name>.trycloudflare.com`
  URL. Players connect the Godot SDK to `wss://<name>.trycloudflare.com/connect`.
- **A small VM** (e.g. a $5 instance) running the same compose file behind a TLS reverse
  proxy (Caddy/nginx), if you want a stable hostname.

### Invite a player

1. Create a project and copy its public key:
   ```bash
   curl -X POST http://localhost:8080/api/projects -H "Content-Type: application/json" -d '{"name":"Playtest"}'
   ```
2. Share the public key (`pk_...`) and the `wss://.../connect` URL.
3. In the Godot sample, paste both, connect, and create/join a room by code.

### Watch what's happening

- **Health:** `GET /health`
- **Live counts:** `GET /connect/stats` → `{ "activeConnections": N, "activeRooms": M }`
- **Logs:** connection lifecycle (`Realtime connected/disconnected`) and room lifecycle
  (`Room created`, `Player … joined/left`, `Room expired`) are logged at Information level.

## Configuration (environment variables)

ASP.NET Core binds environment variables using `__` for nested keys:

| Variable | Default | Meaning |
|---|---|---|
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address inside the container |
| `Database__Provider` | `sqlite` | `sqlite` or `postgres` |
| `ConnectionStrings__Default` | `Data Source=spawnweaver.db` | DB connection string |
| `Realtime__MaxMessageBytes` | `16384` | Max inbound message size |
| `Realtime__MaxMessagesPerSecond` | `20` | Sustained per-connection message rate |
| `Realtime__MessageBurst` | `40` | Rate-limiter burst capacity |
| `Realtime__EmptyRoomTtl` | `00:01:00` | How long empty rooms live before expiry |
| `Auth__TokenSecret` | _(random per process)_ | HMAC secret for player tokens. **Set this** so tokens survive restarts. |
| `Auth__TokenLifetime` | `7.00:00:00` | Player/reconnect token lifetime |
| `Realtime__MatchmakingTimeout` | `00:00:30` | How long a player waits in the queue before timing out |
| `Storage__MaxValueBytes` | `65536` | Max size of a stored value |
| `Storage__MaxKeyLength` | `128` | Max storage key length |
| `Storage__MaxKeysPerPlayer` | `100` | Max distinct keys per player |
| `Admin__ApiKey` | _(unset = open)_ | If set, the admin API + dashboard require this bearer token |
| `Realtime__MaxConnectionsPerProject` | `0` (unlimited) | Max concurrent connections per project |
| `Security__AllowedOrigins` | _(unset = any)_ | Comma-separated Origin allowlist for `/connect` |

## PostgreSQL mode (Mode C)

```bash
POSTGRES_PASSWORD=change-me docker compose -f deploy/docker-compose.postgres.yml up --build -d
```

In Postgres mode the schema is created from the EF model on startup (`EnsureCreated`).
Provider-specific EF migrations can be added later for controlled schema upgrades.

## Load testing

A simple WebSocket load generator lives in `tools/Platform.LoadTest`. With the backend
running, it creates a project, connects N clients, groups them into rooms, and relays game
events for a fixed duration:

```bash
dotnet run --project tools/Platform.LoadTest -- \
  --api http://localhost:8080 --clients 40 --room-size 4 --seconds 15 --rate 10
```

It prints connections established, events sent/received, and per-second throughput — use it
to find the max stable connections/throughput on a given machine.
