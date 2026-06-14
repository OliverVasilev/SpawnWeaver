# Hosting the Alpha (Operator Guide)

How to stand up a SpawnWeaver alpha that external testers can reach, and how to monitor it.
This builds on the [infrastructure guide](./infrastructure.md).

## 1. Deploy a single container

```bash
# On a small VM (or locally + a tunnel):
docker compose -f deploy/docker-compose.test.yml up --build -d
curl http://localhost:8080/health
```

## 2. Lock down config (do this before exposing it)

Set these environment variables (e.g. in a `.env` next to the compose file):

| Variable | Why |
|---|---|
| `Auth__TokenSecret` | A long random secret so player tokens survive restarts. |
| `Admin__ApiKey` | Require a bearer token for the admin API + dashboard. |
| `Realtime__MaxConnectionsPerProject` | Cap connections per project (e.g. `200`). |
| `Security__AllowedOrigins` | If browser clients connect, restrict origins. |

Keep the **dashboard** (`/dashboard`) and **admin API** (`/api/admin/*`) behind your
proxy/tunnel even with `Admin__ApiKey` set.

## 3. Give testers a public WSS endpoint

External clients need TLS (`wss://`). Cheapest options:

- **Cloudflare Tunnel:** `cloudflared tunnel --url http://localhost:8080` →
  `https://<name>.trycloudflare.com`. Testers connect to `wss://<name>.trycloudflare.com/connect`.
- **Small VM + TLS reverse proxy** (Caddy/nginx) for a stable hostname.

## 4. Onboard a tester

Point them at the landing page (`/`) and the [onboarding guide](./onboarding.md). They can
self-serve: `POST /api/projects` to get a key, download the SDK addon, and connect a Godot
sample. (For a tighter alpha, create the projects yourself and hand out the public keys.)

## 5. Monitor

- **Dashboard** (`/dashboard`): live projects, active rooms/connections, recent sessions,
  logs (filter Errors/Warnings to spot abuse), and metric totals.
- **Admin API:** `/api/admin/realtime`, `/api/admin/sessions`, `/api/admin/metrics`,
  `/api/admin/logs?level=Warning`, and `/api/admin/feedback`.
- **Metrics:** the OpenTelemetry `Meter` "SpawnWeaver" (console exporter by default; point
  it at your OTLP collector via the standard `OTEL_*` env vars for dashboards/alerts).

## 6. Collect feedback

Feedback submitted via the landing form or `POST /api/feedback` is stored durably; read it
at `GET /api/admin/feedback` (admin-gated).

## Checklist

- [ ] `Auth__TokenSecret` set (stable)
- [ ] `Admin__ApiKey` set; dashboard not publicly reachable
- [ ] `Realtime__MaxConnectionsPerProject` set
- [ ] `wss://` endpoint working (tester connected end-to-end)
- [ ] Dashboard shows the tester's session
- [ ] Test feedback submission lands in `/api/admin/feedback`
