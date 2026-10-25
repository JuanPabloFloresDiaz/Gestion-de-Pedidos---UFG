#!/usr/bin/env bash
# Regenera scripts/sql/GestionPedidos_esquema.sql a partir de las migraciones de EF Core.
# Corre dentro de Docker (no requiere el SDK en el equipo): ./scripts/generar-script-sql.sh
set -euo pipefail
cd "$(dirname "$0")/.."
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -v "$PWD":/repo -w /repo mcr.microsoft.com/dotnet/sdk:8.0 bash -c '
    dotnet tool restore >/dev/null &&
    dotnet ef migrations script --idempotent \
      --project src/GestionPedidos.Infrastructure --startup-project src/GestionPedidos.Infrastructure \
      -o /tmp/esquema.sql >/dev/null &&
    { sed -n "1,/^$/p" scripts/sql/GestionPedidos_esquema.sql; cat /tmp/esquema.sql; } > /tmp/final.sql &&
    cp /tmp/final.sql scripts/sql/GestionPedidos_esquema.sql'
echo "Script regenerado: scripts/sql/GestionPedidos_esquema.sql"
