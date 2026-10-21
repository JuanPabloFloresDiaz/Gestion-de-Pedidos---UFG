# Gestión de Pedidos — UFG

Sistema de gestión de pedidos para la materia **Programación Orientada a Eventos** (Facultad de Ingeniería y Sistemas, UFG, ciclo 02-2026). Una **única interfaz Blazor** hospedada en un **escritorio .NET MAUI Blazor Hybrid** y en una **web Blazor Web App**, sobre una misma **API ASP.NET Core** y una base de datos **SQL Server**. Usa **delegados y eventos** de C# como mecanismo de notificación.

> **Estado:** documentación base lista; el código se inicia en la Fase 0 ([plan](docs/Implementacion.md)).

## Equipo

| Carnet | Nombre |
|---|---|
| FD100125 | Juan Pablo Flores Díaz |
| FC100426 | Anderson Rene Figueroa Coto |
| MJ100326 | Adiel Enrique Montepeque Juarez |
| AC101325 | Daniel Alexander Álvarez Chávez |

## Stack

C# 12 / **.NET 8** · Blazor (Razor Class Library) · .NET MAUI Blazor Hybrid (escritorio) · Blazor Web App (web) · ASP.NET Core · Entity Framework Core · SQL Server 2022 · JWT · Docker · GitHub Actions · VS Code.

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
| [Equipo/GuiaCI.md](docs/Equipo/GuiaCI.md) | Mini guía de integración continua |

Reglas para agentes de IA: [AGENTS.md](AGENTS.md).

## Ejecución (disponible desde la Fase 0)

La **API, la web y la base de datos** corren en **Docker**; el hospedaje de escritorio (MAUI) se ejecuta en Windows con VS Code, el SDK de .NET 8 y la carga de trabajo `maui`. Las instrucciones paso a paso se añadirán aquí al cerrar la Fase 0 ([Infraestructura.md](docs/Infraestructura.md)).
