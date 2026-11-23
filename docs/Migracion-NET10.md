# Plan de migración a .NET 10 (LTS)

**Estado:** planificado, sin ejecutar. Reemplaza al §5 de [Tecnologia.md](Tecnologia.md) (que quedaba "a decidir tras consultar al ingeniero"): el proyecto ya no se usa en la universidad y se mantiene como proyecto de portafolio, así que la decisión es **migrar**.

## 1. Por qué .NET 10 y no otra versión

* .NET 8 terminó su soporte el 10/11/2026: hoy no recibe parches de seguridad.
* .NET 10 es la **LTS vigente** (soporte hasta noviembre de 2028). .NET 11 ya salió, pero es STS (soporte corto); no conviene para un proyecto que se quiere estable.
* Todo el toolchain es Docker (no hay SDK 10 en el host): la imagen `mcr.microsoft.com/dotnet/sdk:10.0` existe y se usa tal cual.

## 2. Alcance

Un solo cambio de versión **sin cambiar funcionalidad ni esquema**. Fuera de alcance (van después, cada uno en su commit/rama): `.slnx`, reemplazar Swashbuckle por `Microsoft.AspNetCore.OpenApi`, xUnit v3, nuevas características de C# 14.

## 3. Inventario de lo que cambia

| Dónde | Hoy | Destino |
|---|---|---|
| `global.json` | SDK `8.0.423` | SDK `10.0.x` (último parche estable), `rollForward: latestFeature` |
| `Directory.Build.props` | `net8.0`, `LangVersion 12.0` | `net10.0`, `LangVersion 14.0` (o quitar la línea) |
| `GestionPedidos.Desktop.csproj` | `net8.0-android`, `net8.0-windows10.0.19041.0`, `$(MauiVersion)` | `net10.0-android`, `net10.0-windows10.0.19041.0`, MAUI `10.0.x` |
| `Directory.Packages.props` | EF Core / JwtBearer / Identity / Http / Mvc.Testing / Components.Web `8.0.x` | `10.0.15` (último estable 10.0 hoy) |
| | `Microsoft.Extensions.Logging.Debug` `8.0.1` (en Desktop) | `10.0.15` |
| | `Swashbuckle.AspNetCore` `6.9.0` | `10.x` (revisar cambios de `Microsoft.OpenApi` 2.x en `AddSwaggerGen`) |
| | `Microsoft.Extensions.TimeProvider.Testing` `8.10.0` | `10.x` |
| | `bunit` `1.34.0` | `2.x` (cambios de API; ver riesgos) |
| | `Testcontainers.MsSql` `3.10.0`, `Respawn` `6.2.1`, `AWSSDK.S3` `3.7.x` | subir al último que soporte .NET 10 (4.x / 7.x / 4.x) |
| | `QuestPDF`, `ClosedXML`, `Blazicons.Lucide`, `NetArchTest.Rules`, `xunit` | verificar compatibilidad; probablemente sin cambio |
| `.config/dotnet-tools.json` | `dotnet-ef 8.0.11` | `10.0.15` |
| Dockerfiles (`Api`, `Web`) | `sdk:8.0` / `aspnet:8.0` | `sdk:10.0` / `aspnet:10.0` |
| `scripts/*-docker.sh` (4) | `sdk:8.0` | `sdk:10.0` |
| `docker-compose.yml` | — | revisar `healthcheck` y la imagen de SQL Server (no depende de .NET) |
| Comentarios y README | "ASP.NET Core 8", `.NET 8` | actualizar |

Las versiones exactas se confirman al ejecutar (`dotnet list package --outdated` dentro del contenedor); los números de arriba son los últimos estables consultados en NuGet el 23/11/2026.

## 4. Riesgos y qué mirar

1. **Swashbuckle 10 / Microsoft.OpenApi 2.x**: cambió el modelo (`OpenApiSecurityScheme`, referencias). Es donde más probable es que `Program.cs` (líneas ~97–128) deje de compilar. Si se complica, se separa en un commit propio.
2. **bUnit 1.x → 2.x**: cambios en `TestContext`/`Render`. Afecta a `GestionPedidos.Ui.Tests`. Plan B: dejar bUnit en la última 1.x compatible hasta que el resto esté verde.
3. **TreatWarningsAsErrors**: las advertencias nuevas (analizadores de .NET 10, APIs obsoletas) rompen la compilación; se corrigen una por una, **sin** relajar la regla.
4. **EF Core 10**: ver el snapshot de migraciones. Criterio: `dotnet ef migrations has-pending-model-changes` debe dar "sin cambios"; si EF 10 quiere una migración nueva, se revisa su SQL y se decide (no se acepta a ciegas). Probar migraciones contra SQL Server real (Testcontainers ya lo hace).
5. **JwtBearer 10**: pasó a `JsonWebToken` (`Microsoft.IdentityModel.JsonWebTokens`) por defecto; revisar pruebas de autenticación y los *claims* (nombres de roles/permisos).
6. **MAUI 10**: el escritorio Windows y Android **no se pueden compilar en Docker/Linux**; la validación del `Desktop` queda como paso manual en Windows con Visual Studio 2026 (o la carga de trabajo `maui` 10). Se deja anotado como pendiente verificable, no como "hecho".
7. **Rama divergente**: hay ramas del equipo sin integrar (`feat-productos-crud`, `feat/reportes`, `feat/maui-movil`…). Se migra desde `main`; si alguna se va a retomar, se rebasa después (los cambios son en archivos de configuración, el conflicto es pequeño).

## 5. Pasos (un commit por paso, en la rama `chore/net10`)

1. **Línea base**: en `main`, correr `./scripts/test-docker.sh` y `./scripts/e2e-docker.sh` y anotar el resultado (número de pruebas verdes). Sin base verde no se migra.
2. **Toolchain**: `global.json`, `dotnet-tools.json`, imágenes en Dockerfiles y `scripts/*-docker.sh` a 10.0.
3. **Framework**: `Directory.Build.props` y `TargetFrameworks` del `Desktop` a `net10.0*`.
4. **Paquetes de Microsoft** (EF, JwtBearer, Identity, Http, Hosting, Components.Web, Mvc.Testing, Logging.Debug) a `10.0.x`. Compilar, corregir.
5. **Swashbuckle** a 10.x. Compilar; abrir `/swagger` y comprobar que sigue mostrando el esquema de seguridad Bearer.
6. **Paquetes de terceros** (AWSSDK.S3, QuestPDF, ClosedXML, Blazicons, Testcontainers, Respawn): subir uno por uno solo si hace falta o si hay aviso de vulnerabilidad (`dotnet list package --vulnerable`).
7. **Pruebas**: `TimeProvider.Testing`, bUnit y xUnit; toda la batería en verde.
8. **EF**: `has-pending-model-changes`, regenerar `scripts/sql/GestionPedidos_esquema.sql` y comparar con el actual (debe ser equivalente).
9. **Docker y E2E**: `./scripts/up.sh`, humo manual (login, catálogo, tienda, un reporte PDF/Excel, imágenes desde Garage) y `./scripts/e2e-docker.sh`.
10. **Documentación**: actualizar README, `Tecnologia.md`, `Infraestructura.md`, `AGENTS.md` y `Principios.md` (menciones a .NET 8) y marcar este plan como ejecutado.
11. **Desktop (manual en Windows)**: compilar `net10.0-windows10.0.19041.0` y abrir la app contra la API. Registrar el resultado aparte.

## 6. Criterios de aceptación

* `./scripts/test-docker.sh`: **mismo número de pruebas** que la línea base y todas en verde, sin advertencias.
* `./scripts/e2e-docker.sh`: 11/11 en verde.
* `dotnet list package --vulnerable --include-transitive` sin resultados.
* El esquema es el mismo que con .NET 8 (`has-pending-model-changes` sin cambios y script SQL equivalente: solo cambian el formato que genera EF 10 y la versión de producto).
* Las imágenes Docker finales arrancan con `aspnet:10.0` como usuario no root, como hoy.
* Ninguna mención a `net8.0` / `.NET 8` fuera del historial (`grep -rn "net8\|\.NET 8\|:8\.0"`), salvo la nota histórica de este documento.

## 7. Reversibilidad

Todo vive en una rama; `main` sigue en .NET 8 hasta que los criterios de la sección 6 pasen. Se fusiona con un PR (no a `main` directo).

## 8. Esfuerzo estimado

Medio día si Swashbuckle y bUnit cooperan; un día completo si hay que adaptar ambos. El `Desktop` (paso 11) depende de tener acceso a un Windows.

## 9. Línea base en .NET 8 (paso 1, 23/11/2026, rama `chore/net10` desde `main` en `53551ff`)

| Batería | Resultado |
|---|---|
| Domain | 99 verdes |
| Application | 256 verdes |
| Architecture | 8 verdes |
| Ui (bUnit) | 132 verdes |
| Infrastructure (SQL Server real) | 59 verdes |
| Api | 136 verdes |
| **Total unitarias/integración** | **690 verdes, 0 fallos** |
| E2E (Playwright, entorno limpio con `up.sh --limpio`) | **9 verdes y 2 fallos previos; tras corregir las pruebas (commit `b3a0dad`): 11 verdes, 0 fallos** |

Los 2 fallos iniciales de E2E **ya existían antes de migrar** (las pruebas estaban desactualizadas respecto a la aplicación) y se corrigieron:
* `administracion.spec.ts:172` espera la pantalla "Apartado en progreso" al abrir Clientes, pero Clientes ya está implementado.
* `administracion.spec.ts:46` no escribía la justificación (10 a 500 caracteres) que ahora exige el diálogo de baja de un usuario.

**Línea base definitiva: 690 pruebas + 11/11 E2E.** Tras migrar debe dar exactamente lo mismo.

Nota de entorno: el E2E depende de una base limpia (`./scripts/up.sh --limpio`); con un volumen de ejecuciones anteriores fallan 4 en vez de 2.

Mejoras de buenas prácticas: ver [Auditoria-BuenasPracticas-NET10.md](Auditoria-BuenasPracticas-NET10.md). Advertencias vistas en los registros de la API: `QuerySplittingBehavior` sin configurar en consultas con varias colecciones, y claves de Data Protection sin persistir fuera del contenedor.

## 10. Avance (23/11/2026, solo la parte web)

| Paso | Estado | Notas |
|---|---|---|
| 1 Línea base | Hecho | 690 + 11/11 |
| 2 Toolchain | Hecho | SDK 10.0.404, `dotnet-ef` 10.0.15, imágenes 10.0 |
| 3 Framework | Hecho | `net10.0`; `LangVersion` ya no se fija (C# 14 por defecto) |
| 4 Paquetes Microsoft | Hecho | 10.0.15; **Testcontainers.MsSql 3.10 → 4.17** (SSH.NET vulnerable) y 3 pruebas adaptadas |
| 5 Swashbuckle | Pendiente | 6.9 funciona tal cual en .NET 10; subir a 10.x es opcional |
| 6 Terceros | Hecho (sin cambios) | `dotnet list package --vulnerable --include-transitive`: **0 vulnerables** en los 13 proyectos de `GestionPedidos.Linux.slnf` (23/11/2026). No se subió nada más |
| 7 Pruebas (bUnit, xUnit) | Parcial | Compilan y pasan con las versiones actuales; bUnit 2.x es opcional |
| 8 EF | Hecho | Modelo sin cambios; script SQL regenerado (equivalente) e incluye la migración `TelefonoSecundarioCliente`, que faltaba |
| 9 Docker + E2E | Hecho | API en ASP.NET Core 10.0.15; **690 pruebas y E2E 11/11**; Swagger (62 rutas, esquema Bearer), imágenes de Garage, reportes PDF/Excel y correos (MailHog) verificados a mano |
| 10 Documentación | Hecho | README, `AGENTS.md`, `Tecnologia.md` (§5 reescrito), `Infraestructura.md`, `Analisis.md`, guías de `Equipo/`. Lo histórico de .NET 8 queda marcado; escritorio y móvil, anotados como pendientes de validar |
| 11 Desktop (Windows) | Pendiente | Se prueba luego, en Windows |

**Dos cambios de comportamiento de .NET 10 que no cubrían las pruebas unitarias** (solo el E2E los detectó):
1. `_framework/blazor.web.js` ya no lo sirve `UseStaticFiles()`: hay que usar `MapStaticAssets()`.
2. Un `dotnet restore` hecho solo con los `.csproj` (capa de caché del Dockerfile) no incluye los activos de Blazor del SDK 10; el `publish` de la Web no puede llevar `--no-restore`.

**Escaneo de paquetes (23/11/2026):**
* Vulnerables (directos y transitivos): ninguno. El `Desktop` no entra en el escaneo (MAUI no restaura en Linux); se escanea en Windows.
* Obsoletos: `xunit` 2.9.2 (marcado *Legacy*, alternativa `xunit.v3`) en `Ui.Tests` y `Architecture.Tests`. Sin urgencia.
* Desactualizados: los paquetes de Microsoft tienen 11.0.0 disponible (.NET 11, soporte corto): **se queda en 10.0.x a propósito**. Menores sin aplicar: `AWSSDK.S3` 4.x (salto de versión mayor), `Blazicons.Lucide` 3.2.13, `Microsoft.NET.Test.Sdk` 18.x, `xunit.runner.visualstudio` 4.x, `Swashbuckle` 11.x (la 6.9 funciona).

## 11. Puntos A de la auditoría (aplicados en la misma rama)

S1 (rehash de contraseñas), S2 (IP real tras un proxy), S3 (claves de Data Protection), D1 (SplitQuery), P2 (política de NuGetAudit) y P3 (analizadores) están hechos; el detalle y una limitación de S2 en [Auditoria-BuenasPracticas-NET10.md](Auditoria-BuenasPracticas-NET10.md). Resultado: **702 pruebas** (12 nuevas) en verde, build sin advertencias, **E2E 11/11** con el stack reconstruido desde cero, sin advertencias de EF.
