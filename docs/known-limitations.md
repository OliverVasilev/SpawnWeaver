# Known Limitations (Alpha)

SpawnWeaver is an early alpha. It is good enough for closed playtests, not for production
games. Current constraints:

## Scale & infrastructure

- **Single node only.** Rooms, lobbies, connections, and matchmaking queues live in one
  process's memory. There is no cross-node coordination yet (no Redis/Valkey). Don't run
  more than one instance behind a load balancer — players on different instances can't see
  each other.
- **In-memory realtime state.** A restart drops all active rooms/connections (players
  reconnect automatically, but room membership is lost). Rooms are not persisted.
- **SQLite by default.** Fine for playtests; for more durability use the PostgreSQL mode.
  Postgres schema is created from the model (no migration history in that mode yet).
- **Baseline capacity:** comfortably hundreds of concurrent connections and thousands of
  messages/sec on one machine (see [performance.md](./performance.md)). Not yet tuned for
  more.

## Protocol & features

- **JSON protocol.** Human-readable and fast enough at this scale; a binary protocol may
  come later if benchmarks demand it.
- **Broadcast sends are sequential** per recipient (no per-connection outbound queue yet),
  so one very slow client can delay a broadcast. Backpressure/bounded channels are the
  planned next step.
- **No dedicated game servers / authoritative simulation.** Events are relayed between
  clients; the server does not run game logic.
- **Matchmaking is basic:** exact-match on project + game mode + region + size, FIFO. No
  skill rating, parties, or backfill.
- **Tokens are invalidated on restart** unless `Auth__TokenSecret` is set to a stable value.

## Security & operations

- **Admin API and dashboard are open by default.** Set `Admin__ApiKey` and keep the
  dashboard behind your tunnel/proxy before exposing anything publicly.
- **Feedback endpoint is unauthenticated** (length-limited only) — expect some spam.
- Rate limits, message-size limits, and per-project connection limits exist but defaults are
  generous; tune `Realtime__*` for your deployment.

## Reporting issues

Use the feedback form on the landing page or `POST /api/feedback`. Include what you did,
what you expected, and what happened.
