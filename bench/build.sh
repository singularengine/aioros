#!/usr/bin/env bash
# Builds what the benchmarks run, each target only when its output is missing or older than one of its inputs, so a
# second run reuses the publish output. A stamp file (.bench-built) in each output marks a successful build.
# usage: bench/build.sh [target...]   targets: lumen lumen-r2r lumen-aot blazor next go leptos loadgen realtime viewscale
#        (default: all but lumen-r2r and lumen-aot)
# go needs Go (go.dev tarball) and leptos needs Rust with the wasm32-unknown-unknown target and cargo-leptos; the bench
# bench workflow installs the pinned versions.
# Env: FORCE_BUILD=1 rebuilds everything; BUILD_FALLBACK=1 (quick profile) keeps the previous output, with a warning,
# when a rebuild fails (say, while src/ is being edited) instead of stopping.
set -euo pipefail
cd "$(dirname "$0")"
BENCH=$PWD
ROOT=$(cd .. && pwd)
NEXT_BIN=$BENCH/next-lumen/node_modules/next/dist/bin/next
FORCE_BUILD=${FORCE_BUILD:-0}
BUILD_FALLBACK=${BUILD_FALLBACK:-0}
STAMP=.bench-built

stale() { # stamp input... -> 0 when the stamp is missing or an input is newer
  local stamp=$1; shift
  [[ $FORCE_BUILD == 1 || ! -f $stamp ]] && return 0
  [[ -n $(find "$@" \( -name bin -o -name obj -o -name node_modules -o -name .next -o -name .next-cache -o -name target \) -prune \
    -o -type f -newer "$stamp" -print -quit 2>/dev/null) ]]
}

target() { # name stamp "inputs" command...
  local name=$1 stamp=$2 inputs=$3; shift 3
  local t0=$SECONDS
  # shellcheck disable=SC2086
  if ! stale "$stamp" $inputs; then echo "   $name: up to date"; return 0; fi
  if "$@"; then
    touch "$stamp"; echo "   $name: built in $((SECONDS - t0)) s"
  elif [[ $BUILD_FALLBACK == 1 && -d $(dirname "$stamp") ]]; then
    echo "   $name: BUILD FAILED, reusing the previous output in $(dirname "$stamp")" >&2
  else
    echo "error: building $name failed" >&2; exit 1
  fi
}

precompile() { # publish dir, dll
  (cd "$1" && ASPNETCORE_ENVIRONMENT=Production Logging__LogLevel__Default=Warning dotnet "$2" --precompile="$1/aioros.plans.json" | grep Precompiled)
}

lumen() {
  local out=$ROOT/samples/Lumen/Lumen/bin/publish
  dotnet publish "$ROOT/samples/Lumen/Lumen/Lumen.csproj" -c Release -o "$out" -v q -nologo > /dev/null && precompile "$out" Lumen.dll
}
lumen_r2r() { # ReadyToRun: the SDK infers this machine's runtime identifier
  local out=$ROOT/samples/Lumen/Lumen/bin/publish-r2r
  dotnet publish "$ROOT/samples/Lumen/Lumen/Lumen.csproj" -c Release -p:PublishReadyToRun=true -o "$out" -v q -nologo > /dev/null && precompile "$out" Lumen.dll
}
lumen_aot() { # Native AOT has no Avalonia to precompile with, so its plans file is the JIT build's (target lumen first)
  local out=$ROOT/samples/Lumen/Lumen/bin/publish-aot
  dotnet publish "$ROOT/samples/Lumen/Lumen/Lumen.csproj" -c Release -p:Aot=true -o "$out" -v q -nologo > /dev/null \
    && cp "$ROOT/samples/Lumen/Lumen/bin/publish/aioros.plans.json" "$out/"
}
viewscale() {
  local out=$BENCH/ViewScale/bin/publish
  dotnet publish "$BENCH/ViewScale/ViewScale.csproj" -c Release -o "$out" -v q -nologo > /dev/null && precompile "$out" ViewScale.dll
}
blazor() { dotnet publish "$BENCH/LumenBlazor" -c Release -o "$BENCH/LumenBlazor/bin/publish" -v q -nologo > /dev/null; }
loadgen() { dotnet build "$BENCH/LoadGen" -c Release -v q -nologo > /dev/null; }
npm_install() { (cd "$1" && npm install --no-audit --no-fund > /dev/null); }
go_build() { # templ components compiled to Go, then one static binary
  (cd "$BENCH/go-lumen" && go tool templ generate -log-level error && CGO_ENABLED=0 go build -trimpath -o bin/go-lumen .)
}
leptos_build() { # the server (feature ssr) and the WebAssembly hydration bundle (feature hydrate), as cargo-leptos builds them
  (cd "$BENCH/leptos-lumen" && mkdir -p target && { cargo leptos build --release > target/build.log 2>&1 || { tail -40 target/build.log >&2; false; }; })
}
next_build() { (cd "$BENCH/next-lumen" && env $1 NEXT_TELEMETRY_DISABLED=1 node "$NEXT_BIN" build > /dev/null); }

DOTNET_INPUTS="$ROOT/src $ROOT/Directory.Build.props $ROOT/Directory.Packages.props"
NEXT_INPUTS="$BENCH/next-lumen $BENCH/lumen-shared"

for list in "${@:-lumen blazor next go leptos loadgen realtime viewscale}"; do
  for t in $list; do
    case $t in
      lumen) target lumen "$ROOT/samples/Lumen/Lumen/bin/publish/$STAMP" "$DOTNET_INPUTS $ROOT/samples/Lumen/Lumen" lumen ;;
      lumen-r2r) target lumen-r2r "$ROOT/samples/Lumen/Lumen/bin/publish-r2r/$STAMP" "$DOTNET_INPUTS $ROOT/samples/Lumen/Lumen" lumen_r2r ;;
      lumen-aot)
        target lumen "$ROOT/samples/Lumen/Lumen/bin/publish/$STAMP" "$DOTNET_INPUTS $ROOT/samples/Lumen/Lumen" lumen
        target lumen-aot "$ROOT/samples/Lumen/Lumen/bin/publish-aot/$STAMP" "$DOTNET_INPUTS $ROOT/samples/Lumen/Lumen" lumen_aot ;;
      viewscale) target viewscale "$BENCH/ViewScale/bin/publish/$STAMP" "$DOTNET_INPUTS $BENCH/ViewScale $BENCH/Directory.Build.props" viewscale ;;
      blazor) target blazor "$BENCH/LumenBlazor/bin/publish/$STAMP" "$BENCH/LumenBlazor $BENCH/lumen-shared $BENCH/Directory.Build.props $ROOT/Directory.Packages.props" blazor ;;
      loadgen) target loadgen "$BENCH/LoadGen/bin/Release/net10.0/$STAMP" "$BENCH/LoadGen $BENCH/Directory.Build.props" loadgen ;;
      realtime) target realtime "$BENCH/realtime/node_modules/$STAMP" "$BENCH/realtime/package.json $BENCH/realtime/package-lock.json" npm_install "$BENCH/realtime" ;;
      next)
        target next-deps "$BENCH/next-lumen/node_modules/$STAMP" "$BENCH/next-lumen/package.json $BENCH/next-lumen/package-lock.json" npm_install "$BENCH/next-lumen"
        target nextjs "$BENCH/next-lumen/.next/$STAMP" "$NEXT_INPUTS" next_build LUMEN_CACHE=0
        target nextjs-cache "$BENCH/next-lumen/.next-cache/$STAMP" "$NEXT_INPUTS" next_build LUMEN_CACHE=1 ;;
      go)
        target go-deps "$BENCH/go-lumen/node_modules/$STAMP" "$BENCH/go-lumen/package.json $BENCH/go-lumen/package-lock.json" npm_install "$BENCH/go-lumen"
        target go "$BENCH/go-lumen/bin/$STAMP" "$BENCH/go-lumen $BENCH/lumen-shared" go_build ;;
      leptos) target leptos "$BENCH/leptos-lumen/target/site/$STAMP" "$BENCH/leptos-lumen $BENCH/lumen-shared" leptos_build ;;
      *) echo "unknown target $t" >&2; exit 1 ;;
    esac
  done
done
