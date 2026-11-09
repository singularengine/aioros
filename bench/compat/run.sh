#!/usr/bin/env bash
# Runs the compatibility bench on a sample app: starts it in development, compares every page in both themes and
# every state against its baseline (bench/compat/baselines/<app>.json), and stops it. Extra arguments go to compat.py:
# --check in CI, --update to record the baseline.
#   bench/compat/run.sh gallery --check
#   OUT=/tmp/compat PORT=5390 bench/compat/run.sh controlcatalog --update
#   bench/compat/run.sh gallery --check --report samples/Gallery/Gallery/wwwroot/compat  (the Gallery's parity report)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
app_name="${1:?usage: run.sh gallery|controlcatalog [compat.py options]}"
shift
case "$app_name" in
  gallery)
    project="$root/samples/Gallery/Gallery"; assembly=Gallery
    routes=(/ /controls/text /controls/buttons /controls/ranges /controls/items /controls/navigation /controls/media /controls/vectors /controls/charts /controls/layout) ;;
  controlcatalog)
    project="$root/samples/ControlCatalog/ControlCatalog"; assembly=ControlCatalog
    routes=(/ /border /relativepanel /textblock) ;;
  *) echo "unknown app $app_name" >&2; exit 2 ;;
esac
port="${PORT:-5390}"
out="${OUT:-$root/artifacts/compat/$app_name}"
configuration="${CONFIGURATION:-Debug}"

SECONDS=0
dotnet build "$project/$assembly.csproj" -c "$configuration" --nologo -v quiet
echo "build ${SECONDS} s"
SECONDS=0
log="$(mktemp)"
(cd "$project" && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" exec dotnet "bin/$configuration/net10.0/$assembly.dll") > "$log" 2>&1 &
app=$!
trap 'kill $app 2>/dev/null || true; wait $app 2>/dev/null || true; rm -f "$log"' EXIT
for _ in $(seq 1 120); do
  curl -sf -o /dev/null "http://127.0.0.1:$port${ready:-/}" && break
  kill -0 $app 2>/dev/null || { cat "$log"; exit 1; }
  sleep 1
done
echo "start ${SECONDS} s"
python3 "$here/compat.py" "http://127.0.0.1:$port" "$out" --baseline "$here/baselines/$app_name.json" --routes "${routes[@]}" "$@"
