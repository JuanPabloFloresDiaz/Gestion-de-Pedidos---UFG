# 🧰 Evaluación Tecnológica - Gestión de Pedidos (UFG)

> **Versión del proyecto: .NET 10 (LTS).** Durante la materia el ingeniero exigió .NET 8; terminada la materia el proyecto se **migró a .NET 10** (soporte hasta noviembre de 2028). El detalle, los pasos y lo aprendido están en [Migracion-NET10.md](Migracion-NET10.md). Las versiones se anclan en `global.json`, `Directory.Build.props` y `Directory.Packages.props`.

## 1. Matriz de Stack

| Capa | Tecnología | Justificación |
|---|---|---|
| Lenguaje / runtime | **C# 14 / .NET 10 (LTS)** | LTS vigente; antes .NET 8 por exigencia de la materia |
| Interfaz (única) | **Blazor** en una **Razor Class Library** (`Ui`) | Una sola implementación de todas las pantallas, para escritorio y web |
| Hospedaje de escritorio | **.NET MAUI Blazor Hybrid** (target Windows) | Aplicación de escritorio de la familia .NET que reutiliza la `Ui` |
| Hospedaje web | **Blazor Web App** (.NET 10, render interactivo de servidor) | La misma `Ui` en el navegador; se despliega en Docker |
| Patrón de interfaz | **Página (.razor) + ViewModel** con eventos | Marcado sin lógica; el ViewModel se prueba sin pantalla |
| Componentes visuales | **Kit propio** (`Ui/Comun/Componentes`) + tema CSS central | Se evaluó MudBlazor y se descartó: el kit propio es pequeño, sin dependencias, se personaliza en un solo archivo (`tema.css`) y evita problemas de versión con .NET 8 |
| Íconos | **Lucide** vía `Blazicons.Lucide` (1 900+ íconos SVG de trazo) detrás del componente `Icono` | Se compararon las librerías para Blazor: Material Design Icons (~7 000, muy completa pero de aspecto más pesado), Fluent UI (grande, estilo Microsoft), Font Awesome (la gratuita es limitada) y Lucide (moderna, consistente, la de moda en interfaces actuales y con todo lo que necesita una tienda). Son SVG incrustados: funcionan sin internet y en el escritorio. Cambiar de librería es tocar `Icono.razor` y `TiendaVisual.cs`. **Sin emojis en la interfaz**: se ven distintos en cada sistema |
| Almacén de imágenes | **Garage** (compatible con S3) + `AWSSDK.S3` 3.7 | Ligero, de un solo binario y software libre; las imágenes no se guardan en la base ni en el sitio. Se usa el cliente 3.7 porque el 4.x añade sumas de comprobación que no todos los servicios S3 aceptan |
| API | **ASP.NET Core 10** (Minimal APIs agrupadas por módulo) | Un solo backend para los dos clientes |
| ORM | **Entity Framework Core 10** (SQL Server) | Requisito del equipo; migraciones y consultas parametrizadas por construcción |
| Base de datos | **SQL Server 2022** (imagen oficial Linux) | Enunciado |
| Autenticación | **JWT** de 60 min (`Microsoft.AspNetCore.Authentication.JwtBearer` 8.x), sin *refresh tokens* | Simplicidad; ver [Seguridad.md](Seguridad.md) |
| Hash de contraseñas | `PasswordHasher<T>` (`Microsoft.Extensions.Identity.Core`, PBKDF2) | Estándar mantenido por Microsoft |
| Documentación de la API | **Swashbuckle.AspNetCore** (OpenAPI/Swagger UI) solo en `Development` | Se mantiene Swashbuckle (funciona en .NET 10); el generador integrado `Microsoft.AspNetCore.OpenApi` es una alternativa opcional |
| Validación | Reglas propias componibles (delegados): `Validaciones` en la API y `Validacion` en la interfaz | Sin dependencias (YAGNI); la API siempre vuelve a validar |
| Cliente HTTP | `HttpClient` tipado vía `IHttpClientFactory` | Sin librerías extra (YAGNI) |
| Tiempo | `TimeProvider` (incluido en .NET 8 y posteriores) + `FakeTimeProvider` en pruebas | Tiempo inyectable sin crear interfaces propias |
| Inyección de dependencias | `Microsoft.Extensions.Hosting` (también en MAUI) | Mismo contenedor y estilo en toda la solución |
| Mapeo | Manual (métodos de extensión) | Sin magia ni dependencias (KISS) |
| Pruebas | **xUnit** + **Testcontainers for .NET** (SQL Server real) + **NetArchTest** + **Playwright** (extremo a extremo) | Sin SQL Server ni navegadores instalados en el host ([Testing.md](Especificaciones/Testing.md)) |
| Contenedores | **Docker + Docker Compose** (API, web y base de datos) | Decisión del equipo |
| Editor | **VS Code** (o Antigravity, derivado de VS Code) con **C# Dev Kit** y la extensión **.NET MAUI**; SDK de .NET 10 y la carga de trabajo `maui` | Decisión del equipo; sin Visual Studio |
| CI/CD | **Ninguno** (decisión del equipo) | Se prefiere avanzar rápido; la verificación es `./scripts/test-docker.sh` antes de subir ([Testing.md §6](Especificaciones/Testing.md)) |

Formato de solución: **`.sln`** (el formato `.slnx` ya lo soporta el SDK 10; migrarlo es opcional). Imágenes base: `mcr.microsoft.com/dotnet/sdk:10.0` y `aspnet:10.0`.

> Nota de calendario: el soporte de .NET 8 terminó el **10 de noviembre de 2026**; por eso el proyecto se migró a .NET 10 (LTS).

## 2. Decisiones de Diseño Tecnológico

### 2.1 API y EF Core entre los clientes y SQL Server
* **Seguridad**: la cadena de conexión y las credenciales de la BD **nunca viajan en un ejecutable** ni en el navegador (criterio de "conexiones seguras").
* **Dos hospedajes, un backend**: el escritorio y la web consumen la misma API y las mismas reglas de negocio.
* **Aprobado por el ingeniero**: la tecnología es libre mientras funcione y sea de la familia .NET.

### 2.2 Minimal APIs
Un `MapGroup` por módulo (`/api/v1/clientes`, …). La lógica vive en `Application`; si se prefieren controladores, el cambio es local a la capa `Api`.

### 2.3 Sin MediatR ni AutoMapper
Añaden indirección y, según la versión, licencias comerciales. Un caso de uso es una clase con un método asíncrono inyectada por DI (YAGNI). Los eventos son los **delegados y eventos nativos de C#** que pide la materia ([PatronEventos.md](Especificaciones/PatronEventos.md)).

### 2.4 EF Core directo, sin repositorio genérico
`DbContext` ya es *Unit of Work* y `DbSet<T>` un repositorio. Se usan repositorios **específicos** solo cuando encapsulan consultas con intención de negocio; nunca un `IRepository<T>` genérico.

### 2.5 Entero como clave primaria
`INT IDENTITY` en lugar de `Guid`: legible en pantalla, más eficiente y suficiente. Como los identificadores son secuenciales, la seguridad **no depende de ocultarlos** sino de la autorización en cada endpoint (en la web pública, cada cliente solo ve **sus** pedidos).

## 3. Decisión: una Interfaz Blazor, Dos Hospedajes

El ingeniero aclaró que los criterios del enunciado **no son cerrados**: la tecnología es libre mientras **funcione** y sea de la **familia .NET**. **.NET MAUI Blazor Hybrid y Blazor Web App son .NET**, así que se descarta tener dos interfaces distintas.

```
        ┌──────────────────────────────────────────┐
        │ GestionPedidos.Ui (Razor Class Library)  │  ← se escribe UNA vez
        └───────────────┬──────────────────────────┘
          ┌─────────────┴─────────────┐
          ▼                           ▼
 Desktop (MAUI Blazor Hybrid)   Web (Blazor Web App)
 Windows, .exe                  Docker, navegador
```

| Opción | Pros | Contras | Veredicto |
|---|---|---|---|
| **Blazor (`Ui`) en MAUI Hybrid + Blazor Web App** | Una sola implementación; escritorio real y web; HTML/CSS para personalizar el aspecto | MAUI para Windows no se compila en Docker/Linux; el aspecto "MDI clásico" se logra con pestañas | ✅ Elegida |
| Windows Forms (escritorio) + Blazor (web) | Cumple al pie de la letra el texto del enunciado y el MDI | Dos interfaces que mantener | Descartada (complica sin necesidad) |
| Solo Blazor Web App | Todo en Docker | No es una aplicación de escritorio | Descartada |

### 3.1 Cómo se trabaja con MAUI Blazor Hybrid y VS Code
* El host `Desktop` es una capa **delgada** (un `BlazorWebView` que carga la `Ui`); casi todo el trabajo se hace y se prueba en el hospedaje **web** (Docker) y se ve igual en escritorio.
* Se compila y ejecuta en **Windows** con el SDK de .NET 10, la carga de trabajo (`dotnet workload install maui`) y el target `net10.0-windows10.0.19041.0` (**pendiente de validar en Windows tras la migración**). **El desarrollo diario, la compilación y la depuración del host `Desktop` desde VS Code se validan en la Fase 0**; si algo no funciona sin Visual Studio, ese único proyecto se abre con Visual Studio y el resto sigue en VS Code.
* **No se puede compilar en Docker ni en Linux**: por eso existe `GestionPedidos.Linux.slnf` (la solución sin `Desktop`), que usan los scripts de Docker; `Desktop` se compila en Windows.
* La `Ui` **no usa** APIs exclusivas de un hospedaje; lo específico entra por interfaces (`ISesionAlmacen`).
* Las **pruebas** de ViewModels y servicios no necesitan Windows ni navegador.
* **Plan B** si MAUI diera problemas serios en los equipos del grupo: hospedar la misma `Ui` en un `BlazorWebView` dentro de **Windows Forms o WPF** (también .NET); la `Ui` no cambia.

> **Nota de soporte**: .NET MAUI 8 terminó su soporte en mayo de 2025; con la migración el escritorio pasa a MAUI 10 (soportado hasta 2028), pendiente de validar en Windows.

## 4. SQL Server en Docker

* La imagen oficial de Linux exige **al menos 2 GB de RAM** disponibles para el contenedor. SQL Server reserva memoria como caché de datos (por defecto hasta el 80 % de la RAM física) para ir más rápido; por eso en reposo ocupa bastante más que PostgreSQL. `MSSQL_MEMORY_LIMIT_MB` limita ese uso.
* No existe una imagen oficial más liviana: *Azure SQL Edge*, la versión pequeña, está **retirada desde el 30/09/2025**.
* `MSSQL_PID` define la edición: `Developer` (gratuita, solo desarrollo y pruebas) o `Express` (gratuita, permitida en producción, con límites de memoria y tamaño de base).
* Requiere `ACCEPT_EULA=Y` y `MSSQL_SA_PASSWORD` con complejidad (≥ 8 caracteres con mayúsculas, minúsculas, dígitos y símbolos).
* Herramientas en la imagen 2022: `/opt/mssql-tools18/bin/sqlcmd` (con `-C` para confiar en el certificado autofirmado en desarrollo).
* Alojarla en un servidor con poca RAM: ver [Infraestructura.md §4](Infraestructura.md) (despliegue pospuesto).

## 5. Migración a .NET 10 (ejecutada)

**Estado:** hecha en la rama `chore/net10` para la parte web (API, Web, Ui, pruebas, Docker); el escritorio y el móvil quedan pendientes de validar en Windows/Android. Plan, pasos, riesgos y resultados medidos: **[Migracion-NET10.md](Migracion-NET10.md)**. Auditoría de buenas prácticas que la acompaña: [Auditoria-BuenasPracticas-NET10.md](Auditoria-BuenasPracticas-NET10.md).

**Por qué:** .NET 8 terminó su soporte el 10/11/2026; .NET 10 es la LTS vigente (hasta noviembre de 2028). .NET 11 es de soporte corto y no se adopta.

**Dónde vive la versión** (un único lugar): `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`, los `Dockerfile` y `scripts/*-docker.sh`.

**Dos cambios de comportamiento a recordar:** `_framework/blazor.web.js` se sirve con `MapStaticAssets()` (no con `UseStaticFiles()`), y el `publish` del Dockerfile de la Web no usa `--no-restore`.
