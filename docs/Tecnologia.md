# 🧰 Evaluación Tecnológica - Gestión de Pedidos (UFG)

> **Restricción del ingeniero: .NET 8 como máximo** (no se usa .NET 9 ni .NET 10). Todas las versiones de paquetes se eligen compatibles con .NET 8 y se anclan en `global.json` y `Directory.Packages.props` en la Fase 0.

## 1. Matriz de Stack

| Capa | Tecnología | Justificación |
|---|---|---|
| Lenguaje / runtime | **C# 12 / .NET 8 (LTS)** | Versión máxima permitida por el ingeniero |
| Cliente de escritorio (administración) | **Windows Forms (.NET 8) con MDI**, interfaz **construida en código** | Lo pide el enunciado y la rúbrica; sin diseñador para controlar el aspecto de los controles ([§3](#3-decisión-clientes)) |
| Cliente web (público) | **Blazor Web App** (.NET 8), render interactivo de servidor | Tienda en línea para los clientes; misma API; tecnología .NET aceptada por el ingeniero |
| Patrón de interfaz (escritorio) | **MVP (*Passive View*)** con eventos | Formularios delgados; lógica en presentadores probables sin interfaz gráfica |
| API | **ASP.NET Core 8** (Minimal APIs agrupadas por módulo) | Un solo backend para los dos clientes |
| ORM | **Entity Framework Core 8** (SQL Server) | Requisito del equipo; migraciones y consultas parametrizadas por construcción |
| Base de datos | **SQL Server 2022** (imagen oficial Linux) | Enunciado |
| Autenticación | **JWT** de 60 min (`Microsoft.AspNetCore.Authentication.JwtBearer` 8.x), sin *refresh tokens* | Simplicidad; ver [Seguridad.md](Seguridad.md) |
| Hash de contraseñas | `PasswordHasher<T>` (`Microsoft.Extensions.Identity.Core`, PBKDF2) | Estándar mantenido por Microsoft |
| Documentación de la API | **Swashbuckle.AspNetCore** (OpenAPI/Swagger UI) solo en `Development` | En .NET 8 no existe el generador integrado de OpenAPI (llegó con .NET 9) |
| Validación | **FluentValidation** | Reglas declarativas en la API; reglas básicas espejadas en el formulario |
| Cliente HTTP | `HttpClient` tipado vía `IHttpClientFactory` | Sin librerías extra (YAGNI) |
| Tiempo | `TimeProvider` (incluido en .NET 8) + `FakeTimeProvider` en pruebas | Tiempo inyectable sin crear interfaces propias |
| Inyección de dependencias | `Microsoft.Extensions.Hosting` (también en WinForms) | Mismo contenedor y estilo en toda la solución |
| Mapeo | Manual (métodos de extensión) | Sin magia ni dependencias (KISS) |
| Pruebas | **xUnit** + **Testcontainers for .NET** (SQL Server) + **Respawn** + **NetArchTest** (+ bUnit para la web si hace falta) | Sin SQL Server instalado en el host ([Testing.md](Especificaciones/Testing.md)) |
| Contenedores | **Docker + Docker Compose** (API, web y base de datos) | Decisión del equipo |
| Editor | **VS Code** (o Antigravity, que es un derivado de VS Code) con **C# Dev Kit** y SDK de .NET 8 | Decisión del equipo; sin Visual Studio |
| CI | **GitHub Actions** | Ver [GuiaCI.md](Equipo/GuiaCI.md) |

Formato de solución: **`.sln`** (el formato `.slnx` requiere el SDK de .NET 9 o superior). Imágenes base: `mcr.microsoft.com/dotnet/sdk:8.0` y `aspnet:8.0`.

> Nota de calendario: Microsoft termina el soporte de .NET 8 el **10 de noviembre de 2026** (dos días antes de la defensa). No afecta a la materia; si la web llega a producción real, migrar a una versión con soporte es un cambio de versión de SDK y paquetes.

## 2. Decisiones de Diseño Tecnológico

### 2.1 API y EF Core entre los clientes y SQL Server
* **Seguridad**: la cadena de conexión y las credenciales de la BD **nunca viajan en un ejecutable** ni en el navegador (criterio de "conexiones seguras").
* **Dos clientes, un backend**: el escritorio (administración) y la web (público) consumen la misma API y las mismas reglas de negocio.
* **Aprobado por el ingeniero**: la única condición es que sean tecnologías de la familia .NET.

### 2.2 Minimal APIs
Un `MapGroup` por módulo (`/api/v1/clientes`, …). La lógica vive en `Application`; si se prefieren controladores, el cambio es local a la capa `Api`.

### 2.3 Sin MediatR ni AutoMapper
Añaden indirección y, según la versión, licencias comerciales. Un caso de uso es una clase con un método asíncrono inyectada por DI (YAGNI). Los eventos son los **delegados y eventos nativos de C#** que pide la materia ([PatronEventos.md](Especificaciones/PatronEventos.md)).

### 2.4 EF Core directo, sin repositorio genérico
`DbContext` ya es *Unit of Work* y `DbSet<T>` un repositorio. Se usan repositorios **específicos** solo cuando encapsulan consultas con intención de negocio; nunca un `IRepository<T>` genérico.

### 2.5 Entero como clave primaria
`INT IDENTITY` en lugar de `Guid`: legible en pantalla, más eficiente y suficiente. Como los identificadores son secuenciales, la seguridad **no depende de ocultarlos** sino de la autorización en cada endpoint (en la web pública, cada cliente solo ve **sus** pedidos).

## 3. Decisión: Clientes

**Escritorio = Windows Forms con MDI** (administración y operación). El enunciado dice textualmente *"Desarrolle una aplicación de Windows Form en código c#"* y la rúbrica puntúa *"Aplicación de escritorio de interfaz de múltiples documentos"* y *"Codifica los diferentes eventos de la interfaz gráfica"*.

**Web = Blazor Web App** (parte pública). Una tienda se usa sobre todo en la web y nadie descarga una aplicación de escritorio para comprar; el ingeniero aceptó web siempre que sea tecnología .NET. En .NET 8, *Blazor Web App* es la plantilla unificada con modos de render de servidor, WebAssembly y automático.

| Opción | Pros | Contras | Veredicto |
|---|---|---|---|
| **WinForms (escritorio) + Blazor Web App (público)** | Cumple el enunciado y da una tienda usable en la web | Dos interfaces distintas (no se comparten pantallas) | ✅ Elegida |
| .NET MAUI Blazor Hybrid | Una UI para escritorio y web | No es Windows Forms ni tiene MDI; MAUI para Windows no se compila en Docker | Descartada |
| WPF | Mejor estilo visual | No es "Windows Form" | Descartada |

### 3.1 Windows Forms sin diseñador, con VS Code
* La interfaz se **escribe en código** (no hay arrastrar y soltar): cada formulario construye sus controles en un método `ConstruirInterfaz()` dentro de un archivo parcial `<Modulo>Form.Interfaz.cs`. No existen archivos `.Designer.cs` ni `.resx`.
* Un **kit de controles propio** (`Comun/Controles`) y un **tema central** (`Comun/Tema`: paleta, fuentes, espaciados) permiten personalizar el aspecto de los componentes de Windows Forms en un solo lugar ([InterfazEscritorio.md §9](Especificaciones/InterfazEscritorio.md)).
* Se ejecuta con `dotnet run` en **Windows** (el SDK de .NET 8 instalado en Windows). Compilar el proyecto en Linux/Docker es posible con `<EnableWindowsTargeting>true</EnableWindowsTargeting>`; **se valida en la Fase 0** (incluida la publicación autocontenida para `win-x64`).
* Las **pruebas** de presentadores no necesitan Windows ni interfaz gráfica (vistas falsas).

## 4. SQL Server en Docker

* La imagen oficial de Linux exige **al menos 2 GB de RAM** disponibles para el contenedor. SQL Server reserva memoria como caché de datos (por defecto hasta el 80 % de la RAM física) para ir más rápido; por eso en reposo ocupa bastante más que PostgreSQL. `MSSQL_MEMORY_LIMIT_MB` limita ese uso.
* No existe una imagen oficial más liviana: *Azure SQL Edge*, la versión pequeña, está **retirada desde el 30/09/2025**.
* `MSSQL_PID` define la edición: `Developer` (gratuita, solo desarrollo y pruebas) o `Express` (gratuita, permitida en producción, con límites de memoria y tamaño de base).
* Requiere `ACCEPT_EULA=Y` y `MSSQL_SA_PASSWORD` con complejidad (≥ 8 caracteres con mayúsculas, minúsculas, dígitos y símbolos).
* Herramientas en la imagen 2022: `/opt/mssql-tools18/bin/sqlcmd` (con `-C` para confiar en el certificado autofirmado en desarrollo).
* Alojarla en un servidor con poca RAM: ver [Infraestructura.md §4](Infraestructura.md) (despliegue pospuesto).
