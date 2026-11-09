#!/usr/bin/env bash
# Installs esbuild, pinned in its lockfile, which minifies the framework's browser scripts when Aioros.Targets.Ssr builds.
set -euo pipefail
cd "$(dirname "$0")/../src/Targets/Aioros.Targets.Ssr/Scripts"
npm ci --no-audit --no-fund --ignore-scripts
