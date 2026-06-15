# Hosting SpawnWeaver on AWS Lightsail

This deploys the whole platform — API, PostgreSQL, and a Caddy reverse proxy with **automatic
HTTPS** — to a single Lightsail instance with Docker Compose. Caddy terminates TLS and forwards
both HTTP and WebSocket traffic to the API, so `wss://` "just works".

> This guide uses **`spawnweaver.dev`** — the project's production domain. If you're self-hosting
> a fork, substitute your own domain everywhere (and in `deploy/.env`).

> **Want automatic deploys?** Skip the manual `docker compose` steps and use the
> [CI/CD pipeline](#automatic-deploys-cicd) — after a one-time bootstrap, every push to `main`
> builds and deploys to the instance for you, one click from the GitHub Actions tab.

## 1. Create the instance

In the [Lightsail console](https://lightsail.aws.amazon.com) → **Create instance**:

- **Platform:** Linux/Unix. **Blueprint:** OS Only → **Ubuntu 24.04 LTS**.
- **Plan:** the **$10/mo** tier (2 GB RAM) is a comfortable start; $5/mo (1 GB) works for light
  playtests but Postgres + the API are tight on it.
- Name it (e.g. `spawnweaver`) and **Create**. Lightsail generates an SSH key — download the
  `.pem` if you haven't already (Account → SSH keys).

## 2. Give it a static IP

By default a Lightsail instance gets a new public IP every reboot — useless for DNS. Fix it:

- Lightsail → **Networking** → **Create static IP** → attach it to your instance.
- Note that static IPv4. It's free while attached to a running instance.

## 3. Open the Lightsail firewall

Lightsail has its **own** firewall in the console, separate from the box. Instance → **Networking**
→ **IPv4 Firewall** → add:

| Application | Protocol | Port |
|---|---|---|
| HTTP | TCP | 80 |
| HTTPS | TCP | 443 |

SSH (22) is already there. **Without 80 + 443 here, Caddy can never fetch a certificate** — this
is the #1 Lightsail gotcha.

## 4. Point DNS at it

Create an **A record** for your domain → the **static IP** from step 2:

```
spawnweaver.dev.  A  <static-ip>
```

(Optionally also `www`.) Wait until `ping spawnweaver.dev` resolves to the IP — Caddy needs
working DNS to issue the certificate.

## 5. Install Docker

SSH in as the default **`ubuntu`** user (not root) using your Lightsail key:

```bash
ssh -i LightsailKey.pem ubuntu@<static-ip>
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu          # run docker without sudo (re-login to apply)
```

The on-host `ufw` firewall is inactive by default on Lightsail; the console firewall (step 3) is
what's enforced. If you do enable `ufw`, also allow 22/80/443.

## 6. Get the code and configure

```bash
git clone <your-repo-url> spawnweaver && cd spawnweaver
cp deploy/.env.example deploy/.env
nano deploy/.env        # set DOMAIN, ACME_EMAIL, POSTGRES_PASSWORD, Resend keys
```

`deploy/.env` is git-ignored — keep your secrets there. See **Email** below for Resend.

## 7. Launch

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.prod.yml up -d --build
```

Caddy will obtain a Let's Encrypt certificate within a few seconds. Verify:

```bash
curl https://spawnweaver.dev/health
# {"status":"ok","service":"Platform.Api","version":"…"}
```

Open `https://spawnweaver.dev/dashboard` to sign up. Players connect at
`wss://spawnweaver.dev/connect`, and developers install the SDK with
`iwr https://spawnweaver.dev/install.ps1 -UseBasicParsing | iex`.

## Email (Resend)

The production stack enforces **email verification before sign-in**, which needs a real email
provider:

1. Create a [Resend](https://resend.com) account and **API key**.
2. **Verify your sending domain** in Resend, then set in `deploy/.env`:
   ```
   EMAIL__RESEND__APIKEY=re_xxxxxxxx
   EMAIL__FROMADDRESS=noreply@spawnweaver.dev
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
  For off-box backups, Lightsail can also take automatic **instance snapshots**
  (Snapshots tab) — handy for whole-disk restore.
- **Stop:** `docker compose -f deploy/docker-compose.prod.yml down` (add `-v` to also delete
  data — destructive).

## Automatic deploys (CI/CD)

Instead of building on the instance by hand, let **GitHub Actions** do it: it runs the tests, builds
a Docker image, pushes it to the GitHub Container Registry (GHCR), then SSHes to your Lightsail
instance and pulls + restarts. The instance only ever needs Docker — never the source or .NET SDK.
Workflow lives at [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml).

**One-time setup**

1. **Do steps 1–4 above** (instance, static IP, Lightsail firewall, DNS).

2. **Bootstrap the instance** (the only time you touch it). Copy and run the script:
   ```bash
   scp -i LightsailKey.pem deploy/bootstrap.sh ubuntu@<static-ip>:~
   ssh -i LightsailKey.pem ubuntu@<static-ip> 'bash bootstrap.sh'
   ```
   It installs Docker, lets `ubuntu` run Docker without sudo, scaffolds
   `~/spawnweaver/deploy/.env`, creates a deploy SSH key, and **prints a private key**. Then edit
   the env file (`nano ~/spawnweaver/deploy/.env`).

3. **Add GitHub repo secrets** (Settings → Secrets and variables → Actions):

   | Secret | Value |
   |---|---|
   | `VPS_HOST` | the instance's **static IP** |
   | `VPS_USER` | `ubuntu` (the Lightsail login user) |
   | `VPS_SSH_KEY` | the private key the bootstrap script printed |
   | `VPS_PORT` | *(optional)* SSH port if not `22` |

   No registry secret is needed — Actions uses the built-in `GITHUB_TOKEN` to push/pull from GHCR.

4. **Push to `main`.** That's it — the pipeline builds and deploys. Trigger it anytime from the
   Actions tab (**Run workflow**) too. The compose file and `Caddyfile` are copied up on each run,
   so infra changes ship automatically; your secrets in `deploy/.env` are never touched.

> The GHCR image defaults to private. The deploy pulls it on the instance using `GITHUB_TOKEN`,
> which works during the run. If you'd rather not log in on the box, make the package public in
> your GitHub **Packages** settings.

## Notes

- The schema is created from the EF model on first start (Postgres uses `EnsureCreated`).
- Keep `deploy/.env` off version control; rotate the Resend key if it leaks.
- For a cheaper, single-container playtest (SQLite, no domain) see
  [infrastructure.md](./infrastructure.md); for general alpha hardening see
  [alpha-hosting.md](./alpha-hosting.md).
