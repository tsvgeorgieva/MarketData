#!/usr/bin/env bash
# Starts the whole system with docker compose and checks that the API reports itself alive.
# Usage: scripts/smoke-test.sh [--down]   (--down stops and removes the system afterwards)
set -euo pipefail

cd "$(dirname "$0")/.."
port="${API_PORT:-8080}"
url="http://localhost:${port}/health/live"

docker compose up -d --build --wait --wait-timeout 180 db
docker compose up -d --build api

for _ in $(seq 1 60); do
  if body="$(curl -fsS "$url" 2>/dev/null)"; then
    echo "OK: $url -> $body"
    [[ "${1:-}" == "--down" ]] && docker compose down -v
    exit 0
  fi
  sleep 1
done

echo "FAIL: $url did not answer within 60 seconds" >&2
docker compose logs --tail 50 api >&2
[[ "${1:-}" == "--down" ]] && docker compose down -v
exit 1
