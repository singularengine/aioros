#!/usr/bin/env bash
# Aioros composable views: how loader fan-out and the database pool scale with the number of views on a page.
# bench/ViewScale has /v1, /v5 and /v20, composing 1, 5 and 20 instances of one view whose loader runs one query
# (10 rows at a random offset). Every page is measured with LoadGen (closed loop) at 1 and 64 connections, per data mode:
#   memory    rows from an in-process array (the cost of the views themselves)
#   postgres  rows from PostgreSQL 17 in a container started here and removed on exit: podman pinned to DB_CPUS
#             through taskset (rootless podman has no cpuset controller), docker through --cpuset-cpus (the daemon
#             starts the container, so the client's affinity does not reach it; the lab's runner talks to the host's
#             daemon through its socket)
# Env: SECS (10), MODES ("memory postgres"), CONNS ("1 64"), SERVER_CPUS (0-7), DB_CPUS (8-11), CLIENT_CPUS (12-31),
#      CONTAINER (podman, or docker),
#      VIEWSCALE_PG (connection string; default: the container, Npgsql's default pool of 100 connections,
#      PostgreSQL's default max_connections of 100), IDLE_CHECK (1: wait for an idle machine through idle.sh when present),
#      SKIP_BUILD (1: reuse the publish output), QUICK (1: memory only, 64 connections, 1 s warm-up and 2 s per page,
#      the idle check gives up after 60 s; part of bench/quick.sh).
# Output: results/viewscale.jsonl + a markdown table on stdout.
set -euo pipefail
cd "$(dirname "$0")"
BENCH=$PWD
OUT=results; mkdir -p $OUT; : > $OUT/viewscale.jsonl
if [[ ${QUICK:-0} == 1 ]]; then
  : "${SECS:=2}" "${MODES:=memory}" "${CONNS:=64}" "${LOADGEN_WARMUP:=1}"
  : "${IDLE_BASIS:=cpu}" "${IDLE_TIMEOUT:=60}" "${IDLE_POLL:=5}" "${IDLE_SAMPLE:=0.5}" "${BUILD_FALLBACK:=1}"
  export LOADGEN_WARMUP IDLE_BASIS IDLE_TIMEOUT IDLE_POLL IDLE_SAMPLE BUILD_FALLBACK
fi
SECS=${SECS:-10}
MODES=${MODES:-memory postgres}
CONNS=${CONNS:-1 64}
SERVER_CPUS=${SERVER_CPUS:-0-7}
DB_CPUS=${DB_CPUS:-8-11}
CLIENT_CPUS=${CLIENT_CPUS:-12-31}
PAGES="/v1 /v5 /v20"
PORT=5241
PUBLISH=$BENCH/ViewScale/bin/publish
LOADGEN="taskset -c $CLIENT_CPUS dotnet $BENCH/LoadGen/bin/Release/net10.0/LoadGen.dll"
PG_NAME=aioros-viewscale-pg
CONTAINER=${CONTAINER:-podman}
if [[ $CONTAINER == docker ]]; then PIN=(); CPUSET=(--cpuset-cpus "$DB_CPUS"); else PIN=(taskset -c "$DB_CPUS"); CPUSET=(); fi

idle() { if [[ ${IDLE_CHECK:-1} == 1 && -f $BENCH/idle.sh ]]; then source $BENCH/idle.sh; wait_idle; fi; }

if [[ ${SKIP_BUILD:-0} != 1 ]]; then
  echo "== building"
  ./build.sh viewscale loadgen
fi

pg_started=0
server=0
cleanup() {
  [[ $server != 0 ]] && kill $server 2>/dev/null && wait $server 2>/dev/null || true
  [[ $pg_started == 1 ]] && $CONTAINER rm -f $PG_NAME > /dev/null 2>&1 || true
}
trap cleanup EXIT

start_postgres() {
  $CONTAINER rm -f $PG_NAME > /dev/null 2>&1 || true
  "${PIN[@]}" $CONTAINER run -d --name $PG_NAME "${CPUSET[@]}" -e POSTGRES_PASSWORD=bench -e POSTGRES_DB=viewscale \
    -p 127.0.0.1::5432 docker.io/library/postgres:17 > /dev/null
  pg_started=1
  until $CONTAINER exec $PG_NAME pg_isready -U postgres -d viewscale -q 2>/dev/null; do sleep 0.2; done
  until $CONTAINER exec $PG_NAME psql -U postgres -d viewscale -qc "select 1" > /dev/null 2>&1; do sleep 0.2; done
  $CONTAINER exec -i $PG_NAME psql -U postgres -d viewscale -q > /dev/null <<'SQL'
CREATE TABLE items (id int PRIMARY KEY, title text NOT NULL, status text NOT NULL, updated timestamptz NOT NULL);
INSERT INTO items SELECT i, 'Item ' || i, (ARRAY['todo','progress','review','done'])[i % 4 + 1],
  timestamptz '2026-10-01 00:00:00+00' - i * interval '1 minute' FROM generate_series(1, 1000) i;
CREATE INDEX items_updated ON items (updated DESC);
ANALYZE items;
SQL
  local port; port=$($CONTAINER port $PG_NAME 5432/tcp | head -1 | sed 's/.*://')
  export VIEWSCALE_PG="Host=127.0.0.1;Port=$port;Username=postgres;Password=bench;Database=viewscale"
}

for mode in $MODES; do
  if [[ $mode == postgres && -z ${VIEWSCALE_PG:-} ]]; then start_postgres; fi
  idle
  echo "== viewscale $mode"
  (cd $PUBLISH && exec env ASPNETCORE_ENVIRONMENT=Production Logging__LogLevel__Default=Warning VIEWSCALE_DATA=$mode \
     taskset -c $SERVER_CPUS dotnet ViewScale.dll --urls http://127.0.0.1:$PORT > $BENCH/$OUT/viewscale-$mode.log 2>&1) &
  server=$!
  until curl -sf -o /dev/null http://127.0.0.1:$PORT/v1; do sleep 0.05; done
  for p in $PAGES; do
    views=${p#/v}
    rows=$(curl -s http://127.0.0.1:$PORT$p | grep -o 'Rows from' | wc -l)
    [[ $rows == "$views" ]] || { echo "   $p shows $rows views, expected $views" >&2; exit 1; }
    for c in $CONNS; do
      r=$($LOADGEN http://127.0.0.1:$PORT$p $c $SECS $server)
      echo "{\"mode\":\"$mode\",\"views\":$views,\"connections\":$c,\"result\":$r}" >> $OUT/viewscale.jsonl
      echo "   $p c=$c: $r"
    done
  done
  kill $server; wait $server 2>/dev/null || true; server=0
done

[[ ${QUICK:-0} == 1 ]] && printf '\n> **Quick profile: trends only, not for publishing** (%s s per page, %s connections, %s).\n' "$SECS" "$CONNS" "$MODES"
python3 - $OUT/viewscale.jsonl <<'PY'
import json, sys
rows = [json.loads(l) for l in open(sys.argv[1])]
conns = sorted({r["connections"] for r in rows})
print("\n| Data | Views | " + " | ".join(f"{c} conn: req/s (p50 / p99)" for c in conns) + " |")
print("|---|---|" + "---|" * len(conns))
for mode in dict.fromkeys(r["mode"] for r in rows):
    for v in sorted({r["views"] for r in rows if r["mode"] == mode}):
        cells = []
        for c in conns:
            m = next((r["result"] for r in rows if r["mode"] == mode and r["views"] == v and r["connections"] == c), None)
            cells.append(f'{m["rps"]:,.0f} ({m["p50"]} / {m["p99"]} ms)' + (f' {m["errors"]} errors' if m["errors"] else "") if m else "-")
        print(f"| {mode} | {v} | " + " | ".join(cells) + " |")
PY
