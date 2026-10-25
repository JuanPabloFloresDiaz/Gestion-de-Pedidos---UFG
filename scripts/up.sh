#!/usr/bin/env bash
# Levanta SQL Server + API + web con Docker y espera a que estén sanos.
#   ./scripts/up.sh            → arranca (crea .env desde .env.example si no existe)
#   ./scripts/up.sh --limpio   → borra antes los datos (volumen de SQL Server) y empieza desde cero
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f .env ] || { cp .env.example .env; echo "Se creó .env desde .env.example (solo valores de desarrollo)."; }
for p in 4390 4391 4392; do
  if ss -ltn 2>/dev/null | awk '{print $4}' | grep -q ":$p$" && ! docker compose ps -q 2>/dev/null | grep -q .; then
    echo "AVISO: el puerto $p ya está en uso por otro proceso. Cámbielo en .env (API_PORT, SQL_PORT, WEB_PORT)."; fi
done
[ "${1:-}" = "--limpio" ] && docker compose down -v
docker compose up -d --build --wait --wait-timeout 300
echo
echo "  Web:      http://localhost:${WEB_PORT:-4392}"
echo "  API:      http://localhost:${API_PORT:-4390}/swagger"
echo "  SQL:      localhost,${SQL_PORT:-4391} (usuario sa; clave en .env)"
echo "  Admin:    usuario 'admin' / ADMIN_INITIAL_PASSWORD de .env (pedirá cambiarla al ingresar)"
