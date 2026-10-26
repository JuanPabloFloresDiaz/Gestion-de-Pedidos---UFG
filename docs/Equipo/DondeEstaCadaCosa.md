# 🗺️ Dónde Está Cada Cosa

> Mapa rápido del repositorio para el equipo. Todas las rutas son relativas a la raíz (`Gestion-de-Pedidos---UFG/`). Si agregan algo nuevo que otros deban encontrar, **añadan su línea aquí**.

## 1. Datos de ejemplo (el *seeder*)

Lo que llena la base con datos realistas para desarrollar y demostrar: categorías, subcategorías, filtros y opciones, **118 productos**, clientes, personal, pedidos, historial e imágenes.

| Qué | Dónde |
|---|---|
| **El sembrador (lógica)**: crea usuarios, clientes, pedidos, historial y sube las imágenes | [`src/GestionPedidos.Infrastructure/Persistence/Seed/DatosDeEjemplo.cs`](../../src/GestionPedidos.Infrastructure/Persistence/Seed/DatosDeEjemplo.cs) |
| **Los datos del catálogo**: categorías, subcategorías, filtros con sus opciones y **todos los productos** (nombre, descripción, precio, stock y características) | [`src/GestionPedidos.Infrastructure/Persistence/Seed/CatalogoDeEjemplo.cs`](../../src/GestionPedidos.Infrastructure/Persistence/Seed/CatalogoDeEjemplo.cs) |
| **Las imágenes de ejemplo** (se llaman como el **código** del producto o categoría) | [`seed/imagenes/productos/`](../../seed/imagenes/productos) y [`seed/imagenes/categorias/`](../../seed/imagenes/categorias) |
| Autores y licencias de esas imágenes | [`seed/imagenes/CREDITOS.md`](../../seed/imagenes/CREDITOS.md) |
| Administrador inicial (`admin`) | [`.../Persistence/Seed/AdministradorInicial.cs`](../../src/GestionPedidos.Infrastructure/Persistence/Seed/AdministradorInicial.cs) |
| Departamentos y distritos de El Salvador (14 + 262) | [`.../Persistence/Seed/GeoSeed.cs`](../../src/GestionPedidos.Infrastructure/Persistence/Seed/GeoSeed.cs) lee [`seed/el-salvador-geo.json`](../../seed/el-salvador-geo.json) |
| Roles y permisos de base | `CatalogoPermisos` en [`src/GestionPedidos.Domain/Seguridad/CatalogoPermisos.cs`](../../src/GestionPedidos.Domain/Seguridad/CatalogoPermisos.cs) (la migración los inserta) |
| **Quién lo ejecuta y cuándo**: al arrancar la API, con la base vacía | [`src/GestionPedidos.Api/Program.cs`](../../src/GestionPedidos.Api/Program.cs) (bloque «Arranque de la base de datos») |
| **Cómo se enciende o apaga** | variable `SEMBRAR_DATOS_EJEMPLO` en `.env` (por defecto `true`; lo pasa [`docker-compose.yml`](../../docker-compose.yml) como `Database__SembrarDatosDeEjemplo`) |
| Pruebas del sembrador | [`tests/GestionPedidos.Infrastructure.Tests/DatosDeEjemploTests.cs`](../../tests/GestionPedidos.Infrastructure.Tests/DatosDeEjemploTests.cs) |
| Explicación y lista de lo que siembra (usuarios, contraseña `Demo_Sembrado_2026`, cantidades) | [`docs/Especificaciones/CatalogoProductos.md`](../Especificaciones/CatalogoProductos.md) §11 |

**Reglas del sembrador**
* **Solo corre si la base está vacía** (sin categorías, productos ni clientes): no duplica. Para volver a sembrar desde cero: `./scripts/up.sh --limpio` (borra la base **y** las imágenes de Garage).
* Es **determinista**: con la misma semilla genera los mismos datos.
* Guarda en **dos lugares**: los datos en **SQL Server** y las imágenes en **Garage** (S3); la base solo guarda la *clave* (`ImagenClave`) y el nombre del archivo es el **código** del producto.

**Cómo agregar o cambiar un producto de ejemplo**
1. Abrir `CatalogoDeEjemplo.cs`, buscar la subcategoría y añadir una línea `P("Nombre", "Descripción", precio, stock, "Filtro=Valor;Filtro=Valor")`. Los filtros que no se indiquen se completan solos.
2. Su **código** se arma solo: `{categoría}-{subcategoría}-{número}` según el orden en la lista (por ejemplo `TEC-LAP-036`). **Ojo:** insertar un producto en medio **corre los números** de los siguientes y con ellos el nombre de la imagen que les corresponde; añadan siempre al **final** de la subcategoría.
3. (Opcional) Soltar su foto en `seed/imagenes/productos/` con **exactamente** el nombre del código (`TEC-LAP-036.jpg`, JPEG).
4. `./scripts/up.sh --limpio` para ver el resultado.

## 2. Imágenes y Garage (almacén S3)

| Qué | Dónde |
|---|---|
| Servicio `garage` (contenedor) y sus variables | [`docker-compose.yml`](../../docker-compose.yml) y [`.env.example`](../../.env.example) (`GARAGE_*`) |
| Configuración de Garage (un nodo, desarrollo) | [`docker/garage/garage.toml`](../../docker/garage/garage.toml) |
| Cliente S3 de la API (guardar, leer, listar, borrar) | [`src/GestionPedidos.Infrastructure/Almacen/AlmacenImagenesS3.cs`](../../src/GestionPedidos.Infrastructure/Almacen/AlmacenImagenesS3.cs) (versión en memoria para pruebas: `AlmacenImagenesEnMemoria.cs`) |
| Reglas: nombres por código, validación por bytes, subir, quitar y **reenlazar** | [`src/GestionPedidos.Application/Imagenes/Imagenes.cs`](../../src/GestionPedidos.Application/Imagenes/Imagenes.cs) |
| Endpoints de imágenes | [`src/GestionPedidos.Api/Modulos/Imagenes/ImagenesEndpoints.cs`](../../src/GestionPedidos.Api/Modulos/Imagenes/ImagenesEndpoints.cs) |
| Proxy `/imagenes/...` del sitio web hacia la API | [`src/GestionPedidos.Web/Program.cs`](../../src/GestionPedidos.Web/Program.cs) |
| Columnas `Codigo` e `ImagenClave` | entidades `Producto` ([`Domain/Productos/Producto.cs`](../../src/GestionPedidos.Domain/Productos/Producto.cs)) y `Categoria` ([`Domain/Catalogo/Catalogo.cs`](../../src/GestionPedidos.Domain/Catalogo/Catalogo.cs)) |
| Cómo se muestran en pantalla (foto + ícono de respaldo) | [`src/GestionPedidos.Ui/Tienda/VisualProducto.razor`](../../src/GestionPedidos.Ui/Tienda/VisualProducto.razor) |
| Diseño completo, recuperación si se pierde la base | [`docs/Especificaciones/CatalogoProductos.md`](../Especificaciones/CatalogoProductos.md) §9 |

## 3. Por capas (dónde va cada tipo de archivo)

| Capa | Carpeta | Qué contiene |
|---|---|---|
| **Domain** | `src/GestionPedidos.Domain/` | Entidades y reglas puras: `Seguridad/` (usuario, rol, permisos), `Clientes/`, `Productos/`, `Catalogo/` (categoría, subcategoría, filtro, opción), `Pedidos/`, `Historial/`, `Geografia/` |
| **Contracts** | `src/GestionPedidos.Contracts/` | DTOs y solicitudes que viajan por la API (los comparten la API y la interfaz); constantes de permisos y códigos de error |
| **Application** | `src/GestionPedidos.Application/` | Casos de uso (uno por clase) y puertos (interfaces): `Usuarios/`, `Roles/`, `Catalogo/` (clasificación), `Tienda/` (catálogo público y pedidos de cliente), `Imagenes/`, `Historial/`, `Eventos/` |
| **Infrastructure** | `src/GestionPedidos.Infrastructure/` | EF Core y adaptadores: `Persistence/Configurations/` (mapa de tablas), `Persistence/Repositories/`, `Persistence/Migrations/`, `Persistence/Seed/`, `Almacen/` (S3), `Seguridad/` (hash de claves) |
| **Api** | `src/GestionPedidos.Api/` | Minimal APIs agrupadas por módulo en `Modulos/` (`Auth`, `Usuarios`, `Roles`, `Catalogos`, `Clasificacion`, `Tienda`, `Imagenes`, `Historial`); `Seguridad/` (JWT y políticas); `Program.cs` |
| **Web** | `src/GestionPedidos.Web/` | Hospedaje web de la interfaz (Blazor Web App) |
| **Desktop** | `src/GestionPedidos.Desktop/` | Hospedaje de escritorio (MAUI Blazor Hybrid, solo Windows) |
| **Ui** | `src/GestionPedidos.Ui/` | **Toda** la interfaz (la usan Web y Desktop): ver el siguiente apartado |

**Dónde agrego un módulo nuevo** (por ejemplo Clientes): la receta paso a paso está en [`PrimerosPasos.md` §5](PrimerosPasos.md#5-receta-cómo-se-construye-un-módulo-nuevo) y el módulo **Usuarios** es el modelo a copiar.

## 4. La interfaz (`src/GestionPedidos.Ui/`)

| Carpeta o archivo | Contenido |
|---|---|
| `Shell/` | Inicio de sesión, cambio de clave, **la administración** (`AdminPagina.razor`: menú, pestañas «+» y recarga) y la portada de la tienda (`InicioPagina.razor`) |
| `Modulos/<Módulo>/` | Cada pantalla de administración: `…Pagina.razor` + `…ViewModel.cs` + `…Service.cs` (Usuarios, Roles, Categorias, Catalogos, Historial, Ayuda) |
| `ModulosAdmin.cs` | **El menú**: cada módulo nuevo añade **una línea** aquí |
| `Tienda/` | La tienda pública: catálogo, producto, carrito, registro, pedido, mis pedidos, y sus servicios y ViewModels |
| `Comun/Componentes/` | El **kit de componentes** (botón, campo de texto, tabla, diálogo de confirmación, chip de estado, `Icono`, paginador…). **Úsenlo siempre** en vez de HTML suelto |
| `Comun/` (resto) | `Api/` (cliente HTTP), `Sesion/`, `Eventos/` (delegados y eventos), `Notificaciones/`, `Documentos/` (pestañas), `Apariencia/` (tema), `ViewModelBase.cs`, `Validacion.cs` |
| `wwwroot/css/tema.css` | **El tema**: colores, modo claro/oscuro y acentos. Es el único lugar con colores |
| `wwwroot/css/tienda.css` | Solo la **disposición** de la tienda |
| `ServiciosUi.cs` | Registro de servicios y ViewModels (hay que añadir aquí los nuevos) |

## 5. Base de datos

| Qué | Dónde |
|---|---|
| Entidades | `src/GestionPedidos.Domain/…` (ver §3) |
| Mapa de tablas (índices, restricciones, claves) | `src/GestionPedidos.Infrastructure/Persistence/Configurations/` |
| El `DbContext` | `src/GestionPedidos.Infrastructure/Persistence/GestionPedidosDbContext.cs` |
| Migraciones (**una a la vez**, avisen al equipo) | `src/GestionPedidos.Infrastructure/Persistence/Migrations/` |
| Script SQL completo y idempotente (se regenera con `generar-script-sql.sh`) | [`scripts/sql/GestionPedidos_esquema.sql`](../../scripts/sql/GestionPedidos_esquema.sql) |
| Diseño de las tablas y reglas de integridad | [`docs/BaseDatos.md`](../BaseDatos.md) |
| Respaldo y restauración | `scripts/db-backup.sh` y `scripts/db-restore.sh` ([`RespaldoYEntrega.md`](../Especificaciones/RespaldoYEntrega.md)) |

## 6. Docker y scripts

| Qué | Dónde |
|---|---|
| Servicios (`sqlserver`, `db-init`, `garage`, `api`, `web`), puertos y variables | [`docker-compose.yml`](../../docker-compose.yml), [`.env.example`](../../.env.example) y [`docs/Infraestructura.md`](../Infraestructura.md) |
| Dockerfiles | `src/GestionPedidos.Api/Dockerfile`, `src/GestionPedidos.Web/Dockerfile` |
| Levantar / apagar todo | `./scripts/up.sh` (`--limpio` borra datos) · `./scripts/down.sh` |
| Pruebas y E2E en Docker | `./scripts/test-docker.sh` · `./scripts/e2e-docker.sh` |
| Migraciones y script SQL | `./scripts/ef-docker.sh` · `./scripts/generar-script-sql.sh` |

## 7. Pruebas

| Qué | Dónde |
|---|---|
| Dominio, aplicación, infraestructura (SQL Server y Garage reales), API, interfaz y arquitectura | `tests/GestionPedidos.<Capa>.Tests/` |
| Extremo a extremo (Playwright: administración y tienda) | `e2e/tests/administracion.spec.ts`, `e2e/tests/tienda.spec.ts` (capturas en `e2e/capturas/`, que no se sube) |
| Qué probar en cada módulo | [`docs/Especificaciones/Testing.md`](../Especificaciones/Testing.md) |

## 8. Documentación

| Quiero saber… | Leer |
|---|---|
| Qué pide el enunciado y cómo lo interpretamos | [`docs/Analisis.md`](../Analisis.md) |
| Cómo se escribe el código (delegados, eventos, Página + ViewModel) | [`PautasCodigoEventos.md`](PautasCodigoEventos.md) |
| Cómo levantar el proyecto y conectarme a la base | [`PrimerosPasos.md`](PrimerosPasos.md) |
| Qué tarea me toca | [`Tareas.md`](Tareas.md) |
| Capas, dependencias permitidas, estructura | [`Arquitectura.md`](../Arquitectura.md) · [`Estructura.md`](../Estructura.md) |
| Especificación de cada módulo | `docs/Especificaciones/` (índice en su `README.md`) |
| Seguridad, permisos y roles | [`Seguridad.md`](../Seguridad.md) · [`AutenticacionYPermisos.md`](../Especificaciones/AutenticacionYPermisos.md) |
| Catálogo (categorías, filtros), imágenes y datos de ejemplo | [`CatalogoProductos.md`](../Especificaciones/CatalogoProductos.md) |
| La tienda pública | [`PortalWeb.md`](../Especificaciones/PortalWeb.md) |
| Reglas cortas para agentes de IA | [`AGENTS.md`](../../AGENTS.md) |
