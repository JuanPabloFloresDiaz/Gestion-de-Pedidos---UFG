# 🏗️ Infraestructura y Despliegue - Gestión de Pedidos (UFG)

## 1. Principio: API, Web y Base de Datos en Docker

La **API, la web y SQL Server** corren en contenedores; el único requisito para ellos es **Docker (Desktop o Engine) con Compose v2**. El **hospedaje de escritorio (.NET MAUI Blazor Hybrid)** se desarrolla y ejecuta en **Windows** con VS Code, el SDK de .NET 8 y la carga de trabajo `maui` (§5), porque MAUI para Windows no se compila en contenedores Linux.

## 2. Topología Local (`docker-compose.yml`)

```
 Equipo del desarrollador (Windows)                       Docker
 ┌───────────────────────────────┐        ┌──────────────────────────────────────────────┐
 │ Desktop (MAUI Hybrid) dotnet run│──HTTP─►│ api :4390 ──red interna──► sqlserver          │
 └───────────────────────────────┘        │                             (127.0.0.1:4391)  │
 Navegador ───────────────────────HTTP───►│ web :4392 ──► api                             │
                                          └──────────────────────────────────────────────┘
```

| Servicio | Imagen / build | Puerto host → contenedor | Notas |
|---|---|---|---|
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | `127.0.0.1:4391 → 1433` | Volumen nombrado, `healthcheck` con `sqlcmd`, `ACCEPT_EULA=Y`, `MSSQL_PID` y `MSSQL_MEMORY_LIMIT_MB` configurables |
| `api` | Dockerfile multi-etapa (`sdk:8.0` → `aspnet:8.0`) | `4390 → 8080` | Espera a `sqlserver` *healthy*; aplica migraciones solo en `Development` |
| `web` | Dockerfile multi-etapa (`sdk:8.0` → `aspnet:8.0`) | `4392 → 8080` | Blazor Web App con la `Ui`; llama a `api` por la red interna. Se añade con la Fase Web |

* **Puertos del ecosistema del equipo**: `4390` (API), `4391` (SQL Server) y `4392` (web), los siguientes libres; verificados el 2026-10-01. Configurables (`${API_PORT:-4390}`).
* SQL Server se publica **solo en `127.0.0.1`** y nunca hacia la red.
* Contenedores con usuario **no root**.
* `.env.example` documenta: `MSSQL_SA_PASSWORD`, `PEDIDOS_APP_PASSWORD`, `PEDIDOS_MIGRATOR_PASSWORD`, `JWT_SIGNING_KEY`, `ADMIN_INITIAL_PASSWORD`, `MSSQL_PID`, `MSSQL_MEMORY_LIMIT_MB`.

## 3. Scripts de Desarrollo

| Script | Qué hace |
|---|---|
| `scripts/dotnet-docker.sh <args>` | `dotnet <args>` en `mcr.microsoft.com/dotnet/sdk:8.0` montando el repo y una caché de NuGet; usa `--user "$(id -u):$(id -g)"` |
| `scripts/ef-docker.sh <args>` | Igual, con `dotnet-ef` 8, conectado a la red de Compose para migraciones |
| `scripts/test-docker.sh` | Pruebas unitarias, de integración (Testcontainers con el socket de Docker) y de arquitectura |
| `scripts/db-backup.sh` / `db-restore.sh` | Respaldo FULL y restauración verificada ([RespaldoYEntrega.md](Especificaciones/RespaldoYEntrega.md)) |

> Testcontainers dentro de un contenedor necesita el socket de Docker montado y, en Docker Desktop, `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`.

## 4. Despliegue en un Servidor (Dokploy): pospuesto

### 4.1 Decisión
**Por ahora todo corre en local** (API, web y base de datos en Docker). Motivo: SQL Server —obligatorio por el enunciado— necesita **≥ 2 GB de RAM libres** y el servidor de Pablo tiene ~1,4 GiB libres (3,82 GiB en total, ~2,4 en uso, 8 GB de *swap*, que lo volvería muy lento). Pablo consultará al ingeniero si puede usarse un motor más liviano (MySQL/MariaDB o PostgreSQL); si lo autoriza, el despliegue se retoma ([BaseDatos.md §11](BaseDatos.md) detalla qué cambiaría).

### 4.2 Qué queda preparado
* `docker-compose.dokploy.yml` con **`expose`** (nunca `ports`; el *Container Port* en Dokploy es el interno `8080`), detrás de Traefik con **TLS**, y secretos como variables de entorno de Dokploy.
* La base se configura solo con `ConnectionStrings__Default`: cambiar de lugar la base no toca el código.

### 4.3 Caminos si cambian las condiciones
| Camino | Cuándo |
|---|---|
| Otro motor liviano (MySQL/MariaDB o PostgreSQL) | Si el ingeniero lo autoriza |
| SQL Server Express en contenedor (`MSSQL_PID=Express`) | Si el servidor llega a tener ≥ 2–3 GB de RAM libres |
| [Azure SQL Database (oferta gratuita)](https://learn.microsoft.com/azure/azure-sql/database/free-offer) | Si se quiere mantener SQL Server sin RAM en el servidor; requiere cuenta de Azure |

### 4.4 Otros puntos
* **Respaldos**: `BACKUP DATABASE` y prueba de restauración ([RespaldoYEntrega.md](Especificaciones/RespaldoYEntrega.md)).
* **Licencia**: la edición *Developer* no es para producción ([BaseLegal.md](BaseLegal.md)).
* **Soporte de .NET 8** termina el 10/11/2026 ([Tecnologia.md](Tecnologia.md)).

## 5. Hospedaje de Escritorio (MAUI Blazor Hybrid)

* Se desarrolla y ejecuta en **Windows 10/11** con **VS Code** (C# Dev Kit + extensión .NET MAUI), el **SDK de .NET 8** y `dotnet workload install maui`: `dotnet build -f net8.0-windows10.0.19041.0 src/GestionPedidos.Desktop` y `dotnet run -f net8.0-windows10.0.19041.0 --project src/GestionPedidos.Desktop`. **La experiencia completa con VS Code se valida en la Fase 0** ([Tecnologia.md §3.1](Tecnologia.md)).
* Para generar el ejecutable: `dotnet publish -f net8.0-windows10.0.19041.0 -c Release` en Windows (con empaquetado según elija el equipo en la Fase 5).
* Configura la URL de la API en `appsettings.json` (`http://localhost:4390` en local). **Nunca** se conecta a SQL Server directamente.
* Como la `Ui` es la misma, casi todo se desarrolla y se prueba en el navegador con el hospedaje web (Docker); el escritorio se verifica antes de cada entrega.
* **Defensa (12/11/2026)**: todo **local** —API, web y base de datos en Docker, y el `.exe` en la misma máquina Windows—, sin depender de internet. Se ensaya el recorrido completo con antelación.

## 6. Observabilidad Mínima

* `GET /health` (liveness) y `GET /health/ready` (verifica SQL Server) en la API; se usan en los *healthchecks*.
* Logs estructurados a `stdout`, sin datos sensibles.
