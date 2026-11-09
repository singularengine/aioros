#!/usr/bin/env bash
# Installs the .NET SDK pinned in global.json into .dotnet/, where global.json's sdk.paths finds it first.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
version="$(sed -n 's/^ *"version": *"\([^"]*\)".*/\1/p' "$root/global.json" | head -1)"
script="$(mktemp)"
trap 'rm -f "$script"' EXIT
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$script"
bash "$script" --version "$version" --install-dir "$root/.dotnet" --no-path
