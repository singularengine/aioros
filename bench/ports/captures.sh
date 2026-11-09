#!/usr/bin/env bash
# Captures for the Showcase's "Ported apps" pages: for each ported app, the ORIGINAL upstream desktop app as Avalonia
# draws it (Avalonia.Headless with Skia drawing, the window's client area) and the Aioros web version of the port in
# Chromium (the viewport), both 1280x800.
#
#   samples/Showcase/Showcase/wwwroot/ports/<slug>.original.png   upstream app, headless Skia capture
#   samples/Showcase/Showcase/wwwroot/ports/<slug>.aioros.png     the port's Aioros web page in Chromium
#
# The originals: each upstream is downloaded at its pinned commit into $WORK (outside the repository, so its
# Directory.Build.props, central package management and analyzers do not apply) and built with
# bench/ports/capture/capture.targets injected through CustomAfterMicrosoftCommonTargets. The targets file compiles
# capture/Capture.cs and capture/<slug>.cs INTO the app's own assembly (so the entry point reaches its internal
# Program.BuildAvaloniaApp), adds Avalonia.Headless and Avalonia.Skia at the app's own Avalonia version and makes
# Capture.Main the startup object. Capture.Main sets the app up with a classic desktop lifetime, sizes the main window
# to 1280x800, pumps the dispatcher and the render timer for a settle time and saves window.CaptureRenderedFrame().
# The apps run with HOME pointing into $WORK, so their settings and databases start fresh and stay out of the real home.
#
# The web captures: each port is built and started in Development on 127.0.0.1:5410-5413, and
# bench/realtime/ports-shots.mjs (playwright-core, /usr/bin/chromium) takes the screenshot. Every server is stopped.
#
# usage: bench/ports/captures.sh                       every app, originals and web
#        bench/ports/captures.sh stylet kartova        some apps
#        STAGES=original bench/ports/captures.sh       only the originals (STAGES=web: only the web captures)
#        WORK=/tmp/aioros-captures                     where the upstream sources and builds go (the default)
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
CAPTURE="$ROOT/bench/ports/capture"
OUTDIR="$ROOT/samples/Showcase/Showcase/wwwroot/ports"
WORK=${WORK:-/tmp/aioros-captures}
STAGES=${STAGES:-original web}
WIDTH=1280
HEIGHT=800
SLUGS=("$@")
[ ${#SLUGS[@]} -gt 0 ] || SLUGS=(kartova advancedtodolist stylet reactiverouting)
mkdir -p "$WORK/src" "$WORK/home" "$OUTDIR"

# slug: repository, commit, directory under $WORK/src, project, Avalonia version, framework, assembly, settle (ms),
# extra arguments for the app
declare -A REPO COMMIT PROJECT AVALONIA TFM SETTLE APPARGS WEBPROJECT WEBPORT
REPO[kartova]=markusfurtlehner/kartova
COMMIT[kartova]=db28c76
PROJECT[kartova]=src/Kartova.App/Kartova.App.csproj
AVALONIA[kartova]=11.3.18
TFM[kartova]=net9.0
SETTLE[kartova]=3000
APPARGS[kartova]=""
WEBPROJECT[kartova]=samples/Kartova/Kartova
WEBPORT[kartova]=5410

REPO[advancedtodolist]=AvaloniaUI/Avalonia.Samples
COMMIT[advancedtodolist]=daa400031456dfed0987ccc848d8341602ef0dad
PROJECT[advancedtodolist]=src/Avalonia.Samples/CompleteApps/AdvancedToDoList/AdvancedToDoList.Desktop/AdvancedToDoList.Desktop.csproj
AVALONIA[advancedtodolist]=12.1.3
TFM[advancedtodolist]=net10.0
SETTLE[advancedtodolist]=4000
APPARGS[advancedtodolist]=""
WEBPROJECT[advancedtodolist]=samples/AdvancedToDoList/AdvancedToDoList
WEBPORT[advancedtodolist]=5411

REPO[stylet]=sealoyal2018/Stylet.Avalonia
COMMIT[stylet]=f56678d
PROJECT[stylet]=Samples/Stylet.Samples.Hello/Stylet.Samples.Hello.csproj
AVALONIA[stylet]=11.0.2
TFM[stylet]=net8.0
SETTLE[stylet]=2000
APPARGS[stylet]=""
WEBPROJECT[stylet]=samples/Stylet/StyletSamples
WEBPORT[stylet]=5412

REPO[reactiverouting]=reactiveui/ReactiveUI.Samples
COMMIT[reactiverouting]=8e7ca27f6e6b5049671cdf273a51407d58cfbe7c
PROJECT[reactiverouting]=avalonia/suspension/ReactiveUI.Samples.Suspension/ReactiveUI.Samples.Suspension.csproj
AVALONIA[reactiverouting]=11.0.11
TFM[reactiverouting]=net9.0
SETTLE[reactiverouting]=2000
APPARGS[reactiverouting]=""
WEBPROJECT[reactiverouting]=samples/ReactiveRouting/ReactiveRouting
WEBPORT[reactiverouting]=5413

cat > "$WORK/nuget.config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML

shrink() {
  if command -v oxipng > /dev/null; then oxipng -q -o 4 --strip safe "$1"
  elif command -v optipng > /dev/null; then optipng -quiet -o2 "$1"
  else python3 -c 'import sys; from PIL import Image; Image.open(sys.argv[1]).convert("RGB").save(sys.argv[1], optimize=True)' "$1" 2> /dev/null || true
  fi
}

original() {
  local slug=$1 src="$WORK/src/$1"
  if [ ! -f "$src/.commit" ] || [ "$(cat "$src/.commit")" != "${COMMIT[$slug]}" ]; then
    rm -rf "$src"; mkdir -p "$src"
    curl -fsSL "https://codeload.github.com/${REPO[$slug]}/tar.gz/${COMMIT[$slug]}" | tar -xz -C "$src" --strip-components=1
    echo "${COMMIT[$slug]}" > "$src/.commit"
  fi
  local project="$src/${PROJECT[$slug]}" name
  name=$(basename "$project" .csproj)
  echo "== $slug: building $name (${REPO[$slug]}@${COMMIT[$slug]}, Avalonia ${AVALONIA[$slug]})"
  nice -n 10 dotnet build "$project" -c Release -f "${TFM[$slug]}" --nologo -v q -clp:ErrorsOnly \
    -p:CustomAfterMicrosoftCommonTargets="$CAPTURE/capture.targets" -p:AiorosCaptureProject="$name" \
    -p:AiorosCaptureTarget="$slug" -p:AiorosAvaloniaVersion="${AVALONIA[$slug]}" \
    -p:RestoreConfigFile="$WORK/nuget.config" -p:TreatWarningsAsErrors=false -p:NuGetAudit=false
  local bin dll
  bin="$(dirname "$project")/bin/Release/${TFM[$slug]}"
  dll=$(sed -n 's/.*<AssemblyName>\(.*\)<\/AssemblyName>.*/\1/p' "$project")
  dll="$bin/${dll:-$name}.dll"
  rm -rf "$WORK/home/$slug"; mkdir -p "$WORK/home/$slug"
  # shellcheck disable=SC2086
  (cd "$bin" && HOME="$WORK/home/$slug" XDG_CONFIG_HOME="$WORK/home/$slug/.config" XDG_DATA_HOME="$WORK/home/$slug/.local/share" \
    XDG_CACHE_HOME="${XDG_CACHE_HOME:-$HOME/.cache}" DOTNET_ROLL_FORWARD=Major \
    nice -n 10 dotnet "$dll" "$OUTDIR/$slug.original.png" $WIDTH $HEIGHT "${SETTLE[$slug]}" ${APPARGS[$slug]})
  shrink "$OUTDIR/$slug.original.png"
}

web() {
  local slug=$1 dir="$ROOT/${WEBPROJECT[$slug]}" port=${WEBPORT[$slug]} name pid
  name=$(basename "$dir")
  echo "== $slug: building the Aioros port $name"
  (cd "$dir" && nice -n 10 dotnet build --nologo -v q -clp:ErrorsOnly)
  (cd "$dir" && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" \
    exec dotnet "bin/Debug/net10.0/$name.dll" > "$WORK/$slug-web.log" 2>&1) &
  pid=$!
  trap 'kill $pid 2> /dev/null || true' RETURN
  for _ in $(seq 1 120); do
    curl -fs -o /dev/null "http://127.0.0.1:$port/" && break
    kill -0 $pid 2> /dev/null || { cat "$WORK/$slug-web.log"; return 1; }
    sleep 0.5
  done
  (cd "$ROOT/bench/realtime" && node ports-shots.mjs "http://127.0.0.1:$port/" "$OUTDIR/$slug.aioros.png" $WIDTH $HEIGHT)
  kill $pid; wait $pid 2> /dev/null || true
  shrink "$OUTDIR/$slug.aioros.png"
}

for slug in "${SLUGS[@]}"; do
  [ -n "${REPO[$slug]:-}" ] || { echo "unknown app: $slug" >&2; exit 2; }
  for stage in $STAGES; do "$stage" "$slug"; done
done
ls -l "$OUTDIR"/*.png
