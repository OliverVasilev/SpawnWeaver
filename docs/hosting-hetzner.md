# Hosting SpawnWeaver on a Hetzner VPS

This deploys the whole platform — API, PostgreSQL, and a Caddy reverse proxy with **automatic
HTTPS** — to a single small VPS with Docker Compose. Caddy terminates TLS and forwards both
HTTP and WebSocket traffic to the API, so `wss://` "just works".

> Placeholder domain: this guide uses **`spawnweaver.example`**. Replace it with your real
> domain everywhere (and in `deploy/.env`).

> **Want automatic deploys?** Skip the manual `docker compose` steps and use the
> [CI/CD pipeline](#automatic-deploys-cicd) — after a one-time bootstrap, every push to `main`
> builds and deploys to the VPS for you.

## 1. Create the server

- Hetzner Cloud → **Add Server**. A **CX22** (2 vCPU / 4 GB) is plenty to start.
- Image: **Ubuntu 24.04**. Add your SSH key.
- After it boots, note the public IPv4.

## 2. Point DNS at it

Create an **A record** for your domain → the server's IPv4:

```
spawnweaver.example.  A  <server-ip>
```

(Optionally also `www`.) Wait until `ping spawnweaver.example` resolves to the IP — Caddy
needs working DNS to issue the certificate.

## 3. Install Docker

SSH in (`ssh root@<server-ip>`) and install Docker Engine + the Compose plugin:

```bash
curl -fsSL https://get.docker.com | sh
```

Lock down the firewall to SSH + web:

```bash
ufw allow 22/tcp && ufw allow 80/tcp && ufw allow 443/tcp && ufw --force enable
```

## 4. Get the code and configure

```bash
git clone <your-repo-url> spawnweaver && cd spawnweaver
cp deploy/.env.example deploy/.env
nano deploy/.env        # set DOMAIN, ACME_EMAIL, POSTGRES_PASSWORD, Resend keys
```

`deploy/.env` is git-ignored — keep your secrets there. See **Email** below for Resend.

## 5. Launch

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.prod.yml up -d --build
```

Caddy will obtain a Let's Encrypt certificate within a few seconds. Verify:

```bash
curl https://spawnweaver.example/health
# {"status":"ok","service":"Platform.Api","version":"…"}
```

Open `https://spawnweaver.example/dashboard` to sign up. Players connect at
`wss://spawnweaver.example/connect`, and developers install the SDK with
`iwr https://spawnweaver.example/install.ps1 -UseBasicParsing | iex`.

## Email (Resend)

The production stack enforces **email verification before sign-in**, which needs a real email
provider:

1. Create a [Resend](https://resend.com) account and **API key**.
2. **Verify your sending domain** in Resend, then set in `deploy/.env`:
   ```
   EMAIL__RESEND__APIKEY=re_xxxxxxxx
   EMAIL__FROMADDRESS=noreply@spawnweaver.example
   ```
3. Re-deploy: `docker compose --env-file deploy/.env -f deploy/docker-compose.prod.yml up -d`.

Before your domain is verified you can leave `EMAIL__RESEND__APIKEY` **empty** — the API falls
back to the dev/logging sender, which **auto-verifies** accounts so you can still test the rest
of the platform. (Resend's `onboarding@resend.dev` only delivers to your own Resend account
email, so it's not suitable for real users.)

## Operating it

- **Update to a new version:**
  ```bash
  git pull
  docker compose --env-file deploy/.env -f deploy/docker-compose.prod.yml up -d --build
  ```
- **Logs:** `docker compose -f deploy/docker-compose.prod.yml logs -f api`
- **Back up the database** (volume `spawnweaver-pgdata`):
  ```bash
  docker exec spawnweaver-db pg_dump -U spawnweaver spawnweaver > backup-$(date +%F).sql
  ```
- **Stop:** `docker compose -f deploy/docker-compose.prod.yml down` (add `-v` to also delete
  data — destructive).

## Automatic deploys (CI/CD)

Instead of building on the VPS by hand, let **GitHub Actions** do it: it runs the tests, builds a
Docker image, pushes it to the GitHub Container Registry (GHCR), then SSHes to your VPS and pulls +
restarts. The VPS only ever needs Docker — never the source or .NET SDK. Workflow lives at
[`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml).

**One-time setup**

1. **Bootstrap the server** (the only time you touch it). Copy and run the script:
   ```bash
   scp deploy/bootstrap.sh root@<server-ip>:~
   ssh root@<server-ip> 'bash bootstrap.sh'
   ```
   It installs Docker, opens the firewall, scaffolds `~/spawnweaver/deploy/.env`, creates a deploy
   SSH key, and **prints a private key**. Then edit the env file (`nano ~/spawnweaver/deploy/.env`)
   and set a real DNS `A` record.

2. **Add GitHub repo secrets** (Settings → Secrets and variables → Actions):

   | Secret | Value |
   |---|---|
   | `VPS_HOST` | server IP / hostname |
   | `VPS_USER` | the user you bootstrapped as (e.g. `root`) |
   | `VPS_SSH_KEY` | the private key the bootstrap script printed |
   | `VPS_PORT` | *(optional)* SSH port if not `22` |

   No registry secret is needed — Actions uses the built-in `GITHUB_TOKEN` to push/pull from GHCR.

3. **Push to `main`.** That's it — the pipeline builds and deploys. Trigger it anytime from the
   Actions tab (**Run workflow**) too. The compose file and `Caddyfile` are copied up on each run,
   so infra changes ship automatically; your secrets in `deploy/.env` are never touched.

> The GHCR image defaults to private. The deploy pulls it on the VPS using `GITHUB_TOKEN`, which
> works during the run. If you'd rather not log in on the VPS, make the package public in your
> GitHub **Packages** settings.

## Notes

- The schema is created from the EF model on first start (Postgres uses `EnsureCreated`).
- Keep `deploy/.env` off version control; rotate the Resend key if it leaks.
- For a cheaper, single-container playtest (SQLite, no domain) see
  [infrastructure.md](./infrastructure.md); for general alpha hardening see
  [alpha-hosting.md](./alpha-hosting.md).
