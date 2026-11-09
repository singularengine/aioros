#!/usr/bin/env bash
# The render path's cost of publishing the outer scopes to the getters for every rendered body (OuterScopes.Swap in
# PlanValues.WriteAll, added for bindings to an ancestor's data context): BenchmarkDotNet (bench/RenderPath) writes
# Lumen-like issue rows (10, 50 and 200) through RepeatNode, which publishes per row, and through the same nodes
# without the publication, pinned to SERVER_CPUS.
# Env: SERVER_CPUS (0-3). Extra arguments go to BenchmarkDotNet (say --job short).
# Output: results/renderpath/results/*.md and *.csv, and BenchmarkDotNet's table on stdout.
set -euo pipefail
cd "$(dirname "$0")"
OUT=results/renderpath; mkdir -p $OUT
dotnet build -c Release RenderPath -nologo -v q
taskset -c "${SERVER_CPUS:-0-3}" dotnet RenderPath/bin/Release/net10.0/RenderPath.dll --filter '*' --artifacts "$OUT" "$@"
