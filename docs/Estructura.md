# 🗂️ Estructura del Proyecto - Gestión de Pedidos (UFG)

> Estructura **objetivo**; se crea en la Fase 0 ([Implementacion.md](Implementacion.md)).

## 1. Árbol de la Solución

```
Gestion-de-Pedidos---UFG/
├── GestionPedidos.sln                   # todo
├── GestionPedidos.Linux.slnf            # filtro sin Desktop (lo que compila en Linux/Docker)
├── global.json                          # fija la versión del SDK
├── Directory.Build.props                # Nullable, TreatWarningsAsErrors, XML docs, analizadores
├── Directory.Packages.props             # versiones centralizadas de NuGet
├── .editorconfig
├── .env.example                         # variables de ejemplo (sin secretos)
├── seed/el-salvador-geo.json            # departamentos y ciudades de El Salvador (catálogo completo)
├── docker-compose.yml                   # local: sqlserver + api + web
├── AGENTS.md                            # reglas cortas para agentes de IA (apunta a docs/)
├── README.md
├── docs/                                # documentación (SDD)
│   ├── Apoyo/                           # enunciado y material del ingeniero
│   ├── Especificaciones/                # una spec por módulo
│   └── Equipo/                          # primeros pasos, pautas de código, tareas
├── scripts/                             # todo corre dentro de Docker
│   ├── up.sh / down.sh                  # levanta / apaga SQL Server + API + web
│   ├── dotnet-docker.sh · ef-docker.sh  # `dotnet ...` y migraciones de EF Core en contenedor
│   ├── test-docker.sh · e2e-docker.sh   # pruebas (unitarias, integración, arquitectura) y extremo a extremo
│   ├── db-backup.sh · db-restore.sh     # respaldo FULL y restauración verificada
│   ├── generar-script-sql.sh            # regenera scripts/sql/GestionPedidos_esquema.sql
│   └── sql/                             # init-usuarios.sql y GestionPedidos_esquema.sql (todas las tablas)
├── e2e/                                 # pruebas de extremo a extremo (Playwright, TypeScript)
├── src/
│   ├── GestionPedidos.Domain/           # entidades, reglas
│   ├── GestionPedidos.Application/      # casos de uso, puertos, eventos de aplicación
│   ├── GestionPedidos.Infrastructure/   # EF Core, migraciones, adaptadores
│   ├── GestionPedidos.Contracts/        # DTOs compartidos Api ↔ Application ↔ Ui
│   ├── GestionPedidos.Api/              # endpoints, auth, composición de DI
│   ├── GestionPedidos.Ui/               # Razor Class Library: TODAS las pantallas (administración y tienda)
│   ├── GestionPedidos.Web/              # Hospedaje web: Blazor Web App (Docker)
│   └── GestionPedidos.Desktop/          # Hospedaje de escritorio: .NET MAUI Blazor Hybrid (Windows)
└── tests/
    ├── GestionPedidos.Domain.Tests/
    ├── GestionPedidos.Application.Tests/
    ├── GestionPedidos.Infrastructure.Tests/   # Testcontainers (SQL Server)
    ├── GestionPedidos.Api.Tests/              # WebApplicationFactory
    ├── GestionPedidos.Ui.Tests/               # ViewModels, servicios, sesión y pestañas (sin navegador)
    └── GestionPedidos.Architecture.Tests/
```

## 2. Organización Interna (por módulo, no por tipo técnico)

Cada módulo (Clientes, Productos, Pedidos, Usuarios…) tiene sus archivos agrupados para que **dos personas trabajando en módulos distintos no toquen los mismos archivos**.

```
GestionPedidos.Application/
├── Clientes/
│   ├── RegistrarCliente.cs            # caso de uso
│   ├── ActualizarCliente.cs
│   ├── CambiarEstadoCliente.cs
│   ├── BuscarClientes.cs
│   ├── IClienteRepository.cs          # puerto
│   └── Eventos/                       # delegado + EventArgs del módulo
├── Pedidos/ · Productos/ · Usuarios/ · Autenticacion/ · Historial/
└── Common/                            # Result, IUnitOfWork, tipos de eventos compartidos
```

```
GestionPedidos.Infrastructure/
├── Persistence/
│   ├── GestionPedidosDbContext.cs
│   ├── Configurations/                # una IEntityTypeConfiguration<T> por entidad
│   ├── Migrations/                    # generadas (no editar)
│   ├── Repositories/                  # un repositorio por agregado
│   └── Seed/                          # semillas obligatorias (geografía, administrador) y DatosDeEjemplo (opcional, solo desarrollo)
└── DependencyInjection.cs
```

```
GestionPedidos.Api/
├── Modulos/
│   ├── Clientes/ClientesEndpoints.cs  # MapGroup("/api/v1/clientes")
│   ├── Productos/ · Pedidos/ · Usuarios/ · Auth/
├── Seguridad/                          # JWT, políticas de permisos
├── Errores/                            # manejo global → Problem Details
└── Program.cs                          # solo composición (llama a Add*/Map* de cada módulo)
```

```
GestionPedidos.Ui/                       # Razor Class Library (la única interfaz del proyecto)
├── Shell/                               # MainLayout (barra, menú por permisos, pestañas), Login, Integrantes
├── Modulos/                             # ADMINISTRACIÓN
│   ├── Clientes/
│   │   ├── ClientesPagina.razor         # marcado + enlace de eventos (sin lógica)
│   │   ├── ClientesViewModel.cs         # estado y lógica de interfaz (probable sin pantalla)
│   │   └── ClientesService.cs           # llama a la API y publica EntidadCambiada
│   ├── Productos/ · Pedidos/ · Usuarios/ · Historial/
├── Tienda/                              # TIENDA PÚBLICA: Catalogo, Registro, Carrito, MisPedidos, Perfil
├── Comun/
│   ├── Eventos/                         # EntidadCambiada*, StockInsuficiente*, Sesion*
│   ├── Api/                             # ApiClient, manejo de errores
│   ├── Sesion/                          # SesionService, ISesionAlmacen
│   ├── Notificaciones/                  # Notificador + componente de avisos
│   └── Componentes/                     # kit propio: Boton, CampoTexto, TablaDatos, Paginador, Dialogo…
├── ModulosAdmin.cs                      # registro de módulos del menú: cada módulo nuevo añade UNA línea
├── ServiciosUi.cs                       # AddGestionPedidosUi(): registro de servicios y ViewModels
└── wwwroot/css/tema.css                 # el ÚNICO lugar con colores, fuentes y espaciados

GestionPedidos.Web/                      # Program.cs, App.razor, appsettings.json (hospedaje web)
GestionPedidos.Desktop/                  # MauiProgram.cs, MainPage.xaml con BlazorWebView, appsettings.json
```

## 3. Nomenclatura

| Elemento | Convención | Ejemplo |
|---|---|---|
| Proyectos | `GestionPedidos.<Capa>` | `GestionPedidos.Domain` |
| Casos de uso | Verbo + sustantivo, una clase | `RegistrarCliente`, `CambiarEstadoPedido` |
| Eventos | Hecho en participio; delegado `XEventHandler`; datos `XEventArgs` | `EntidadCambiada` / `EntidadCambiadaEventHandler` / `EntidadCambiadaEventArgs` |
| Páginas, ViewModels y servicios | `<Modulo>Pagina.razor`, `<Modulo>ViewModel`, `<Modulo>Service` | `ClientesPagina.razor` |
| Componentes Razor | `PascalCase`, un archivo por componente | `TablaDatos.razor`, `CampoTexto.razor` |
| Manejadores de eventos | `<Origen>_<Evento>` (C#) / `Al<Accion>` (Razor) | `Servicio_EntidadCambiada`, `AlGuardar` |
| Pruebas | `<Clase>Tests`; método `Metodo_Escenario_Resultado` | `Pedido_Cancelar_Entregado_Falla` |
| Migraciones | PascalCase descriptivo | `CrearEsquemaInicial` |

Idioma y estilo: [Principios.md §7](Principios.md).

## 4. Qué NO va en el Repositorio

Además de lo cubierto por el `.gitignore` de .NET: `.env` reales, cadenas de conexión con credenciales, volúmenes de datos de SQL Server, respaldos `.bak` de trabajo (el respaldo **de entrega** se genera aparte), artefactos de publicación y `*.user`.
