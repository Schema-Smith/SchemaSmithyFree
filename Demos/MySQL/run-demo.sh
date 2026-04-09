#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/../.."

if [ ! -f "$REPO_ROOT/SchemaQuench/publish/SchemaQuench" ]; then
    echo "SchemaQuench not built yet, building..."
    "$REPO_ROOT/build-schemaquench.sh"
fi

echo "Starting MySQL demo..."
cd "$SCRIPT_DIR"
docker compose up --build -d
docker compose wait completed
