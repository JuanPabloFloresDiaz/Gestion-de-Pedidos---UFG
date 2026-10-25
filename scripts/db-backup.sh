#!/usr/bin/env bash
# Respaldo FULL de la base GestionPedidos → dist/respaldo/GestionPedidos_FULL.bak  (entregable obligatorio)
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p dist/respaldo
docker compose exec -T sqlserver bash -c '
  mkdir -p /var/opt/mssql/backup &&
  /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q \
    "BACKUP DATABASE [GestionPedidos] TO DISK = N'"'"'/var/opt/mssql/backup/GestionPedidos_FULL.bak'"'"' WITH INIT, FORMAT, CHECKSUM, STATS = 25"'
docker compose cp sqlserver:/var/opt/mssql/backup/GestionPedidos_FULL.bak dist/respaldo/GestionPedidos_FULL.bak
echo "Respaldo creado: dist/respaldo/GestionPedidos_FULL.bak"
