#!/usr/bin/env bash
# SpawnWeaver Godot SDK installer (macOS / Linux).
#
# Run this from the ROOT of your Godot project:
#   curl -fsSL __BASE_URL__/install.sh | bash
#
# It downloads the SpawnWeaver addon and extracts it into ./addons/multiplayer_service.
# Requires: curl and unzip.
set -euo pipefail

BASE="__BASE_URL__"
ZIP_URL="$BASE/sdk/multiplayer_service.zip"
DEST="$(pwd)"

echo "Installing the SpawnWeaver Godot SDK into $DEST/addons ..."

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
curl -fsSL "$ZIP_URL" -o "$TMP/sdk.zip"
unzip -oq "$TMP/sdk.zip" -d "$DEST"

echo "Done."
echo "Next: open your project in Godot and enable 'SpawnWeaver Multiplayer Service'"
echo "      in Project -> Project Settings -> Plugins."
