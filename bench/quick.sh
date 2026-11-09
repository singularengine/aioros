#!/usr/bin/env bash
# The quick profile (docs/benchmarks.md, "Quick profile"): the whole comparison in QUICK_BUDGET seconds (600) of wall
# time, for trends while developing, not for publishing. Builds come first, only where the publish output is stale
# (bench/build.sh), and are timed apart from the budget. Then bench/app.sh with QUICK=1 (7 variants x 3 interleaved
# runs, 64 connections, 1 s warm-up and 3 s per page, open loop at 50 and 90% on /issues for 2 s each, browser metrics
# from the first run of each variant; trimmed to fit, which with 7 variants means 2 s per page and the desktop profile
# only) and bench/viewscale.sh with QUICK=1 (memory, /v1 /v5 /v20, 2 s each).
# usage: bench/quick.sh            Env: QUICK_BUDGET (600), VIEWSCALE (1: include ViewScale), and the knobs of both scripts.
set -euo pipefail
cd "$(dirname "$0")"
export QUICK=1
QUICK_BUDGET=${QUICK_BUDGET:-600}
VIEWSCALE=${VIEWSCALE:-1}
VIEWSCALE_SECS=12 # projected: start, 3 pages x (1 s warm-up + 2 s + LoadGen start), idle check (measured: 10 s)

echo "== quick profile: trends only, not for publishing"
echo "== building (not part of the budget)"
t=$SECONDS
BUILD_FALLBACK=1 ./build.sh lumen blazor next go leptos loadgen realtime
if ((VIEWSCALE)) && ! BUILD_FALLBACK=1 ./build.sh viewscale; then
  echo "   ViewScale does not build; skipping it" >&2; VIEWSCALE=0
fi
echo "   builds: $((SECONDS - t)) s"

((VIEWSCALE)) || VIEWSCALE_SECS=0
start=$SECONDS
RESERVED_SECS=$VIEWSCALE_SECS BUDGET_SECS=$((QUICK_BUDGET - VIEWSCALE_SECS)) SKIP_BUILD=1 ./app.sh
app=$((SECONDS - start))
vs=0
if ((VIEWSCALE)); then
  t=$SECONDS; SKIP_BUILD=1 ./viewscale.sh; vs=$((SECONDS - t))
fi
total=$((SECONDS - start))
echo
echo "== quick profile done: app ${app} s, ViewScale ${vs} s, total $((total / 60)):$(printf '%02d' $((total % 60))) of a $((QUICK_BUDGET / 60)):$(printf '%02d' $((QUICK_BUDGET % 60))) budget (builds excluded)"
((total <= QUICK_BUDGET)) || echo "   over budget by $((total - QUICK_BUDGET)) s" >&2
