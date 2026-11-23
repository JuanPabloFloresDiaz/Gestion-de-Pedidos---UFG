#!/usr/bin/env bash
# Ejecuta `dotnet <args>` dentro del SDK de .NET 10 en Docker (no requiere instalar nada en el equipo).
#   ./scripts/dotnet-docker.sh build GestionPedidos.Linux.slnf
# Corre como root dentro del contenedor (igual que test-docker.sh) para compartir la caché NuGet del volumen sin problemas de permisos.
# No sirve para el proyecto Desktop (MAUI para Windows no compila en Linux).
set -euo pipefail
cd "$(dirname "$0")/.."
docker run --rm -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -v "$PWD":/repo -v gestionpedidos-nuget:/tmp/.nuget -e NUGET_PACKAGES=/tmp/.nuget/packages \
  -w /repo mcr.microsoft.com/dotnet/sdk:10.0 dotnet "$@"
