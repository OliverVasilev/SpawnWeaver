#!/usr/bin/env bash
#
# SpawnWeaver one-command quickstart for macOS / Linux.
#
# Gets you from a fresh clone to "two players moving" with a single command. It:
#   1. Starts the SpawnWeaver API (via `dotnet run`, or --docker for Docker Compose).
#   2. Waits for /health to report ok.
#   3. Signs in (or signs up) a local developer account.
#   4. Creates a project and grabs its public key (pk_...).
#   5. Writes the Godot SDK config (spawnweaver.cfg) so the bundled examples
#      auto-connect — no copy/paste of keys required.
#   6. Leaves the server running until you press Ctrl+C.
#
# Re-running reuses the same account and project (idempotent). Generated
# credentials live in .quickstart/credentials.json (git-ignored) — local dev only.
#
# Requires: curl, and either the .NET SDK (default) or Docker (--docker).
# jq is used if present; a portable fallback is used otherwise.
#
# Usage:
#   ./quickstart.sh            # dotnet run on :5159
#   ./quickstart.sh --docker   # Docker Compose on :8080
#   ./quickstart.sh --no-serve # provision only, don't hold the server open
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CFG_PATH="$REPO_ROOT/sdk/godot-gdscript/addons/multiplayer_service/spawnweaver.cfg"
STATE_DIR="$REPO_ROOT/.quickstart"
CRED_PATH="$STATE_DIR/credentials.json"
COOKIE_JAR="$STATE_DIR/cookies.txt"

USE_DOCKER=0
NO_SERVE=0
PORT=0
for arg in "$@"; do
  case "$arg" in
    --docker)   USE_DOCKER=1 ;;
    --no-serve) NO_SERVE=1 ;;
    --port=*)   PORT="${arg#*=}" ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

if [ "$PORT" -le 0 ]; then
  if [ "$USE_DOCKER" -eq 1 ]; then PORT=8080; else PORT=5159; fi
fi
BASE_URL="http://localhost:$PORT"
WS_URL="ws://127.0.0.1:$PORT/connect"

cyan()  { printf '\033[36m==> %s\033[0m\n' "$1"; }
green() { printf '\033[32m    %s\033[0m\n' "$1"; }

SERVER_PID=""
DOCKER_STARTED=0

cleanup() {
  if [ -n "$SERVER_PID" ] && kill -0 "$SERVER_PID" 2>/dev/null; then
    printf '\nStopping server (PID %s)...\n' "$SERVER_PID"
    kill "$SERVER_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM

# Extract a string field from a small JSON blob without requiring jq.
json_field() {
  local field="$1" json="$2"
  if command -v jq >/dev/null 2>&1; then
    printf '%s' "$json" | jq -r ".$field // empty"
  else
    printf '%s' "$json" | sed -n "s/.*\"$field\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -n1
  fi
}

health_ok() {
  curl -fsS --max-time 3 "$BASE_URL/health" 2>/dev/null | grep -q '"status":"ok"'
}

mkdir -p "$STATE_DIR"

# --- 1. Start the server -----------------------------------------------------
if health_ok; then
  cyan "API already healthy at $BASE_URL — reusing it."
elif [ "$USE_DOCKER" -eq 1 ]; then
  cyan "Starting API with Docker Compose (this builds on first run)..."
  docker compose -f "$REPO_ROOT/deploy/docker-compose.yml" up --build -d
  DOCKER_STARTED=1
else
  cyan "Starting API with 'dotnet run' on port $PORT..."
  LOG_FILE="$STATE_DIR/server.log"
  dotnet run --project "$REPO_ROOT/src/Platform.Api" --urls "http://localhost:$PORT" >"$LOG_FILE" 2>&1 &
  SERVER_PID=$!
  green "Server PID $SERVER_PID — logs: $LOG_FILE"
fi

# --- 2. Wait for health ------------------------------------------------------
cyan "Waiting for $BASE_URL/health ..."
deadline=$(( $(date +%s) + 120 ))
until health_ok; do
  if [ "$(date +%s)" -gt "$deadline" ]; then echo "API did not become healthy within 120s." >&2; exit 1; fi
  if [ -n "$SERVER_PID" ] && ! kill -0 "$SERVER_PID" 2>/dev/null; then echo "Server exited early — see $LOG_FILE" >&2; exit 1; fi
  sleep 0.8
done
green "API is healthy."

# --- 3. Sign in or sign up a local dev account -------------------------------
if [ -f "$CRED_PATH" ]; then
  EMAIL="$(json_field email "$(cat "$CRED_PATH")")"
  PASSWORD="$(json_field password "$(cat "$CRED_PATH")")"
  PROJECT_ID="$(json_field projectId "$(cat "$CRED_PATH")")"
else
  SUFFIX="$(LC_ALL=C tr -dc 'a-f0-9' </dev/urandom | head -c 8)"
  EMAIL="dev-$SUFFIX@spawnweaver.local"
  PASSWORD="sw-$(LC_ALL=C tr -dc 'a-f0-9' </dev/urandom | head -c 24)"
  PROJECT_ID=""
fi

rm -f "$COOKIE_JAR"
cyan "Authenticating local developer account ($EMAIL)..."
if curl -fsS -c "$COOKIE_JAR" -X POST "$BASE_URL/api/auth/signin" \
     -H 'Content-Type: application/json' \
     -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" >/dev/null 2>&1; then
  green "Signed in."
else
  curl -fsS -c "$COOKIE_JAR" -X POST "$BASE_URL/api/auth/signup" \
     -H 'Content-Type: application/json' \
     -d "{\"email\":\"$EMAIL\",\"displayName\":\"Quickstart Dev\",\"password\":\"$PASSWORD\"}" >/dev/null
  green "Created account."
fi

# --- 4. Reuse or create a project --------------------------------------------
PUBLIC_KEY=""
if [ -n "$PROJECT_ID" ]; then
  RESP="$(curl -fsS -b "$COOKIE_JAR" "$BASE_URL/api/projects/$PROJECT_ID" 2>/dev/null || true)"
  PUBLIC_KEY="$(json_field publicKey "$RESP")"
  [ -n "$PUBLIC_KEY" ] && green "Reusing project $PROJECT_ID."
fi

if [ -z "$PUBLIC_KEY" ]; then
  cyan "Creating project 'Quickstart Game'..."
  RESP="$(curl -fsS -b "$COOKIE_JAR" -X POST "$BASE_URL/api/projects" \
     -H 'Content-Type: application/json' -d '{"name":"Quickstart Game"}')"
  PUBLIC_KEY="$(json_field publicKey "$RESP")"
  PROJECT_ID="$(json_field id "$RESP")"
  green "Project $PROJECT_ID created."
fi

cat >"$CRED_PATH" <<JSON
{
  "email": "$EMAIL",
  "password": "$PASSWORD",
  "projectId": "$PROJECT_ID",
  "publicKey": "$PUBLIC_KEY"
}
JSON

# --- 5. Write the Godot SDK config -------------------------------------------
cyan "Writing Godot SDK config..."
cat >"$CFG_PATH" <<CFG
[project]

public_key="$PUBLIC_KEY"
server_url="$WS_URL"
environment="Development"
debug_enabled=false
CFG
green "Wrote $CFG_PATH"

# --- Summary -----------------------------------------------------------------
echo
green "SpawnWeaver is ready."
echo "  API:        $BASE_URL"
echo "  WebSocket:  $WS_URL"
echo "  Public key: $PUBLIC_KEY"
echo "  Dashboard:  $BASE_URL/dashboard"
echo
cyan "Next:"
echo "  1. Open  sdk/godot-gdscript  in Godot 4.3+"
echo "  2. Debug -> Run Multiple Instances -> 2 instances, then press Play"
echo "  3. Both windows are pre-configured — just click Connect."
echo

if [ "$NO_SERVE" -eq 1 ]; then
  green "Provisioning done (--no-serve). Server left as-is."
  trap - EXIT INT TERM
  exit 0
fi

if [ "$DOCKER_STARTED" -eq 1 ]; then
  printf '\033[90mServer runs in Docker. Stop it with:\n  docker compose -f deploy/docker-compose.yml down\033[0m\n'
  trap - EXIT INT TERM
  exit 0
fi

if [ -n "$SERVER_PID" ]; then
  printf '\033[90mServer is running. Press Ctrl+C to stop it.\033[0m\n'
  wait "$SERVER_PID"
fi
