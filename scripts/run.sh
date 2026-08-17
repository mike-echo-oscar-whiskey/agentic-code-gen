#!/usr/bin/env bash
# Purpose: start the backend (http://localhost:5117) and frontend (http://localhost:4200)
#          in the background, with logs and pidfiles under .run/
# Usage:   scripts/run.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_DIR="$ROOT/.run"
mkdir -p "$RUN_DIR"

command -v dotnet >/dev/null || { echo "needs dotnet"; exit 1; }
command -v npm >/dev/null || { echo "needs npm (nvm: run 'nvm use default' first)"; exit 1; }
command -v setsid >/dev/null || { echo "needs setsid (util-linux)"; exit 1; }

check_port_free() {
  if curl -sf -o /dev/null --max-time 1 "http://localhost:$1"; then
    echo "Port $1 is already in use — run scripts/stop.sh first."
    exit 1
  fi
}

echo "=== Step 1: check ports ==="
check_port_free 5117
check_port_free 4200

# setsid makes each service the leader of its own process group, so stop.sh
# can take down dotnet/ng and all their children with one group kill.
echo "=== Step 2: start backend (http://localhost:5117) ==="
(cd "$ROOT/api/AgentCodeGen.Api" && exec setsid dotnet run) > "$RUN_DIR/api.log" 2>&1 &
echo $! > "$RUN_DIR/api.pid"

echo "=== Step 3: start frontend (http://localhost:4200) ==="
(cd "$ROOT/web" && exec setsid npm start) > "$RUN_DIR/web.log" 2>&1 &
echo $! > "$RUN_DIR/web.pid"

echo "=== Step 4: wait for health ==="
for i in $(seq 1 30); do
  curl -sf -o /dev/null "http://localhost:5117/openapi/v1.json" && { echo "backend up"; break; }
  [ "$i" = 30 ] && { echo "backend did not come up — see $RUN_DIR/api.log"; exit 1; }
  sleep 1
done
for i in $(seq 1 60); do
  curl -sf -o /dev/null "http://localhost:4200" && { echo "frontend up"; break; }
  [ "$i" = 60 ] && { echo "frontend did not come up — see $RUN_DIR/web.log"; exit 1; }
  sleep 1
done

echo
echo "App:  http://localhost:4200"
echo "API:  http://localhost:5117 (OpenAPI: /openapi/v1.json)"
echo "Logs: $RUN_DIR/api.log · $RUN_DIR/web.log"
echo "Stop: scripts/stop.sh"
