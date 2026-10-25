#!/usr/bin/env bash
# Corre TODAS las pruebas (unitarias, de integración con SQL Server real, de arquitectura) en Docker.
# Las de integración usan Testcontainers: necesitan el socket de Docker y la IP del anfitrión.
#   ./scripts/test-docker.sh                  → todo
#   ./scripts/test-docker.sh --filter "FullyQualifiedName~Usuarios"
set -euo pipefail
cd "$(dirname "$0")/.."
docker run --rm -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -v /var/run/docker.sock:/var/run/docker.sock -v "$PWD":/repo -w /repo \
  -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal --add-host=host.docker.internal:host-gateway \
  -v gestionpedidos-nuget:/tmp/.nuget -e NUGET_PACKAGES=/tmp/.nuget/packages \
  mcr.microsoft.com/dotnet/sdk:8.0 bash -c '
    set -e
    for t in Domain Application Architecture Ui Infrastructure Api; do
      echo "=== GestionPedidos.$t.Tests"; dotnet test tests/GestionPedidos.$t.Tests "$@" --nologo -v q
    done' _ "$@"
