#!/usr/bin/env bash
# Restaura el respaldo en una base NUEVA (GestionPedidos_Restaurada) y verifica que los conteos de filas coinciden.
# Genera dist/respaldo/RestauracionVerificada.txt como evidencia. Termina con error si algo no coincide.
set -euo pipefail
cd "$(dirname "$0")/.."
BAK="${1:-dist/respaldo/GestionPedidos_FULL.bak}"
[ -f "$BAK" ] || { echo "No existe $BAK (ejecute primero ./scripts/db-backup.sh)"; exit 1; }
docker compose exec -T sqlserver mkdir -p /var/opt/mssql/backup
docker compose cp "$BAK" sqlserver:/var/opt/mssql/backup/restaurar.bak
# `docker cp` deja el archivo como root y SQL Server corre como 'mssql': sin esto, "Access is denied".
docker compose exec -T -u root sqlserver bash -c 'chown mssql:root /var/opt/mssql/backup/restaurar.bak && chmod 640 /var/opt/mssql/backup/restaurar.bak'
docker compose exec -T sqlserver bash -c '
  S="/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P $MSSQL_SA_PASSWORD"
  $S -Q "RESTORE VERIFYONLY FROM DISK = N'"'"'/var/opt/mssql/backup/restaurar.bak'"'"' WITH CHECKSUM"
  $S -Q "IF DB_ID(N'"'"'GestionPedidos_Restaurada'"'"') IS NOT NULL DROP DATABASE GestionPedidos_Restaurada"
  $S -Q "RESTORE DATABASE [GestionPedidos_Restaurada] FROM DISK = N'"'"'/var/opt/mssql/backup/restaurar.bak'"'"' WITH
         MOVE N'"'"'GestionPedidos'"'"' TO N'"'"'/var/opt/mssql/data/GestionPedidos_Restaurada.mdf'"'"',
         MOVE N'"'"'GestionPedidos_log'"'"' TO N'"'"'/var/opt/mssql/data/GestionPedidos_Restaurada_log.ldf'"'"', STATS = 25"
  for t in Roles Permisos RolesPermisos Usuarios Departamentos Ciudades Clientes Productos Pedidos DetallesPedido Actividades; do
    a=$($S -h -1 -W -d GestionPedidos -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.$t" | tr -d "\r ")
    b=$($S -h -1 -W -d GestionPedidos_Restaurada -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.$t" | tr -d "\r ")
    if [ "$a" = "$b" ]; then echo "OK   $t: $a filas"; else echo "FALLA $t: original=$a restaurada=$b"; exit 1; fi
  done' | tee dist/respaldo/RestauracionVerificada.txt
echo "Restauración verificada → dist/respaldo/RestauracionVerificada.txt"
