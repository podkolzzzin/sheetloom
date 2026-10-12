#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export NODE_SCRIPT="$PWD/src/main.js"
export TEMPLATES_DIR="${TEMPLATES_DIR:-$PWD/examples/basic}"
export CACHE_DIR="${CACHE_DIR:-$PWD/.cache}"
export NODE_ENV=production
exec dotnet run --project dotnet/Sheetloom.Service
