#!/usr/bin/env bash
# One-time SpawnWeaver VPS bootstrap. Run this ONCE on a fresh Ubuntu server (as root or with
# sudo). After this, GitHub Actions deploys on every push to main — you never touch the VPS again.
#
#   scp deploy/bootstrap.sh root@<server-ip>:~   &&   ssh root@<server-ip> 'bash bootstrap.sh'
#
# It installs Docker, opens the firewall, scaffolds deploy/.env, and creates a deploy SSH key for
# GitHub Actions (printing the private key once so you can paste it into the VPS_SSH_KEY secret).
set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/spawnweaver}"

echo "==> Installing Docker (if missing)…"
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh
fi

echo "==> Firewall: allow SSH + web…"
if command -v ufw >/dev/null 2>&1; then
  ufw allow 22/tcp || true
  ufw allow 80/tcp || true
  ufw allow 443/tcp || true
  ufw --force enable || true
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
echo " 2. Point your domain's DNS A record at this server's IP."
echo " 3. In your GitHub repo → Settings → Secrets and variables → Actions, add:"
echo "      VPS_HOST   = this server's IP or hostname"
echo "      VPS_USER   = $(whoami)"
echo "      VPS_SSH_KEY = the PRIVATE key printed below (whole block)"
echo "    (optional) VPS_PORT if SSH isn't on 22."
echo " 4. Push to main — GitHub Actions builds, pushes, and starts everything."
echo "================================================================"
echo
echo "----- BEGIN VPS_SSH_KEY (secret — copy everything between the lines) -----"
cat "$KEY"
echo "----- END VPS_SSH_KEY -----"
