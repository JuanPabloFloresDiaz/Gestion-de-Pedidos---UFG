# 🧰 Evaluación Tecnológica - Gestión de Pedidos (UFG)

> **Versión del proyecto: .NET 8.** El ingeniero exige .NET 8 en parciales y laboratorios; se arranca así y **se deja preparada la migración a .NET 10** (§5) por si confirma que se permite. Todas las versiones de paquetes se eligen compatibles con .NET 8 y se anclan en `global.json` y `Directory.Packages.props` en la Fase 0.

## 1. Matriz de Stack

| Capa | Tecnología | Justificación |
|---|---|---|
| Lenguaje / runtime | **C# 12 / .NET 8 (LTS)** | Versión máxima permitida por el ingeniero |
| Interfaz (única) | **Blazor** en una **Razor Class Library** (`Ui`) | Una sola implementación de todas las pantallas, para escritorio y web |
| Hospedaje de escritorio | **.NET MAUI Blazor Hybrid** (target Windows) | Aplicación de escritorio de la familia .NET que reutiliza la `Ui` |
| Hospedaje web | **Blazor Web App** (.NET 8, render interactivo de servidor) | La misma `Ui` en el navegador; se despliega en Docker |
| Patrón de interfaz | **Página (.razor) + ViewModel** con eventos | Marcado sin lógica; el ViewModel se prueba sin pantalla |
| Componentes visuales | **Kit propio** (`Ui/Comun/Componentes`) + tema CSS central | Se evaluó MudBlazor y se descartó: el kit propio es pequeño, sin dependencias, se personaliza en un solo archivo (`tema.css`) y evita problemas de versión con .NET 8 |
| API | **ASP.NET Core 8** (Minimal APIs agrupadas por módulo) | Un solo backend para los dos clientes |
| ORM | **Entity Framework Core 8** (SQL Server) | Requisito del equipo; migraciones y consultas parametrizadas por construcción |
| Base de datos | **SQL Server 2022** (imagen oficial Linux) | Enunciado |
| Autenticación | **JWT** de 60 min (`Microsoft.AspNetCore.Authentication.JwtBearer` 8.x), sin *refresh tokens* | Simplicidad; ver [Seguridad.md](Seguridad.md) |
| Hash de contraseñas | `PasswordHasher<T>` (`Microsoft.Extensions.Identity.Core`, PBKDF2) | Estándar mantenido por Microsoft |
| Documentación de la API | **Swashbuckle.AspNetCore** (OpenAPI/Swagger UI) solo en `Development` | En .NET 8 no existe el generador integrado de OpenAPI (llegó con .NET 9) |
| Validación | Reglas propias componibles (delegados): `Validaciones` en la API y `Validacion` en la interfaz | Sin dependencias (YAGNI); la API siempre vuelve a validar |
| Cliente HTTP | `HttpClient` tipado vía `IHttpClientFactory` | Sin librerías extra (YAGNI) |
| Tiempo | `TimeProvider` (incluido en .NET 8) + `FakeTimeProvider` en pruebas | Tiempo inyectable sin crear interfaces propias |
| Inyección de dependencias | `Microsoft.Extensions.Hosting` (también en MAUI) | Mismo contenedor y estilo en toda la solución |
| Mapeo | Manual (métodos de extensión) | Sin magia ni dependencias (KISS) |
| Pruebas | **xUnit** + **Testcontainers for .NET** (SQL Server real) + **NetArchTest** + **Playwright** (extremo a extremo) | Sin SQL Server ni navegadores instalados en el host ([Testing.md](Especificaciones/Testing.md)) |
| Contenedores | **Docker + Docker Compose** (API, web y base de datos) | Decisión del equipo |
| Editor | **VS Code** (o Antigravity, derivado de VS Code) con **C# Dev Kit** y la extensión **.NET MAUI**; SDK de .NET 8 y la carga de trabajo `maui` | Decisión del equipo; sin Visual Studio |
| CI/CD | **Ninguno** (decisión del equipo) | Se prefiere avanzar rápido; la verificación es `./scripts/test-docker.sh` antes de subir ([Testing.md §6](Especificaciones/Testing.md)) |

Formato de solución: **`.sln`** (el formato `.slnx` requiere el SDK de .NET 9 o superior). Imágenes base: `mcr.microsoft.com/dotnet/sdk:8.0` y `aspnet:8.0`.

> Nota de calendario: Microsoft termina el soporte de .NET 8 el **10 de noviembre de 2026** (dos días antes de la defensa). No afecta a la materia; si la web llega a producción real, migrar a una versión con soporte es un cambio de versión de SDK y paquetes.

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
* Se compila y ejecuta en **Windows** con el SDK de .NET 8, la carga de trabajo (`dotnet workload install maui`) y el target `net8.0-windows10.0.19041.0`. **El desarrollo diario, la compilación y la depuración del host `Desktop` desde VS Code se validan en la Fase 0**; si algo no funciona sin Visual Studio, ese único proyecto se abre con Visual Studio y el resto sigue en VS Code.
* **No se puede compilar en Docker ni en Linux**: por eso existe `GestionPedidos.Linux.slnf` (la solución sin `Desktop`), que usan los scripts de Docker; `Desktop` se compila en Windows.
* La `Ui` **no usa** APIs exclusivas de un hospedaje; lo específico entra por interfaces (`ISesionAlmacen`).
* Las **pruebas** de ViewModels y servicios no necesitan Windows ni navegador.
* **Plan B** si MAUI diera problemas serios en los equipos del grupo: hospedar la misma `Ui` en un `BlazorWebView` dentro de **Windows Forms o WPF** (también .NET); la `Ui` no cambia.

> **Nota de soporte**: .NET MAUI 8 ya terminó su soporte (mayo de 2025) y .NET 8 lo termina el 10/11/2026; para este proyecto académico no es un impedimento. Si el ingeniero levanta también el límite de .NET 8, conviene pasar a .NET 10 (soporte hasta 2028).

## 4. SQL Server en Docker

* La imagen oficial de Linux exige **al menos 2 GB de RAM** disponibles para el contenedor. SQL Server reserva memoria como caché de datos (por defecto hasta el 80 % de la RAM física) para ir más rápido; por eso en reposo ocupa bastante más que PostgreSQL. `MSSQL_MEMORY_LIMIT_MB` limita ese uso.
* No existe una imagen oficial más liviana: *Azure SQL Edge*, la versión pequeña, está **retirada desde el 30/09/2025**.
* `MSSQL_PID` define la edición: `Developer` (gratuita, solo desarrollo y pruebas) o `Express` (gratuita, permitida en producción, con límites de memoria y tamaño de base).
* Requiere `ACCEPT_EULA=Y` y `MSSQL_SA_PASSWORD` con complejidad (≥ 8 caracteres con mayúsculas, minúsculas, dígitos y símbolos).
* Herramientas en la imagen 2022: `/opt/mssql-tools18/bin/sqlcmd` (con `-C` para confiar en el certificado autofirmado en desarrollo).
* Alojarla en un servidor con poca RAM: ver [Infraestructura.md §4](Infraestructura.md) (despliegue pospuesto).

## 5. Posible Migración a .NET 10 (opcional, a decidir tras consultar al ingeniero)

**Estado:** el proyecto arranca en **.NET 8**. Pablo consultará al ingeniero si se permite .NET 10; hasta que lo confirme **no se cambia nada**.

**Por qué conviene considerarla**
* .NET 8 y .NET 9 terminan su soporte el **10/11/2026**; .NET 10 (LTS) tiene soporte hasta noviembre de 2028.
* .NET MAUI 8 ya no recibe soporte; MAUI 10 sí.
* Nada de la arquitectura, el dominio ni la documentación depende de funciones exclusivas de .NET 8.

**Qué cambiaría (acotado)**

| Qué | Cambio |
|---|---|
| `global.json` y `<TargetFramework>` | `net8.0` → `net10.0` (y `net10.0-windows10.0.19041.0` en `Desktop`) |
| Paquetes (`Directory.Packages.props`) | EF Core 8 → 10, `JwtBearer` 8 → 10, los demás a versiones que soporten .NET 10 |
| Imágenes Docker | `sdk:8.0`/`aspnet:8.0` → `sdk:10.0`/`aspnet:10.0` |
| Imágenes y scripts | `sdk:8.0`/`aspnet:8.0` → `10.0` en los `Dockerfile` y en `scripts/*-docker.sh`; carga de trabajo `maui` de la versión 10 |
| OpenAPI | Se puede reemplazar Swashbuckle por el generador integrado (`Microsoft.AspNetCore.OpenApi`); opcional |
| Formato de solución | Se puede adoptar `.slnx`; opcional |
| Migraciones de EF Core | Regenerar el *snapshot* o verificar que no cambió el modelo; ninguna tabla cambia |

**Cómo mantener barata la migración desde el primer día**
* Versiones de paquetes y de SDK **solo** en `global.json`, `Directory.Build.props` y `Directory.Packages.props` (un único lugar).
* No usar APIs marcadas como obsoletas ni funciones de vista previa; las advertencias son errores de compilación.
* Una sola rama de migración (`chore/net10`) que cambia esos archivos, corrige lo que no compile y pasa toda la batería de pruebas.
* Mejor **hacerla temprano** (antes de tener muchos módulos) que tarde; si el ingeniero responde pronto, el costo es de horas, no de días.

**Compatibilidad con los equipos del laboratorio**: .NET 8 y .NET 10 se publican autocontenidos ([Infraestructura.md §5](Infraestructura.md)); en ambos casos lo ya instalado de .NET Framework es irrelevante. WebView2 y una versión reciente de Windows son necesarios con cualquiera de las dos.
