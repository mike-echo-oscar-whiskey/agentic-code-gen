#!/usr/bin/env bash
# Purpose: stop the backend and frontend started by scripts/run.sh
# Usage:   scripts/stop.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_DIR="$ROOT/.run"

stop_one() {
  local name="$1" pidfile="$RUN_DIR/$1.pid"
  if [ ! -f "$pidfile" ]; then
    echo "$name: no pidfile (not started via run.sh?)"
    return
  fi

  local pid
  pid="$(cat "$pidfile")"
  if kill -0 "$pid" 2>/dev/null; then
    # Kill the whole process group so dotnet/ng children go down with the shell.
    kill -- -"$pid" 2>/dev/null || kill "$pid" 2>/dev/null || true
    for _ in $(seq 1 10); do
      kill -0 "$pid" 2>/dev/null || break
      sleep 0.5
    done
    kill -0 "$pid" 2>/dev/null && kill -9 -- -"$pid" 2>/dev/null || true
    echo "$name: stopped (pid $pid)"
  else
    echo "$name: already gone (pid $pid)"
  fi
  rm -f "$pidfile"
}

echo "=== Stopping app ==="
stop_one api
stop_one web

# Fallback for strays not tracked by pidfiles (e.g. started by hand).
# The [b]racketed first letter keeps the pattern from matching this script's
# own command line (or any shell whose invocation mentions these names).
pkill -f "[A]gentCodeGen.Api.dll" 2>/dev/null || true
pkill -f "[n]g serve --port 4200" 2>/dev/null || true

echo "Ports 5117 and 4200 should now be free."
