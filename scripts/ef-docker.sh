#!/usr/bin/env bash
# Comandos de Entity Framework Core (migraciones) en Docker.
#   ./scripts/ef-docker.sh migrations add NombreDeLaMigracion
#   ./scripts/ef-docker.sh migrations list
# Una sola persona a la vez genera migraciones (si no, chocan en el ModelSnapshot).
set -euo pipefail
cd "$(dirname "$0")/.."
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -v "$PWD":/repo -v gestionpedidos-nuget:/tmp/.nuget -e NUGET_PACKAGES=/tmp/.nuget/packages \
  -w /repo mcr.microsoft.com/dotnet/sdk:8.0 bash -c '
    dotnet tool restore >/dev/null && dotnet ef "$@" \
      --project src/GestionPedidos.Infrastructure --startup-project src/GestionPedidos.Infrastructure \
      --output-dir Persistence/Migrations' _ "$@"
