#!/usr/bin/env bash
# Pruebas de extremo a extremo (Playwright) en Docker contra el entorno levantado con docker compose.
#   ./scripts/up.sh && ./scripts/e2e-docker.sh
# Variables opcionales: BASE_URL (web), API_URL, E2E_ADMIN_INICIAL, E2E_ADMIN_CLAVE.
set -euo pipefail
cd "$(dirname "$0")/../e2e"
docker run --rm --user "$(id -u):$(id -g)" --ipc=host \
  --add-host=host.docker.internal:host-gateway \
  -e HOME=/tmp -e npm_config_cache=/tmp/.npm \
  -e BASE_URL="${BASE_URL:-http://host.docker.internal:4392}" \
  -e API_URL="${API_URL:-http://host.docker.internal:4390}" \
  -e E2E_ADMIN_INICIAL -e E2E_ADMIN_CLAVE \
  -v "$PWD":/e2e -w /e2e mcr.microsoft.com/playwright:v1.49.1-noble \
  bash -c 'npm install --no-audit --no-fund >/dev/null && npx playwright test'
