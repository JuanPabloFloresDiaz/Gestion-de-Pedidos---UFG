#!/usr/bin/env bash
# Apaga el entorno. Con --datos también borra la base de datos (volumen).
set -euo pipefail
cd "$(dirname "$0")/.."
if [ "${1:-}" = "--datos" ]; then docker compose down -v; else docker compose down; fi
