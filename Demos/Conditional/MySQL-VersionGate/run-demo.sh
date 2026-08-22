#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/../../.."

echo "Publishing SchemaQuench..."
"$REPO_ROOT/build-schemaquench.sh"

echo "Starting MySQL conditional-deployment demo..."
cd "$SCRIPT_DIR"

docker compose up --build --abort-on-container-exit verify
