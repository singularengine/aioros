#!/usr/bin/env bash
# Lumen published three ways (docs/benchmarks.md, "Startup: JIT, ReadyToRun and Native AOT"): the JIT build the app
# benchmark measures (aioros), ReadyToRun (aioros-r2r) and Native AOT (aioros-aot), interleaved, with the quick
# profile's SSR settings: cold start, memory, req/s at 64 connections and the open loop on /issues; and on every run,
# the desktop browser's /issues and interactions, so each run's save is the first post after that server started.
# About six minutes with the builds. The bench workflow runs it as the `startup.sh` profile.
# usage: bench/startup.sh            Env: the knobs of bench/app.sh.
set -euo pipefail
cd "$(dirname "$0")"
BUILD_FALLBACK=0 ./build.sh lumen lumen-r2r lumen-aot loadgen realtime
export QUICK=1 VARIANTS=${VARIANTS:-"aioros aioros-r2r aioros-aot"} PROFILES=${PROFILES:-desktop}
export DESKTOP_PAGES=${DESKTOP_PAGES:-/issues} BROWSER_ITERATIONS=${BROWSER_ITERATIONS:-3} BUDGET_SECS=${BUDGET_SECS:-0}
SKIP_BUILD=1 OUT=${OUT:-results/startup-$(date +%Y%m%d-%H%M%S)} ./app.sh
