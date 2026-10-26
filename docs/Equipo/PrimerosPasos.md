# 🚀 Primeros Pasos para el Equipo

> Lo mínimo para clonar, levantar, probar y empezar a trabajar en un módulo. Todo corre en **Docker** salvo el escritorio, que corre en **Windows**.

## 1. Requisitos

| Para qué | Qué se necesita |
|---|---|
| API, web, base de datos y pruebas | **Docker Desktop** (o Docker Engine con Compose v2). SQL Server pide **≥ 2 GB de RAM** libres para su contenedor |
| Escritorio (MAUI Blazor Hybrid) | **Windows 10/11**, **SDK de .NET 8**, carga de trabajo `maui` y **WebView2** (viene con Windows 11 y casi siempre con Windows 10 actualizado) |
| Editar código | **Visual Studio 2022** ([§4.1](#41-paso-a-paso-con-visual-studio-2022)) o **VS Code** con *C# Dev Kit* y *.NET MAUI* (se recomiendan solas al abrir la carpeta: `.vscode/extensions.json`) |

No hace falta instalar SQL Server, Node ni navegadores de pruebas: todo eso vive en contenedores.

## 2. Levantar el sistema

```bash
git clone https://github.com/JuanPabloFloresDiaz/Gestion-de-Pedidos---UFG.git
cd Gestion-de-Pedidos---UFG
./scripts/up.sh          # crea .env, construye y levanta SQL Server + API + web (la primera vez tarda unos minutos)
```

| Qué | Dónde |
|---|---|
| **Web** | http://localhost:4392 |
| **API** (con Swagger) | http://localhost:4390/swagger |
| **SQL Server** | `localhost`, puerto **4391** (usuario `sa`, clave en `.env`); ver [§2.1](#21-conectarse-a-la-base-de-datos-dbeaver-azure-data-studio-ssms) |
| **Administrador inicial** | usuario `admin`, contraseña `ADMIN_INITIAL_PASSWORD` de `.env`; el sistema le pedirá cambiarla al primer ingreso |

`./scripts/up.sh --limpio` borra los datos y empieza de cero; `./scripts/down.sh` apaga. Los puertos `4390–4392` están reservados para este proyecto (ya revisados contra los demás proyectos del equipo); se cambian en `.env`.

### 2.1 Conectarse a la base de datos (DBeaver, Azure Data Studio, SSMS)

| Campo | Valor |
|---|---|
| Host | `localhost` |
| Puerto | **`4391`** (no el 1433: el contenedor lo publica en este puerto) |
| Base de datos | `GestionPedidos` (o `master` para ver todas) |
| Autenticación | SQL Server Authentication |
| Usuario / contraseña | `sa` / `MSSQL_SA_PASSWORD` de `.env` (por defecto `Sa_Dev_Password_123!`) |
| **Trust Server Certificate** | **Marcado** (el contenedor usa un certificado autofirmado; sin esto falla con un error de certificado) |

* **Explorar con `sa`; la aplicación usa `pedidos_app`**, un usuario de mínimo privilegio que no puede modificar ni borrar el historial (`Actividades`). Para ver el sistema como lo ve la API, crear otra conexión con `pedidos_app` / `PEDIDOS_APP_PASSWORD` de `.env`.
* Verán las 11 tablas en `dbo`, la vista `vw_PedidosResumen` y el procedimiento `usp_Pedidos_Buscar`.
* El puerto solo escucha en `127.0.0.1`: la base nunca queda expuesta a la red. **No** la modifiquen a mano; los cambios de esquema van por migraciones ([§6](#6-migraciones-de-la-base-de-datos)).
* *Connection refused* → el contenedor no está corriendo (`docker compose ps`, `sqlserver` debe estar *healthy*). *Login failed for user 'sa'* → la contraseña de su `.env` es otra.

## 3. Pruebas (no hay CI/CD: se corren a mano antes de subir cambios)

```bash
./scripts/test-docker.sh                      # unitarias + integración (SQL Server real) + arquitectura
./scripts/up.sh && ./scripts/e2e-docker.sh    # extremo a extremo en navegador (Playwright)
```

Detalle y reglas en [Testing.md](../Especificaciones/Testing.md).

## 4. Escritorio en Windows

> ⚠️ **Estado**: el proyecto `src/GestionPedidos.Desktop` está escrito pero **todavía no se ha validado en Windows** (se creó desde Linux, donde MAUI no compila). Es la primera tarea del equipo en Windows; si aparece algún error, se corrige y se anota aquí. Con **Visual Studio 2022** sigan el [§4.1](#41-paso-a-paso-con-visual-studio-2022).

1. Con el entorno levantado (§2), en **PowerShell** o la terminal de VS Code:
   ```powershell
   dotnet workload install maui          # solo la primera vez
   dotnet run -f net8.0-windows10.0.19041.0 --project src/GestionPedidos.Desktop
   ```
2. Se abre la ventana "Gestión de Pedidos — UFG" con el login. Solo entra el **personal** (administrador u operador); un usuario "Cliente" recibe "usuario o contraseña incorrectos".
3. La dirección de la API sale de `GESTIONPEDIDOS_API_URL` (variable de entorno), luego de `appsettings.json` junto al `.exe`, y por defecto es `http://localhost:4390`.
4. Si Docker corre dentro de **WSL**, los puertos también se ven desde Windows en `localhost`.
5. Para generar un `.exe` que no necesite instalar nada: `dotnet publish -f net8.0-windows10.0.19041.0 -c Release` (queda autocontenido, con el runtime de Windows App SDK incluido).
6. Si la ventana sale en blanco: falta **WebView2** (instalar el *Evergreen Runtime* de Microsoft) o la API no está levantada.

### 4.1 Paso a paso con Visual Studio 2022

Sirve para ejecutar y depurar el **escritorio** (y también la API y la web) desde Visual Studio. Se necesita Visual Studio **2022 17.8 o superior** en Windows 10/11.

**A. Preparar una sola vez**

1. **Instalar las cargas de trabajo.** Abrir el *Visual Studio Installer* → **Modificar** → marcar:
   * **"Desarrollo de la interfaz de usuario de aplicaciones multiplataforma de .NET"** (.NET MAUI);
   * **"ASP.NET y desarrollo web"**.

   Debe estar el **SDK de .NET 8** (viene en ambas; se comprueba con `dotnet --list-sdks`).
2. **Instalar Docker Desktop** y dejarlo iniciado (la API y la base corren ahí).
3. **WebView2**: ya viene con Windows 11 y casi siempre con Windows 10 actualizado. Si la ventana sale en blanco, instalar el *Evergreen Runtime* de Microsoft.
4. **Clonar el repositorio** (Git → Clonar repositorio, o `git clone`) y abrir **`GestionPedidos.sln`** (la solución completa, con `Desktop`). No abrir el `.slnf`: ese filtro es para Linux y excluye el escritorio.

**B. Levantar la API y la base de datos**

En una terminal de Windows (PowerShell, o *Ver → Terminal* dentro de Visual Studio) en la carpeta del repositorio:

```powershell
copy .env.example .env            # solo la primera vez
docker compose up -d --build      # sqlserver + api + web
```

> Los scripts `./scripts/*.sh` necesitan **Git Bash o WSL**; en PowerShell se usa `docker compose` directamente (como arriba; `docker compose down` para apagar y `docker compose down -v` para borrar también los datos).

Comprobar que responde http://localhost:4390/health (debe decir `vivo`).

**C. Ejecutar el escritorio**

1. En el **Explorador de soluciones**: clic derecho en **`GestionPedidos.Desktop` → Establecer como proyecto de inicio**. (Por defecto Visual Studio puede dejar `GestionPedidos.Domain`, que no es ejecutable.)
2. En la barra de herramientas, el perfil de ejecución debe decir **"Escritorio (Windows)"** y la configuración **Debug** / **Any CPU** (o x64).
3. **F5** para depurar o **Ctrl+F5** para ejecutar sin depurar. La primera compilación de MAUI tarda varios minutos.
4. Se abre la ventana *Gestión de Pedidos — UFG*. Iniciar sesión con `admin` y la contraseña de `ADMIN_INITIAL_PASSWORD` en `.env` (por defecto `Admin_Dev_Password_123!`; pedirá cambiarla). Si la base ya existía de antes, será la contraseña que se fijó entonces.

**D. Ejecutar la web desde Visual Studio (opcional)**

Con la API en Docker ya levantada, para depurar la web: detener el servicio de Docker `web` (`docker compose stop web`), establecer **`GestionPedidos.Web`** como proyecto de inicio y pulsar **F5**. Apunta a `http://localhost:4390` por defecto. Para depurar también la API: `docker compose stop api`, establecer **`GestionPedidos.Api`** como inicio (usa `appsettings.Development.json`, que ya trae la conexión a `localhost:4391`) y **F5**.

**E. Ejecutar las pruebas desde Visual Studio**

*Ver → Explorador de pruebas* → **Ejecutar todas**. Las pruebas de integración (`Infrastructure.Tests` y `Api.Tests`) levantan su propio SQL Server en Docker con Testcontainers, así que **Docker Desktop debe estar iniciado** y la primera vez descarga la imagen. Las demás no necesitan Docker.

**F. Problemas frecuentes**

| Síntoma | Causa y solución |
|---|---|
| El proyecto `Desktop` aparece como "no se pudo cargar" o pide una carga de trabajo | Falta la carga de trabajo de MAUI (**A.1**) o `dotnet workload install maui` en una terminal de administrador |
| Error de compilación de `Desktop` | Copiar el mensaje completo de la ventana **Salida / Lista de errores** y avisar al equipo: este proyecto aún se está validando en Windows |
| La ventana se abre y queda en blanco | Falta WebView2 (**A.3**) o la API no responde (**B**) |
| "No se pudo conectar con el servidor" | La API no está levantada o el puerto 4390 lo usa otra aplicación; revisar `docker compose ps` |
| "Usuario o contraseña incorrectos" con un usuario *Cliente* | Es lo esperado: el escritorio solo permite administradores y operadores |
| Visual Studio sugiere actualizar paquetes NuGet | No actualizar: las versiones están fijadas a propósito en `Directory.Packages.props` (compatibles con .NET 8) |
| Pide instalar .NET 9 o 10 | No: el proyecto usa **.NET 8** (`global.json`) |

La interfaz del escritorio es **la misma** que la de la web: casi todo se desarrolla y se prueba en el navegador (http://localhost:4392) y solo se verifica en el escritorio antes de cada entrega.

## 5. Receta: cómo se construye un módulo nuevo

**Usuarios es el módulo de referencia** (API, interfaz y pruebas completas): se copia su estructura, sin inventar otra. Orden recomendado (spec → dominio → API → interfaz):

| # | Paso | Dónde (ejemplo con Usuarios) |
|---|---|---|
| 1 | Leer la spec del módulo y actualizarla si algo cambia | `docs/Especificaciones/<Modulo>.md` |
| 2 | **Dominio**: reglas del agregado (la entidad ya existe, sin comportamiento) + pruebas unitarias | `Domain/<Area>/…`, `tests/…Domain.Tests` |
| 3 | **Casos de uso** (una clase cada uno) y puertos; publican el evento **después** de guardar | `Application/<Modulo>/…`, `tests/…Application.Tests` (con dobles en memoria, ver `Fakes.cs`) |
| 4 | **Repositorio** (EF Core) y registro en `DependencyInjection.AddGestionPedidos` | `Infrastructure/Persistence/Repositories/` |
| 5 | **DTO** en `Contracts` y **endpoints** con su permiso (`RequierePermiso(...)`) | `Contracts/…`, `Api/Modulos/<Modulo>/…Endpoints.cs` + `app.Map<Modulo>()` en `Program.cs` |
| 6 | **Pruebas de API** (401, 403 y éxito; datos ajenos si aplica) | `tests/…Api.Tests` |
| 7 | **Servicio** de la interfaz (publica `EntidadCambiada` tras la API) y su registro | `Ui/Modulos/<Modulo>/<Modulo>Service.cs`, `ServiciosUi.cs` |
| 8 | **ViewModel** (estado, validación, `Cambiado`) + pruebas | `Ui/Modulos/<Modulo>/<Modulo>ViewModel.cs`, `tests/…Ui.Tests` |
| 9 | **Página** `.razor` (solo marcado, usa el kit) y **una línea** en `ModulosAdmin.cs` | `Ui/Modulos/<Modulo>/<Modulo>Pagina.razor` |
| 10 | Prueba **Playwright** del recorrido principal | `e2e/tests/<modulo>.spec.ts` |

Reglas que no se negocian (resumen; completas en [PautasCodigoEventos.md](PautasCodigoEventos.md)): páginas sin lógica · evento solo tras confirmar la operación · todo `+=` con su `-=` en `Dispose` · comentarios XML en español en todo lo público (si falta uno, **no compila**) · consultas siempre parametrizadas · nada de colores ni CSS sueltos (todo sale de `tema.css` y del kit).

## 6. Migraciones de la base de datos

```bash
./scripts/ef-docker.sh migrations add NombreDeLaMigracion   # genera la migración
./scripts/generar-script-sql.sh                             # regenera el script SQL completo
```

* **Una persona a la vez**: dos migraciones simultáneas chocan en `GestionPedidosDbContextModelSnapshot`. Avisen en el chat antes de crearla; si otra entró primero, se borra la propia (`migrations remove`) y se vuelve a generar.
* **Nunca** se edita una migración que ya está en `main`: se crea una nueva.
* Se revisa el SQL que genera antes de subirla. Las tablas ya existen todas (Fase 0.5): lo normal es solo **agregar columnas o índices**.

## 7. Flujo de Git

* Una rama por módulo o apartado (`feat/clientes-pagina`), Pull Request a `main` con **una revisión** de otro integrante.
* **Commits pequeños y por apartado** (entidad, caso de uso, endpoint, ViewModel, página…), con título claro en español; cada commit deja el repositorio compilando.
* Antes de abrir el PR: `./scripts/test-docker.sh` en verde. **Quien escribe el código lo entiende y lo puede defender**: en la defensa se pregunta por cualquier parte.
* Sin co-autorías ni marcas de IA en commits ni PR ([AGENTS.md](../../AGENTS.md)).

## 8. Mapa de documentación

[Analisis](../Analisis.md) (qué se pidió) · [Arquitectura](../Arquitectura.md) · [Estructura](../Estructura.md) · [BaseDatos](../BaseDatos.md) · [Seguridad](../Seguridad.md) · [Tareas](Tareas.md) (qué toca a cada quien) · [Pautas de código](PautasCodigoEventos.md) · [Implementación](../Implementacion.md) (fases y estado).
