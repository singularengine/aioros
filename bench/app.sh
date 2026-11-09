#!/usr/bin/env bash
# Lumen (SaaS app) comparison, the same app and data on every stack (docs/benchmarks.md, "Methodology"):
#   aioros        samples/Lumen/Lumen, precompiled plans, production
#   aioros-base   another commit's Lumen publish in AIOROS_BASE_DIR, measured beside this tree (bench/regression.sh)
#   aioros-r2r    the same published ReadyToRun (bench/startup.sh)
#   aioros-aot    the same published with Native AOT, with the JIT build's plans file (bench/startup.sh)
#   blazor        bench/LumenBlazor, global Interactive Server (prerendered, then a circuit)
#   blazor-ssr    bench/LumenBlazor with Lumen__Render=Static: static SSR, enhanced navigation and forms
#   nextjs        bench/next-lumen, every request rendered
#   nextjs-cache  bench/next-lumen with LUMEN_CACHE=1: Cache Components ("use cache" + cacheTag)
#   go            bench/go-lumen: net/http, templ components, htmx hx-boost for partial navigation and forms
#   leptos        bench/leptos-lumen: Leptos on Axum, server-rendered, hydrated (WebAssembly), client-side navigation
# Per run of a variant: cold start and memory; per page, closed-loop SSR throughput at 1 and 64 connections; on
# RATE_PAGES, open-loop latency at RATE_PCTS percent of the 64-connection maximum; browser metrics per PROFILES.
#
# usage: bench/app.sh                       one run of every variant (the previous single-run usage)
#        RUNS=5 VARIANTS="aioros nextjs" bench/app.sh
#        QUICK=1 bench/app.sh                three short runs within BUDGET_SECS, trends only (bench/quick.sh adds ViewScale)
# Runs are interleaved: iteration i runs every variant, starting one further along the list each time, and each run
# waits for an idle machine first (bench/idle.sh). Output: results/app-<time>/app.jsonl (one line per run, raw),
# env.json, server logs and APP.md (medians, p99 and spread); results/app.jsonl and results/APP.md are the latest.
set -euo pipefail
cd "$(dirname "$0")"
ROOT=$(cd .. && pwd)
QUICK=${QUICK:-0}
if [[ $QUICK == 1 ]]; then # the quick profile (docs/benchmarks.md, "Quick profile"); every value can still be overridden
  : "${RUNS:=3}" "${C1_SECS:=0}" "${C64_SECS:=3}" "${LOADGEN_WARMUP:=1}" "${RATE_PAGES:=/issues}" "${RATE_PCTS:=50 90}"
  : "${RATE_SECS:=2}" "${RATE_WARMUP:=0}" "${BROWSER_RUNS:=1}" "${BROWSER_ITERATIONS:=1}" "${BUDGET_SECS:=600}"
  : "${IDLE_BASIS:=cpu}" "${IDLE_TIMEOUT:=60}" "${IDLE_POLL:=5}" "${IDLE_SAMPLE:=0.5}" "${BUILD_FALLBACK:=1}" "${SETTLE_SECS:=0}"
  export IDLE_BASIS IDLE_TIMEOUT IDLE_POLL IDLE_SAMPLE BUILD_FALLBACK
fi
[[ -n ${LOADGEN_WARMUP:-} ]] && export LOADGEN_WARMUP
source ./idle.sh

VARIANTS=${VARIANTS:-"aioros blazor blazor-ssr nextjs nextjs-cache go leptos"}
RUNS=${RUNS:-1}
PAGES=${PAGES:-"/issues /issues/LUM-105 /projects /inbox /settings"}
C1_SECS=${C1_SECS:-5}
C64_SECS=${C64_SECS:-10}
RATE_PAGES=${RATE_PAGES:-"/issues /issues/LUM-105"}
RATE_PCTS=${RATE_PCTS:-"25 50 75 90"}
RATE_SECS=${RATE_SECS:-10}
PROFILES=${PROFILES:-"desktop mobile"}
BROWSER_RUNS=${BROWSER_RUNS:-5}
BROWSER_ITERATIONS=${BROWSER_ITERATIONS:-$RUNS}
DESKTOP_PAGES=${DESKTOP_PAGES:-$PAGES}
MOBILE_PAGES=${MOBILE_PAGES:-$PAGES}
BUDGET_SECS=${BUDGET_SECS:-0}
SETTLE_SECS=${SETTLE_SECS:-1}
SKIP_BUILD=${SKIP_BUILD:-0}
SERVER_CPUS=${SERVER_CPUS:-0-7}
CLIENT_CPUS=${CLIENT_CPUS:-8-31}
SERVER_CORES=$(taskset -c "$SERVER_CPUS" nproc)
NEXT_WORKERS=${NEXT_WORKERS:-$SERVER_CORES}
OUT=${OUT:-results/app-$(date +%Y%m%d-%H%M%S)}
mkdir -p "$OUT"
LOADGEN="taskset -c $CLIENT_CPUS dotnet LoadGen/bin/Release/net10.0/LoadGen.dll"
NEXT_BIN=$ROOT/bench/next-lumen/node_modules/next/dist/bin/next

if [[ $SKIP_BUILD != 1 ]]; then
  echo "== building"
  t=$SECONDS; ./build.sh lumen blazor next go leptos loadgen realtime; echo "   builds: $((SECONDS - t)) s (not part of the measurement)"
fi

python3 - "$OUT/env.json" "$SERVER_CPUS" "$CLIENT_CPUS" "$NEXT_WORKERS" "$QUICK" <<'EOF'
import json, os, platform, subprocess, sys
def sh(c):
    try: return subprocess.run(c, shell=True, capture_output=True, text=True).stdout.strip()
    except Exception: return None
cpu = next((l.split(":", 1)[1].strip() for l in open("/proc/cpuinfo") if l.startswith("model name")), None)
json.dump(dict(cpu=cpu, cores=os.cpu_count(), memGb=round(os.sysconf("SC_PAGE_SIZE") * os.sysconf("SC_PHYS_PAGES") / 2**30, 1),
    kernel=platform.release(), dotnet=sh("dotnet --version"), node=sh("node --version"), chromium=sh("chromium --version"),
    next=sh("node -p \"require('./next-lumen/node_modules/next/package.json').version\""), git=sh("git rev-parse --short HEAD"),
    go=sh("go version"), rust=sh("rustc --version"),
    leptos=sh("sed -n '/^name = \"leptos\"$/{n;s/version = \"\\(.*\\)\"/\\1/p}' leptos-lumen/Cargo.lock"),
    templ=sh("sed -n 's#^\\s*github.com/a-h/templ \\(v[^ ]*\\)$#\\1#p' go-lumen/go.mod"),
    serverCpus=sys.argv[2], clientCpus=sys.argv[3], nextWorkers=int(sys.argv[4]), http="HTTP/1.1 over loopback, no TLS",
    profile="quick" if sys.argv[5] == "1" else "full"),
    open(sys.argv[1], "w"), indent=1)
EOF

tree_rss_mb() {
  local total=0 stack=("$1")
  while ((${#stack[@]})); do
    local p=${stack[-1]}; unset 'stack[-1]'
    [[ -r /proc/$p/status ]] || continue
    total=$((total + $(awk '/VmRSS/{print $2}' /proc/$p/status 2>/dev/null || echo 0)))
    for c in $(cat /proc/$p/task/*/children 2>/dev/null); do stack+=("$c"); done
  done
  echo $((total / 1024))
}
kill_tree() { for c in $(cat /proc/$1/task/*/children 2>/dev/null); do kill_tree $c; done; kill $1 2>/dev/null || true; }
json() { python3 -c "import json,sys; print(json.dumps($1))" "${@:2}"; }

next_cmd() { # the Next.js production server: `next start`, or one per core through Node's cluster module
  if ((NEXT_WORKERS > 1)); then echo "node cluster.mjs $1 127.0.0.1"; else echo "node $NEXT_BIN start -p $1 -H 127.0.0.1"; fi
}

variant() { # name -> "port dir env command..."
  case $1 in
    aioros)       echo "5231 ${AIOROS_DIR:-$ROOT/samples/Lumen/Lumen/bin/publish} - dotnet Lumen.dll --urls http://127.0.0.1:5231" ;;
    aioros-base)  echo "5241 ${AIOROS_BASE_DIR:?the base build to compare with} - dotnet Lumen.dll --urls http://127.0.0.1:5241" ;;
    aioros-r2r)   echo "5239 $ROOT/samples/Lumen/Lumen/bin/publish-r2r - dotnet Lumen.dll --urls http://127.0.0.1:5239" ;;
    aioros-aot)   echo "5240 $ROOT/samples/Lumen/Lumen/bin/publish-aot - ./Lumen --urls http://127.0.0.1:5240" ;;
    blazor)       echo "5233 LumenBlazor/bin/publish - dotnet LumenBlazor.dll --urls http://127.0.0.1:5233" ;;
    blazor-ssr)   echo "5235 LumenBlazor/bin/publish Lumen__Render=Static dotnet LumenBlazor.dll --urls http://127.0.0.1:5235" ;;
    nextjs)       echo "5234 next-lumen - $(next_cmd 5234)" ;;
    nextjs-cache) echo "5236 next-lumen LUMEN_CACHE=1 $(next_cmd 5236)" ;;
    go)           echo "5237 go-lumen - ./bin/go-lumen -addr 127.0.0.1:5237 -shared ../lumen-shared" ;;
    leptos)       echo "5238 leptos-lumen - env LEPTOS_OUTPUT_NAME=leptos-lumen LEPTOS_SITE_ROOT=target/site LEPTOS_SITE_ADDR=127.0.0.1:5238 ./target/release/leptos-lumen" ;;
    *) echo "unknown variant $1" >&2; exit 1 ;;
  esac
}

run() {
  local name=$1 iteration=$2
  read -r port dir extra cmd <<< "$(variant $name)"
  [[ $extra == - ]] && extra=
  local base=http://127.0.0.1:$port
  local t_idle=$SECONDS
  wait_idle
  IDLE_SECS=$((IDLE_SECS + SECONDS - t_idle))
  local t_run=$SECONDS load0; load0=$(load_now)
  echo "== $name (run $iteration/$RUNS, load $load0, elapsed $(elapsed))"
  local t0; t0=$(date +%s%N)
  (cd $dir && exec env ASPNETCORE_ENVIRONMENT=Production Logging__LogLevel__Default=Warning NEXT_TELEMETRY_DISABLED=1 \
     NODE_ENV=production NEXT_WORKERS=$NEXT_WORKERS $extra taskset -c $SERVER_CPUS $cmd > $ROOT/bench/$OUT/$name-$iteration.log 2>&1) &
  local pid=$!
  server=$pid
  until curl -sf -o /dev/null $base/issues; do
    kill -0 $pid 2>/dev/null || { echo "   $name did not start (not built?), skipped: $OUT/$name-$iteration.log" >&2; server=0; return 0; }
    sleep 0.02
  done
  local cold=$(( ($(date +%s%N) - t0) / 1000000 ))
  local first; first=$(curl -s -o /dev/null -w "%{time_total}" $base/projects)
  for p in $PAGES; do curl -s -o /dev/null $base$p; curl -s -o /dev/null -b theme=dark $base$p; done
  sleep $SETTLE_SECS
  local idle; idle=$(tree_rss_mb $pid)

  local ssr=()
  for p in $PAGES; do
    local c1 c64 rates=()
    c1=null; [[ $C1_SECS != 0 ]] && c1=$($LOADGEN $base$p 1 $C1_SECS $pid)
    c64=$($LOADGEN $base$p 64 $C64_SECS $pid)
    if [[ " $RATE_PAGES " == *" $p "* ]]; then
      local max; max=$(json 'json.loads(sys.argv[1])["rps"]' "$c64")
      for pct in $RATE_PCTS; do
        local rate; rate=$(python3 -c "print(max(1, round($max * $pct / 100)))")
        rates+=("$(env ${RATE_WARMUP:+LOADGEN_WARMUP=$RATE_WARMUP} LOADGEN_RATE=$rate $LOADGEN $base$p 64 $RATE_SECS $pid | python3 -c "import json,sys; d=json.load(sys.stdin); d['pct']=$pct; print(json.dumps(d))")")
      done
    fi
    local size gz enc
    size=$(curl -s $base$p | wc -c); gz=$(curl -s $base$p | gzip -c | wc -c)
    enc=$(curl -s -o /dev/null -D - -H 'Accept-Encoding: br, gzip' $base$p | awk -F': ' 'tolower($1)=="content-encoding"{print $2}' | tr -d '\r')
    ssr+=("$(python3 -c "import json,sys; print(json.dumps(dict(page=sys.argv[1], html=int(sys.argv[2]), gzip=int(sys.argv[3]), encoding=sys.argv[4] or None, c1=json.loads(sys.argv[5]), c64=json.loads(sys.argv[6]), rates=[json.loads(x) for x in sys.argv[7:]])))" "$p" "$size" "$gz" "$enc" "$c1" "$c64" "${rates[@]}")")
    echo "   $p: $(echo "${ssr[-1]}" | python3 -c "import json,sys; d=json.load(sys.stdin); print((d['c1'] or {}).get('rps', '-'), d['c64']['rps'], d['c64']['peakRssMb'], ' '.join(f\"{r['pct']}%:{r['p99']}ms\" for r in d['rates']))")"
  done
  local t_ssr=$((SECONDS - t_run)) browser=()
  SSR_SECS=$((SSR_SECS + t_ssr)); SSR_DONE=$((SSR_DONE + 1))
  echo "   ssr: ${t_ssr} s"
  if ((iteration <= BROWSER_ITERATIONS)); then
    local t_browser=$SECONDS
    for profile in $PROFILES; do
      local pages=$DESKTOP_PAGES; [[ $profile == mobile ]] && pages=$MOBILE_PAGES
      [[ -n $pages ]] || continue
      [[ $pages == - ]] && pages=" "
      browser+=("$(cd realtime && BROWSER_PAGES="$pages" taskset -c $CLIENT_CPUS node app-metrics.mjs $base $profile $BROWSER_RUNS)")
    done
    BROWSER_DONE=$((BROWSER_DONE + 1)); BROWSER_SECS=$((BROWSER_SECS + SECONDS - t_browser))
    echo "   browser ($PROFILES; desktop: ${DESKTOP_PAGES:-none}; mobile: ${MOBILE_PAGES:-none}): $((SECONDS - t_browser)) s"
  fi
  local after; after=$(tree_rss_mb $pid)
  kill_tree $pid; wait $pid 2>/dev/null || true; server=0
  local load1; load1=$(load_now)
  python3 -c "import json,sys; b=[json.loads(x) for x in sys.argv[9].split('\x1e') if x]; print(json.dumps(dict(name=sys.argv[1], iteration=int(sys.argv[2]), loadStart=sys.argv[3], loadEnd=sys.argv[4], coldStartMs=int(sys.argv[5]), firstVisitMs=round(float(sys.argv[6])*1000), idleRssMb=int(sys.argv[7]), afterRssMb=int(sys.argv[8]), browser={x['profile']: x for x in b}, ssr=[json.loads(x) for x in sys.argv[10:]])))" \
    "$name" "$iteration" "$load0" "$load1" "$cold" "$first" "$idle" "$after" "$(printf '%s\x1e' "${browser[@]}")" "${ssr[@]}" >> $OUT/app.jsonl
}

read -ra list <<< "$VARIANTS"
words() { wc -w <<< "$1"; }
# Projected seconds, calibrated on the 32-core benchmark machine: one SSR run of a variant (start, warm-up requests,
# closed loop on every page, open loop on RATE_PAGES, LoadGen and process overhead), and one browser profile.
ssr_run_secs() {
  awk -v n="$(words "$PAGES")" -v c1="$C1_SECS" -v c64="$C64_SECS" -v w="${LOADGEN_WARMUP:-3}" -v s="$SETTLE_SECS" \
      -v r="$(( $(words "$RATE_PAGES") * $(words "$RATE_PCTS") ))" -v rs="$RATE_SECS" -v rw="${RATE_WARMUP:-${LOADGEN_WARMUP:-3}}" \
      -v idle="$([[ $IDLE_CHECK == 0 ]] && echo 0 || echo "$IDLE_SAMPLE")" \
      'BEGIN { print 1.5 + idle + s + n * (c64 + w + 0.5 + (c1 > 0 ? c1 + w + 0.4 : 0)) + r * (rs + rw + 0.3) }'
}
browser_secs() { # profile pages ("-": the interactions only, empty: not measured); desktop measured on the lab runner
  [[ -n $2 ]] || { echo 0; return; }
  local n; n=$(words "${2//-/}")
  if [[ $1 == mobile ]]; then echo "$((13 + n * 5))"; else echo "$((7 + n * 2))"; fi
}
browser_variant_secs() { # BROWSER_RUNS runs of each profile, after its discarded first load (bench/realtime/app-metrics.mjs)
  local total=0 profile
  for profile in $PROFILES; do
    local pages=$DESKTOP_PAGES; [[ $profile == mobile ]] && pages=$MOBILE_PAGES
    [[ -n $pages ]] || continue
    total=$((total + $(browser_secs $profile "$pages") * BROWSER_RUNS + $([[ $profile == mobile ]] && echo 3 || echo 1)))
  done
  echo $total
}
projection() { # SSR runs, browser variants -> seconds
  awk -v a="$1" -v p="$(ssr_run_secs)" -v b="$2" -v bv="$(browser_variant_secs)" 'BEGIN { printf "%d", a * p + b * bv + 0.5 }'
}
elapsed() { local e=$((SECONDS - START)); printf '%d:%02d' $((e / 60)) $((e % 60)); }
# Trims the browser metrics to one page and the interactions, then the SSR measurement to 2 s, then the mobile and the
# desktop profiles, until the projection plus BUDGET_MARGIN fits BUDGET_SECS (0: no budget): with seven variants the
# SSR measurement alone no longer fits at 3 s, and at 2 s the desktop profile still does. It decides once, before the
# first run, so every variant is measured the same way.
fit_budget() { # SSR runs, browser variants
  ((BUDGET_SECS > 0)) || return 0
  local left=$((BUDGET_SECS - BUDGET_MARGIN)) step need
  for step in mobile-one desktop-one mobile-interactions ssr-2s no-mobile no-desktop; do
    need=$(projection "$1" "$2")
    ((need <= left)) && return 0
    echo "   budget: projected ${need} s > ${left} s, trimming: $step" >&2
    case $step in
      mobile-one) MOBILE_PAGES=${PAGES%% *} ;;
      desktop-one) DESKTOP_PAGES=${PAGES%% *} ;;
      mobile-interactions) MOBILE_PAGES=- ;;
      no-mobile) MOBILE_PAGES= ;;
      no-desktop) DESKTOP_PAGES= ;;
      ssr-2s) C64_SECS=2 RATE_SECS=2 ;;
    esac
  done
  need=$(projection "$1" "$2")
  ((need <= left)) || echo "   budget: still projected ${need} s > ${left} s; reduce VARIANTS or RUNS" >&2
}

BUDGET_MARGIN=${BUDGET_MARGIN:-10}
server=0
trap '[[ $server != 0 ]] && kill_tree $server; wait 2>/dev/null || true' EXIT
SSR_SECS=0 SSR_DONE=0 IDLE_SECS=0 BROWSER_SECS=0 BROWSER_DONE=0
START=$SECONDS
total=$((${#list[@]} * RUNS)); browsers=$((${#list[@]} * (BROWSER_ITERATIONS < RUNS ? BROWSER_ITERATIONS : RUNS)))
[[ $QUICK == 1 ]] && echo "== quick profile: trends only, not for publishing"
echo "== plan: ${#list[@]} variants x $RUNS runs; SSR $(ssr_run_secs) s per run; browser $(browser_variant_secs) s per variant on $browsers run(s)"
RESERVED_SECS=${RESERVED_SECS:-0} # time the caller keeps for later phases (bench/quick.sh: ViewScale)
p=$(projection $total $browsers)
echo "   projected: $p s$( ((RESERVED_SECS)) && echo " + $RESERVED_SECS s reserved = $((p + RESERVED_SECS)) s")$( ((BUDGET_SECS)) && echo ", budget $((BUDGET_SECS + RESERVED_SECS)) s")"
fit_budget $total $browsers
((BUDGET_SECS)) && echo "   after trimming: $(projection $total $browsers) s; browser pages: desktop ${DESKTOP_PAGES:-not measured}, mobile ${MOBILE_PAGES:-not measured} (-: interactions only)"
for ((i = 1; i <= RUNS; i++)); do
  for ((k = 0; k < ${#list[@]}; k++)); do
    run "${list[$(( (k + i - 1) % ${#list[@]} ))]}" $i
  done
done
echo "== measured $(elapsed): SSR ${SSR_SECS} s over $SSR_DONE runs, browser ${BROWSER_SECS} s over $BROWSER_DONE variants, idle checks ${IDLE_SECS} s"

python3 app_report.py $OUT/app.jsonl $OUT/env.json > $OUT/APP.md
cp $OUT/app.jsonl results/app.jsonl; cp $OUT/APP.md results/APP.md
cat $OUT/APP.md
