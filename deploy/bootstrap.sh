#!/usr/bin/env bash
# One-time SpawnWeaver bootstrap for an AWS Lightsail instance. Run this ONCE on a fresh
# Ubuntu 24.04 Lightsail instance (the default login user is `ubuntu`, with passwordless sudo).
# After this, GitHub Actions deploys on every push to main — you never touch the box again.
#
#   scp -i LightsailKey.pem deploy/bootstrap.sh ubuntu@<static-ip>:~
#   ssh -i LightsailKey.pem ubuntu@<static-ip> 'bash bootstrap.sh'
#
# It installs Docker, lets the login user run Docker without sudo, opens the on-host firewall,
# scaffolds deploy/.env, and creates a dedicated deploy SSH key for GitHub Actions (printing the
# private key once so you can paste it into the VPS_SSH_KEY secret).
#
# NOTE: Lightsail has a SECOND firewall in its web console (Networking tab). You must also open
# ports 80 (HTTP) and 443 (HTTPS) there — see the "IPv4 Firewall" section — or TLS will never
# issue. SSH (22) is open by default.
set -euo pipefail

# Use sudo only when we aren't already root (Lightsail logs you in as `ubuntu`).
SUDO=""
if [ "$(id -u)" -ne 0 ]; then SUDO="sudo"; fi

APP_DIR="${APP_DIR:-$HOME/spawnweaver}"
LOGIN_USER="$(whoami)"

echo "==> Installing Docker (if missing)…"
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | $SUDO sh
fi

echo "==> Allowing '$LOGIN_USER' to run Docker without sudo…"
# So the GitHub Actions deploy can run `docker compose` as this user over SSH.
$SUDO groupadd -f docker || true
$SUDO usermod -aG docker "$LOGIN_USER" || true

echo "==> On-host firewall (ufw): allow SSH + web…"
if command -v ufw >/dev/null 2>&1; then
  $SUDO ufw allow 22/tcp  || true
  $SUDO ufw allow 80/tcp  || true
  $SUDO ufw allow 443/tcp || true
  $SUDO ufw --force enable || true
fi

echo "==> Creating $APP_DIR/deploy…"
mkdir -p "$APP_DIR/deploy"

ENV_FILE="$APP_DIR/deploy/.env"
if [ ! -f "$ENV_FILE" ]; then
  cat > "$ENV_FILE" <<'ENV'
# SpawnWeaver production secrets — EDIT THESE before your first deploy.
DOMAIN=spawnweaver.example
ACME_EMAIL=you@spawnweaver.example
POSTGRES_PASSWORD=change-me-to-a-long-random-string
# Resend (leave the key blank to disable real email — accounts then auto-verify):
EMAIL__RESEND__APIKEY=
EMAIL__FROMADDRESS=onboarding@resend.dev
EMAIL__FROMNAME=SpawnWeaver
ENV
  chmod 600 "$ENV_FILE"
  echo "    Wrote $ENV_FILE — EDIT IT (DOMAIN, ACME_EMAIL, POSTGRES_PASSWORD, Resend)."
else
  echo "    $ENV_FILE already exists — leaving it untouched."
fi

echo "==> Creating a deploy SSH key for GitHub Actions…"
mkdir -p "$HOME/.ssh"; chmod 700 "$HOME/.ssh"
KEY="$HOME/.ssh/spawnweaver_deploy"
if [ ! -f "$KEY" ]; then
  ssh-keygen -t ed25519 -N "" -C "spawnweaver-deploy" -f "$KEY"
  cat "$KEY.pub" >> "$HOME/.ssh/authorized_keys"
  chmod 600 "$HOME/.ssh/authorized_keys"
fi

echo
echo "================================================================"
echo " 1. Edit your secrets:   nano $ENV_FILE"
echo " 2. Lightsail console → Networking → attach a STATIC IP, then point"
echo "    your domain's DNS A record at that static IP."
echo " 3. Lightsail console → Networking → IPv4 Firewall: add rules for"
echo "    HTTP (80) and HTTPS (443). (SSH/22 is already there.)"
echo " 4. In your GitHub repo → Settings → Secrets and variables → Actions, add:"
echo "      VPS_HOST    = this instance's STATIC IP"
echo "      VPS_USER    = $LOGIN_USER"
echo "      VPS_SSH_KEY = the PRIVATE key printed below (whole block)"
echo "    (optional) VPS_PORT if SSH isn't on 22."
echo " 5. Push to main — GitHub Actions builds, pushes, and starts everything."
echo "================================================================"
echo
echo "----- BEGIN VPS_SSH_KEY (secret — copy everything between the lines) -----"
cat "$KEY"
echo "----- END VPS_SSH_KEY -----"
