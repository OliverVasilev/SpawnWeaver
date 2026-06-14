# Performance Baseline (v1)

Milestone 15. Measured first, then applied one obvious fix. JSON is kept (it is fast
enough — see below).

**Machine:** 11th Gen Intel Core i7-11850H @ 2.50GHz (8 physical / 16 logical), 16 GB,
Windows 11, .NET SDK 9.0.312, single process (SQLite).

## Micro-benchmarks (BenchmarkDotNet, ShortRun)

Run with: `dotnet run -c Release --project tools/Platform.Benchmarks -- --filter *`

### Per-message envelope cost

| Method            | Mean    | Allocated |
|-------------------|--------:|----------:|
| `ParseEnvelope`   | 727 ns  | 512 B     |
| `SerializeOutbound` | 486 ns | 504 B     |

A full inbound parse + outbound serialize is ~1.2 µs and ~1 KB. **JSON is not the
bottleneck** at the target scale — a single core could (de)serialize ~800k messages/s.
Per the plan, we keep JSON until a benchmark proves it insufficient.

### Room broadcast: serialize-per-member vs serialize-once

| Members | Per-member (old) | Once (new) | Time ratio | Alloc (old → new) |
|--------:|-----------------:|-----------:|-----------:|-------------------|
| 2       | 1,003 ns         | 500 ns     | 0.50×      | 1,008 B → 504 B   |
| 8       | 3,743 ns         | 492 ns     | 0.13×      | 4,032 B → 504 B   |
| 32      | 15,634 ns        | 498 ns     | 0.03×      | 16,128 B → 504 B  |

**Applied fix:** broadcasts now serialize the envelope **once** and reuse the bytes for all
recipients (`RoomBroadcast.SendToMembersAsync` + `RealtimeMessageSender.Serialize`/
`SendRawAsync`). Serialization cost/allocations are now **constant** per broadcast instead
of linear in room size — a 32× allocation reduction for a 32-player room. This is the
hot path for `game.event` relay.

## Load test (macro)

Run with: `dotnet run -c Release --project tools/Platform.LoadTest -- --api <url> --clients N --room-size 4 --seconds 10 --rate 10`

| Clients | Rooms | Connected | Errors | Events sent (ingress) | Events received (egress) |
|--------:|------:|----------:|-------:|----------------------:|-------------------------:|
| 50      | ~13   | 50/50     | 0      | 450/s                 | 1,314/s                  |
| 100     | ~25   | 100/100   | 0      | 900/s                 | 2,699/s                  |
| 200     | ~50   | 200/200   | 0      | 1,798/s               | 5,395/s                  |

Egress ≈ 3× ingress (rooms of 4 → each event relayed to 3 others), exactly as expected.
Throughput scaled **linearly** from 50→200 clients with **zero errors**.

### Max stable connections (local)

**At least 200 concurrent WebSocket connections** across ~50 active rooms, sustained for
10s at 10 events/s/client with 0 errors. The ceiling here was the load generator on the
same machine, not the server — the server showed no errors or saturation. The first MVP
target ("dozens of rooms, hundreds of players") is comfortably met on one machine.

### Messages per second

At 200 clients: ~1,800 inbound messages/s, ~5,400 outbound relays/s. Aggregate across the
sweep: 31,850 messages processed, 350 connections, **0 errors** (`/api/admin/metrics`).

## Clear next bottleneck

The realtime hot path is correct and cheap per message; the next limits, in order:

1. **Sequential per-recipient sends in a broadcast.** `SendToMembersAsync` awaits each
   socket send in turn. For very large rooms or slow clients, one slow socket delays the
   rest (head-of-line blocking). *Next step:* give each connection a bounded outbound
   channel + a dedicated writer (the plan's "bounded channels / backpressure"), and/or fan
   out sends concurrently. This also enables dropping/disconnecting slow consumers instead
   of stalling a broadcast.
2. **Single-process, single-node memory.** Rooms/connections live in one process. Scaling
   past one node needs Redis/Valkey pub-sub for cross-node broadcast and presence (planned
   "later milestone", only when one node is no longer enough).
3. **No write backpressure yet.** A fast producer to a slow consumer grows the OS send
   buffer. The bounded outbound channel above is the mitigation.

JSON serialization, envelope parsing, and room bookkeeping are **not** current bottlenecks
and need no further optimization at this scale.
