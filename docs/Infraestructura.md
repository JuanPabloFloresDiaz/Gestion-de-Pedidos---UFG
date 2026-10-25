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
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | `127.0.0.1:4391 → 1433` | Volumen nombrado, `healthcheck` con `sqlcmd`, `ACCEPT_EULA=Y`, `MSSQL_PID` y `MSSQL_MEMORY_LIMIT_MB` (1536 por defecto) |
| `db-init` | misma imagen de SQL Server | — | **Un solo uso**: crea la base `GestionPedidos` y los usuarios `pedidos_migrator` y `pedidos_app` ([BaseDatos.md §7](BaseDatos.md)) |
| `api` | `src/GestionPedidos.Api/Dockerfile` (`sdk:8.0` → `aspnet:8.0`) | `4390 → 8080` | Espera a `db-init`; al arrancar aplica las migraciones con `pedidos_migrator` y siembra al administrador; conecta con `pedidos_app` |
| `web` | `src/GestionPedidos.Web/Dockerfile` | `4392 → 8080` | Blazor Web App con la `Ui`; llama a `api` por la red interna (`http://api:8080`) |

* **Puertos del ecosistema del equipo**: `4390` (API), `4391` (SQL Server) y `4392` (web), los siguientes libres; verificados el 2026-10-01. Configurables (`${API_PORT:-4390}`).
* SQL Server se publica **solo en `127.0.0.1`** y nunca hacia la red.
* Contenedores con usuario **no root**.
* `.env.example` documenta: puertos (`API_PORT`, `SQL_PORT`, `WEB_PORT`), `MSSQL_SA_PASSWORD`, `PEDIDOS_APP_PASSWORD`, `PEDIDOS_MIGRATOR_PASSWORD`, `JWT_SIGNING_KEY`, `ADMIN_INITIAL_PASSWORD`, `MSSQL_PID` y `MSSQL_MEMORY_LIMIT_MB`. `.env` está en `.gitignore`.
* Swagger queda **activo** en el entorno local de Docker (`SWAGGER_HABILITADO`, por defecto `true`); en un despliegue real debe ir en `false`.

## 3. Scripts de Desarrollo

| Script | Qué hace |
|---|---|
| `scripts/up.sh [--limpio]` | Crea `.env` si falta, avisa si un puerto está ocupado, levanta SQL Server + API + web y espera a que estén sanos; `--limpio` borra antes los datos |
| `scripts/down.sh [--datos]` | Apaga el entorno; `--datos` también borra la base |
| `scripts/dotnet-docker.sh <args>` | `dotnet <args>` en el SDK 8 en Docker con caché de NuGet (ej. `build GestionPedidos.Linux.slnf`) |
| `scripts/ef-docker.sh <args>` | Comandos de EF Core en Docker (ej. `migrations add NombreDeLaMigracion`) |
| `scripts/test-docker.sh` | Todas las pruebas (unitarias, integración con SQL Server real, arquitectura) |
| `scripts/e2e-docker.sh` | Pruebas de extremo a extremo con Playwright; guarda capturas en `e2e/capturas/` |
| `scripts/generar-script-sql.sh` | Regenera `scripts/sql/GestionPedidos_esquema.sql` desde las migraciones |
| `scripts/db-backup.sh` / `db-restore.sh` | Respaldo FULL y restauración verificada ([RespaldoYEntrega.md](Especificaciones/RespaldoYEntrega.md)) |

> Testcontainers dentro de un contenedor necesita el socket de Docker montado y, en Docker Desktop, `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`.

## 4. Despliegue en un Servidor (Dokploy): pospuesto

### 4.1 Decisión
**Por ahora todo corre en local** (API, web y base de datos en Docker). Motivo: SQL Server —obligatorio por el enunciado— necesita **≥ 2 GB de RAM libres** y el servidor de Pablo tiene ~1,4 GiB libres (3,82 GiB en total, ~2,4 en uso, 8 GB de *swap*, que lo volvería muy lento). Pablo consultará al ingeniero si puede usarse un motor más liviano (MySQL/MariaDB o PostgreSQL); si lo autoriza, el despliegue se retoma ([BaseDatos.md §11](BaseDatos.md) detalla qué cambiaría).

### 4.2 Qué queda preparado
* Cuando se retome: un `docker-compose.dokploy.yml` con **`expose`** (nunca `ports`; el *Container Port* en Dokploy es el interno `8080`), detrás de Traefik con **TLS**, y secretos como variables de entorno de Dokploy. **Todavía no existe**: se crea a partir de `docker-compose.yml` cuando haya dónde desplegar.
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
