#!/usr/bin/env bash
# Builds and starts both services, fills 100 orders three ways, and prints how many requests each way sent to the
# user service. Then it stops the user service and shows what each way does when its dependency is down.
# Needs the .NET 10 SDK. Ports: 5100, 5101 (user service), 5200 (order service).
set -euo pipefail
cd "$(dirname "$0")"

dotnet build UserService -nologo -v q
dotnet build OrderService -nologo -v q

pids=()
start() { dotnet "$1/bin/Debug/net10.0/$1.dll" "${@:2}" > "$1.log" 2>&1 & pids+=($!); }
stop_all() { for p in "${pids[@]}"; do kill "$p" 2>/dev/null || true; done; pids=(); }
trap stop_all EXIT

wait_for() { for _ in $(seq 1 50); do curl -s -o /dev/null "$1" && return; sleep 0.2; done; echo "$1 did not start"; exit 1; }
call() { echo "GET /orders/$1"; curl -s "http://localhost:5200/orders/$1"; echo; echo; }

start UserService; start OrderService
wait_for http://localhost:5100/stats; wait_for http://localhost:5200/orders/batched

# The first calls open the connections; measure the second ones
for m in per-order batched fxmap; do curl -s -o /dev/null "http://localhost:5200/orders/$m"; done

echo "=== 100 orders, 12 users, plus one order whose user does not exist ==="
for m in per-order batched fxmap; do call "$m"; done

echo "=== The user service is down ==="
kill "${pids[0]}"; sleep 1
for m in batched fxmap; do call "$m"; done
