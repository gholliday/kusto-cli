#!/usr/bin/env sh
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
dotnet run --project "$SCRIPT_DIR/src/Kusto.Cli.Mcp/" -- "$@"
