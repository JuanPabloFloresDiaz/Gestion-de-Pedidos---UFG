# Auditoría de buenas prácticas (previa a la migración a .NET 10)

**Fecha:** 23/11/2026 · **Rama:** `chore/net10` · **Alcance:** solo la parte web (API, Web, Ui, Infrastructure, Docker). Escritorio y móvil se revisan después.
**Método:** lectura del código y de la configuración (Program.cs de API y Web, DI, repositorios, Dockerfiles, `docker-compose.yml`, `Directory.*.props`, `.editorconfig`) y búsquedas dirigidas (`DateTime.Now`, `async void`, `.Include`, `.Result`, etc.), más las advertencias vistas en los registros de la API en ejecución. **No fue una revisión línea por línea de todo el código** (en particular, las páginas `.razor` y los ViewModels solo se muestrearon por búsqueda). Nada de lo de abajo se ha modificado todavía.

Prioridad: **A** = conviene hacerlo con la migración · **B** = después de migrar, antes de publicar · **C** = opcional / pulido.

## 0. Lo que ya está bien (no tocar)

* Capas con reglas de dependencia verificadas por pruebas de arquitectura (`NetArchTest`).
* Sin `DateTime.Now/UtcNow` en `src` y `TimeProvider` inyectado en 24 archivos (código testeable).
* Sin `.Result`, `.Wait()` ni `GetAwaiter().GetResult()`; sin `new HttpClient()` (se usa `IHttpClientFactory`).
* 46 consultas con `AsNoTracking`; consultas siempre parametrizadas por EF; concurrencia optimista con `rowversion`.
* Mínimo privilegio en SQL Server (`pedidos_app` vs `pedidos_migrator`), puertos de BD, Garage y MailHog solo en `127.0.0.1`.
* Limitación de tasa en login, registro y tienda; igualación de tiempos en el login (`GastarTiempo`); límite de 2 MB en imágenes.
* Manejador de excepciones que no filtra detalles técnicos; `ProblemDetails` con código estable.
* Tokens de sesión web en `ProtectedSessionStorage` (nunca `localStorage`); el navegador no ve el JWT.
* Imagen final no corre como root (`USER app`).

## 1. Hallazgos

### Seguridad

| # | Pri | Hallazgo | Evidencia | Propuesta |
|---|---|---|---|---|
| S1 | A | **Hash que necesita actualizarse no se actualiza.** `VerifyHashedPassword` puede devolver `SuccessRehashNeeded` (hash con parámetros viejos); `HasherClave.Verificar` lo trata como éxito pero nunca vuelve a guardar el hash. Al subir de versión el costo del algoritmo, las claves existentes quedarían con el hash viejo para siempre. | `Infrastructure/Seguridad/HasherClave.cs:19`, `Autenticacion/AutenticarUsuario.cs:57` | Devolver el resultado completo y, si pide rehash, guardar el hash nuevo en el login correcto. Prueba nueva. |
| S2 | A | **La limitación de tasa se basa en `RemoteIpAddress` sin `UseForwardedHeaders`.** Detrás de un proxy inverso (Dokploy/Traefik, que es el destino de despliegue) todas las personas comparten una IP: 10 intentos de login por minuto para *todo el mundo*, o ninguna protección real si el proxy no se configura. | `Api/Program.cs` (políticas `login`, `registro`, `tienda`); no hay `UseForwardedHeaders` | `ForwardedHeadersOptions` con redes/proxies conocidos (`KnownNetworks`) y `UseForwardedHeaders()` primero en el pipeline; configurable por entorno. |
| S3 | A | **Claves de Data Protection sin persistir** (advertencia en los registros). Cada redespliegue invalida la sesión web de todas las personas (`ProtectedSessionStorage` falla con `CryptographicException`, que el código ya captura) y no funciona con 2 réplicas. | Registros de la API y la Web | `PersistKeysToFileSystem` en un volumen de Docker (o a la base) y `SetApplicationName`. |
| S4 | B | **Sin encabezados de seguridad** (`X-Content-Type-Options`, `Referrer-Policy`, `Content-Security-Policy`, `X-Frame-Options`/`frame-ancestors`, `Permissions-Policy`) ni HSTS en API y Web. | No hay `UseHsts`/middleware de cabeceras | Middleware común de cabeceras; CSP compatible con Blazor Server (`wss:` del circuito). HSTS solo en producción tras el proxy. |
| S5 | B | **Swagger habilitado por defecto en el compose** (`SWAGGER_HABILITADO:-true`) aunque el entorno sea `Production`; y llaves de Garage con valores por defecto dentro del `docker-compose.yml` versionado. Es válido en desarrollo, pero es fácil publicarlo así. | `docker-compose.yml` (`Swagger__Habilitado`, `GARAGE_*`) | Valor por defecto `false` y que el `.env.example` lo active; quitar los valores por defecto de las llaves (hacerlas obligatorias como `JWT_SIGNING_KEY`). |
| S6 | B | **`AllowedHosts: "*"`** en API y Web. | `appsettings.json` | Lista de hosts por entorno (variable). |
| S7 | B | **`TrustServerCertificate=True`** en las cadenas del compose y de `appsettings.Development.json`. Aceptable en local; no debe llegar a producción. | `docker-compose.yml`, `appsettings.Development.json` | Documentarlo y exigir un certificado válido en producción. |
| S8 | C | Token de acceso único de 60 min sin *refresh token*; la baja de usuario surte efecto en ≤ 60 s por la caché. Es una decisión consciente y documentada. | `Program.cs` (`OnTokenValidated`) | Solo si se quiere sesión larga sin reautenticar. |

### Rendimiento y datos (EF Core)

| # | Pri | Hallazgo | Evidencia | Propuesta |
|---|---|---|---|---|
| D1 | A | **`QuerySplittingBehavior` sin configurar** con consultas que cargan varias colecciones (advertencia `20504` en los registros). Riesgo de explosión cartesiana en producto→opciones, pedido→detalles. | `ProductoRepository` (2 colecciones), `PedidoRepository`, `PedidosClienteRepository` | `UseQuerySplittingBehavior(SplitQuery)` global y `AsSingleQuery()` donde convenga; una prueba que fije el comportamiento. |
| D2 | B | **Migraciones al iniciar la API con la credencial del dueño** (`Database:MigrarAlIniciar`). Funciona con una réplica, pero deja la cadena privilegiada en el contenedor en ejecución y compite si hay varias réplicas. | `Program.cs` (bloque final), `docker-compose.yml` | Migrar con un paso aparte (`dotnet ef migrations bundle` o un contenedor de un solo uso, como ya hace `db-init`), y que la API corra solo con `pedidos_app`. |
| D3 | C | `AddDbContext` → `AddDbContextPool` (menor costo por petición). Requiere que el contexto no guarde estado por petición; revisar antes. | `Infrastructure/DependencyInjection.cs:37` | Medir antes de cambiar. |
| D4 | C | Con `EnableRetryOnFailure` las transacciones manuales exigen `CreateExecutionStrategy`. **Hoy no hay transacciones manuales** (`BeginTransaction`: 0 usos), por lo que no falla; queda como regla para los módulos nuevos (Pagos, Favoritos…). | búsqueda en `src` | Anotarlo en las pautas. |

### Plataforma y .NET 10

| # | Pri | Hallazgo | Evidencia | Propuesta |
|---|---|---|---|---|
| P1 | A | **`UseStaticFiles()` en la Web.** Desde .NET 9 lo recomendado es `MapStaticAssets()` (compresión, huellas digitales y caché automática); es el cambio más barato con mayor efecto en la carga del sitio. | `Web/Program.cs:19` | `app.MapStaticAssets()` + `@Assets[...]` en `App.razor`; verificar con el E2E y las capturas. |
| P2 | A | **Auditoría de NuGet y `TreatWarningsAsErrors`.** El SDK moderno audita también las dependencias transitivas; un aviso de vulnerabilidad (`NU1901–NU1904`) se vuelve **error de compilación** con la regla actual. Conviene decidir la política explícita antes de que rompa el build. | `Directory.Build.props` | `NuGetAuditMode`/`NuGetAuditLevel` explícitos y `WarningsNotAsErrors` solo para `NU190x` (siguen visibles); corregir las vulnerabilidades reales subiendo paquetes. |
| P3 | A | **Analizadores sin endurecer.** No hay `AnalysisLevel`, `AnalysisMode` ni `EnforceCodeStyleInBuild`; el `.editorconfig` solo exige namespaces de archivo. | `Directory.Build.props`, `.editorconfig` | `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`; aplicarlo **por etapas** (puede generar muchas advertencias-error): primero medir cuántas salen y decidir qué reglas se bajan a `suggestion`. |
| P4 | B | **Cliente HTTP Ui→API sin resiliencia ni tiempo de espera configurado.** | `Ui/ServiciosUi.cs:30` | `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`) o, al menos, `Timeout` y reintento solo en `GET`. Cuidado con los POST (idempotencia). |
| P5 | B | **`Program.cs` de la API de 210 líneas** mezcla autenticación, límites de tasa, Swagger, manejo de errores y migraciones. | `Api/Program.cs` | Extraer a métodos de extensión (`AddAutenticacionJwt`, `AddLimitesDeTasa`, `AddSwaggerDocs`, `MigrarYSembrarAsync`). Sin cambio de comportamiento; las pruebas de la API lo cubren. |
| P6 | B | Manejador de excepciones escrito a mano en `UseExceptionHandler(lambda)`. | `Program.cs:127` | Pasar a `IExceptionHandler` (probado y más legible); mantiene el JSON `ProblemDetails` actual. |
| P7 | B | **Health checks caseros** (`/health`, `/health/ready`) en vez de `AddHealthChecks()` + `AddDbContextCheck`. | `Program.cs` | Cambio pequeño; deja la puerta abierta a más comprobaciones (Garage, SMTP). |
| P8 | C | **Swashbuckle → `Microsoft.AspNetCore.OpenApi`** (OpenAPI 3.1 integrado en .NET 10). | `Api.csproj` | Opcional; ya anotado en el plan de migración (paso 5). |
| P9 | C | Sin trazas/métricas (OpenTelemetry) ni registro estructurado; los registros son de consola por defecto. | — | Opcional para portafolio: OpenTelemetry + OTLP. |
| P10 | C | **Imágenes Docker**: etiquetas flotantes y sin límites de recursos en el compose. Con .NET 10 existen imágenes `-noble-chiseled` (más pequeñas, sin shell); **no** son compatibles con los `healthcheck` actuales que usan `bash` y `/dev/tcp`. | Dockerfiles, `docker-compose.yml` | Mantener `aspnet:10.0` normal. Si se quiere chiseled, el `healthcheck` pasa a un binario propio o a `dotnet`. Además, `InvariantGlobalization=false` exige ICU, que chiseled no trae por defecto. |

### Mantenibilidad (UI)

| # | Pri | Hallazgo | Evidencia | Propuesta |
|---|---|---|---|---|
| U1 | B | **`async void` en 6 manejadores de eventos.** Cuatro ViewModels lo documentan y capturan errores en `EjecutarAsync`; las dos páginas de tienda (`PedidoPagina`, `MisPedidosPagina`: `Sesion_Restaurada`) no lo documentan y no se vio protección contra excepciones: una excepción ahí tumbaría el circuito. | `PedidoPagina.razor:83`, `MisPedidosPagina.razor:94` | Envolver en `try/catch` con registro o delegar en un método `Task` protegido. |
| U2 | C | Las páginas de tienda tienen lógica en el `@code`, contrario a la regla "Páginas sin lógica" de las pautas del propio proyecto. | mismas páginas | Moverla a un ViewModel cuando se toquen por los módulos nuevos (Favoritos, Comentarios). |

## 2. Qué entra con la migración y qué no

**Con la migración (rama `chore/net10`, un commit por punto):** S1, S2, S3, D1, P1, P2, P3 (medición primero), y los cambios de versión del plan.
**Después de migrar y antes de publicar:** S4–S7, D2, P4–P7, U1.
**Opcional:** S8, D3, D4 (como regla), P8–P10, U2.

Recomendación de orden: primero **migrar sin mezclar** (pasos 2–9 del plan) para que cualquier fallo se atribuya a la versión, y aplicar los puntos "A" como commits propios *después* de que la migración esté en verde, salvo P2 y P3, que dependen del SDK nuevo.

## 3. Lo que esta auditoría no cubre

* No se ejecutaron análisis automáticos (`dotnet list package --vulnerable`, analizadores en `latest-all`, escaneo de imágenes). Se harán en el paso 6 del plan con la toolchain de .NET 10.
* No se revisaron los módulos de escritorio y móvil, los reportes (QuestPDF/ClosedXML: exportación a CSV/Excel y posible inyección de fórmulas) ni las migraciones de EF.
* No se midió rendimiento: D1/D3 son riesgos teóricos hasta tener datos.
