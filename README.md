# Gestión de Pedidos — UFG

Sistema de gestión de pedidos para la materia **Programación Orientada a Eventos** (Facultad de Ingeniería y Sistemas, UFG, ciclo 02-2026). Una **única interfaz Blazor** hospedada en un **escritorio .NET MAUI Blazor Hybrid** y en una **web Blazor Web App**, sobre una misma **API ASP.NET Core** y una base de datos **SQL Server**. Usa **delegados y eventos** de C# como mecanismo de notificación.

> **Estado:** Fases 0, 0.5 y 1 terminadas (esqueleto, 11 tablas con migraciones, autenticación, usuarios, roles y permisos). Siguen Clientes, Productos y Pedidos ([plan](docs/Implementacion.md), [tareas](docs/Equipo/Tareas.md)).

## Equipo

| Carnet | Nombre |
|---|---|
| FD100125 | Juan Pablo Flores Díaz |
| FC100426 | Anderson Rene Figueroa Coto |
| MJ100326 | Adiel Enrique Montepeque Juarez |
| AC101325 | Daniel Alexander Álvarez Chávez |

## Stack

C# 12 / **.NET 8** · Blazor (Razor Class Library) · .NET MAUI Blazor Hybrid (escritorio) · Blazor Web App (web) · ASP.NET Core · Entity Framework Core · SQL Server 2022 · JWT · Docker · Playwright · VS Code.

## Documentación (`docs/`)

| Documento | Contenido |
|---|---|
| [Analisis.md](docs/Analisis.md) | Requerimientos oficiales, rúbrica e interpretaciones |
| [Tecnologia.md](docs/Tecnologia.md) | Stack y decisiones (incluye por qué Blazor Hybrid + API) |
| [Arquitectura.md](docs/Arquitectura.md) | Capas, modelo de dominio, eventos y flujos |
| [Estructura.md](docs/Estructura.md) | Árbol de la solución y nomenclatura |
| [BaseDatos.md](docs/BaseDatos.md) | Esquema, restricciones, migraciones y respaldo |
| [Infraestructura.md](docs/Infraestructura.md) | Docker, scripts, despliegue y cliente de escritorio |
| [Seguridad.md](docs/Seguridad.md) | Autenticación, autorización, conexiones seguras |
| [Principios.md](docs/Principios.md) | SOLID, ACID, DRY, KISS, YAGNI y convenciones |
| [Implementacion.md](docs/Implementacion.md) | Fases, cronograma y trabajo en equipo |
| [Especificaciones/](docs/Especificaciones/README.md) | Una spec por módulo (clientes, productos, pedidos, portal web…) |
| [Equipo/PautasCodigoEventos.md](docs/Equipo/PautasCodigoEventos.md) | **Pautas de código para delegados y eventos (leer antes de programar)** |
| [Equipo/Tareas.md](docs/Equipo/Tareas.md) | Tablero de tareas por módulo |
| [Equipo/PrimerosPasos.md](docs/Equipo/PrimerosPasos.md) | **Cómo levantar, probar y construir un módulo nuevo** |

Reglas para agentes de IA: [AGENTS.md](AGENTS.md).

## Ejecución rápida

```bash
./scripts/up.sh                 # SQL Server + API + web en Docker (crea .env la primera vez)
./scripts/test-docker.sh        # todas las pruebas (unitarias, integración con SQL Server real, arquitectura)
./scripts/e2e-docker.sh         # extremo a extremo en navegador (Playwright)
```

| Qué | Dónde |
|---|---|
| Web | http://localhost:4392 |
| API / Swagger | http://localhost:4390/swagger |
| SQL Server | `localhost,4391` |
| Administrador inicial | `admin` / `ADMIN_INITIAL_PASSWORD` de `.env` (pide cambiarla al ingresar) |

**Escritorio** (Windows, no corre en Docker): con **Visual Studio 2022** sigan el [paso a paso](docs/Equipo/PrimerosPasos.md#41-paso-a-paso-con-visual-studio-2022-o-2026); desde consola: `dotnet run -f net8.0-windows10.0.19041.0 --project src/GestionPedidos.Desktop`. **Base de datos**: [cómo conectarse](docs/Equipo/PrimerosPasos.md#21-conectarse-a-la-base-de-datos-dbeaver-azure-data-studio-ssms).

**Script SQL con todas las tablas**: [`scripts/sql/GestionPedidos_esquema.sql`](scripts/sql/GestionPedidos_esquema.sql).
