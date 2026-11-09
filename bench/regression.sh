#!/usr/bin/env bash
# The regression check (principle 9; docs/benchmarks.md, "Regression check"): Aioros alone on the quick profile's SSR
# measurement (3 runs, 5 pages, 64 connections, the open loop on /issues, no browser metrics), compared by
# bench/regression.py, which fails on a drop past its margins. Without BASE it compares with the baseline stored for the
# runner (bench/baselines/regression.json). With BASE=<commit> it publishes that commit beside this tree and measures
# both interleaved (aioros-base and aioros, one after the other in every run), comparing them run by run, so the host's
# other jobs weigh on both sides alike. The bench workflow runs it as the `regression.sh` profile.
# usage: bench/regression.sh [base]     base: a commit or branch (or Env BASE); Env: UPDATE=1 records a run without a base
#                                       as the stored baseline; RUNS (3); GEO_MARGIN, PAGE_MARGIN.
set -euo pipefail
cd "$(dirname "$0")"
BUILD_FALLBACK=0 ./build.sh lumen loadgen
OUT=results/regression-$(date +%Y%m%d-%H%M%S)
VARIANTS=aioros
BASE=${1:-${BASE:-}}
if [[ -n ${BASE:-} ]]; then
  base=$(cd .. && pwd)/artifacts/regression-base
  commit=$(git rev-parse --verify -q "$BASE^{commit}" || { git fetch -q --depth 1 origin "$BASE" && git rev-parse FETCH_HEAD; })
  rm -rf "$base"; mkdir -p "$base"
  (cd .. && git archive "$commit") | tar -x -C "$base"
  echo "== base $BASE ($(git rev-parse --short "$commit"))"
  dotnet publish "$base/samples/Lumen/Lumen/Lumen.csproj" -c Release -o "$base/publish" -v q -nologo > /dev/null
  (cd "$base/publish" && ASPNETCORE_ENVIRONMENT=Production Logging__LogLevel__Default=Warning dotnet Lumen.dll --precompile="$base/publish/aioros.plans.json" | grep Precompiled)
  export AIOROS_BASE_DIR=$base/publish
  VARIANTS="aioros-base aioros"
fi
QUICK=1 VARIANTS=$VARIANTS PROFILES=" " SKIP_BUILD=1 OUT=$OUT ./app.sh
python3 regression.py "$OUT/app.jsonl" "$OUT/env.json" baselines/regression.json ${UPDATE:+--update} | tee "$OUT/REGRESSION.md"
