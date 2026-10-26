# ✅ Tareas del Equipo (Fases 2 a 5)

> Este es el **tablero de tareas**. Las Fases 0, 0.5 y 1 ya están hechas (Pablo). Los bloques **C, P y D** son de los 3 integrantes (Pablo asigna cuál es de quién y anota el nombre en *Responsable*); el resto es de Pablo ([reparto](#reparto-del-equipo-acordado-el-25102026)).

**Tallas**: **S** ≤ medio día · **M** 1–2 días · **L** 3–4 días. **Depende de** indica qué debe estar listo antes de empezar.

**Cómo leer cada tarea**: cada una termina con su **prueba** y su **spec** actualizada. Las pantallas se escriben en la `Ui` con el tema y el kit de componentes ([InterfazUsuario.md §8](../Especificaciones/InterfazUsuario.md)); cada una aparece igual en escritorio y en web.

## Reparto del equipo (acordado el 25/10/2026)

| Quién | Responsabilidad |
|---|---|
| **Los 3 integrantes** (un bloque cada uno; Pablo asigna cuál) | Los **apartados administrativos** de **Clientes** (bloque C), **Productos** (bloque P) y **Pedidos con su detalle** (bloque D; el *detalle de pedido* son las líneas del pedido y se trabajan junto con el pedido) |
| **Pablo** | **Todo lo demás**: Usuarios (ya hecho), **gestión de roles y permisos**, **catálogos** (Departamentos y Ciudades) y demás vistas de consulta, **historial de actividades**, seguridad, la **entrega** (bloque E) y el **sitio público** (web) como responsable principal |
| **Sitio público** (provisional, ver abajo) | **Pablo**: catálogo, registro e inicio de sesión del cliente, carrito y hacer pedido, diseño. **Los integrantes**: pequeñas funcionalidades del cliente: **historial de pedidos**, **perfil** y **cambio de contraseña** |

> **Lo del sitio público aún está en discusión.** La web no es lo primordial de la materia (el escritorio es lo que el ingeniero evalúa). La idea inicial es que los integrantes tomen esas tres funcionalidades (W-07a, W-07b y W-07c); si se les complica o hay poco tiempo, **las hace Pablo**. Se confirma cuando terminen su primer bloque.

**Cómo se coordinan los tres** (sus bloques dependen entre sí: Pedidos usa Clientes y Productos):
* Al inicio acuerdan en **2 días** los contratos (`ClienteDto`, `ProductoDto`, `PedidoDto`) y los endpoints, para trabajar en paralelo sin esperarse.
* **Pedidos arranca por el dominio** (D-01), que no depende de nadie, y se conecta con Clientes y Productos cuando sus endpoints estén listos.
* Productos expone `DescontarStock`/`ReponerStock`, que usa Pedidos.
* Los **catálogos de Departamentos y Ciudades** (C-02) los entrega **Pablo primero**, porque el formulario de Clientes los necesita.
* Cada quien sigue la receta de [PrimerosPasos.md §5](PrimerosPasos.md#5-receta-cómo-se-construye-un-módulo-nuevo) y la estructura del módulo **Usuarios**.

---

## Bloque A — Administración transversal (a cargo de Pablo) · Specs: [AutenticacionYPermisos.md](../Especificaciones/AutenticacionYPermisos.md), [HistorialActividades.md](../Especificaciones/HistorialActividades.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| A-01 ✅ | **Gestión de roles y permisos**: página con la matriz rol × permiso (consulta y edición de los permisos de un rol), endpoint, evento e historial. Reglas a especificar: el rol Administrador no puede perder `admin.acceso`, `usuarios.*` ni `roles.*`; los cambios surten efecto al iniciar sesión de nuevo (el token lleva los permisos) | M | Fase 1 | Pablo |
| A-02 ✅ | **Catálogos**: vistas de consulta de Departamentos y Ciudades (con búsqueda y paginación) para la administración | S | C-02 | Pablo |
| A-03 ✅ | **Vistas de consulta** de las demás tablas de apoyo que haga falta mostrar (permisos, etc.), solo lectura | S | A-01 | Pablo |
| A-04 | **Gestión de usuarios**: mejoras que pidan las pruebas del equipo (la base ya está hecha en la Fase 1) | S | Fase 1 | Pablo |

---

## Bloque K — Catálogo de productos: categorías, subcategorías, filtros y opciones (Fase 2.5, a cargo de Pablo) · Spec: [CatalogoProductos.md](../Especificaciones/CatalogoProductos.md)

> **Nuevo (26/10/2026).** Versión básica, sin SKU ni variantes. Amplía la base de datos con 5 tablas ([BaseDatos.md §4.7](../BaseDatos.md)). Los integrantes de **Productos** no esperan: `SubcategoriaId` es opcional y suman P-10/P-11 cuando K-04 esté listo.

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| K-01 ✅ | Dominio: `Categoria`, `Subcategoria`, `Filtro`, `OpcionFiltro` (baja suave, reglas de nombres y de baja) y `Producto.EstablecerClasificacion(subcategoriaId, opciones)`; pruebas unitarias | M | Fase 0.5 | Pablo |
| K-02 ✅ | Configuración EF de las 5 tablas (únicos, FK compuesta, índices), permisos 29–33 al final del catálogo y **una** migración `AgregarCatalogoDeProductos`; regenerar el script SQL; pruebas de integración | M | K-01 | Pablo |
| K-03 ✅ | Casos de uso (árbol, altas, ediciones, bajas, reactivación, eliminación definitiva) con eventos de aplicación e historial; pruebas | M | K-02 | Pablo |
| K-04 ✅ | DTOs y endpoints `/categorias`, `/subcategorias`, `/filtros`, `/opciones` con validación, permisos y pruebas de API | M | K-03 | Pablo |
| K-05 ✅ | `CatalogoService` (publica `EntidadCambiada`) y `CategoriasViewModel`; pruebas | M | K-04 | Pablo |
| K-06 ✅ | `CategoriasPagina`: árbol, filtros con *chips* de opciones, estado, baja suave y eliminar definitivamente; E2E | L | K-05 | Pablo |
| K-07 | Integración con Productos: `ProductoDto`/solicitudes con subcategoría y detalles, validación de opciones y `GET /productos` con `categoriaId`/`subcategoriaId`/`opcionIds` (**se coordina con P-04**: una migración a la vez) | M | K-04, P-04 | Pablo + integrante de Productos |
| K-08 ✅ | Endpoints anónimos de la tienda (`/tienda/categorias`, `/catalogo` con filtros Y/O) y su uso en el catálogo público | M | K-04, W-04 | Pablo |
| K-10 ✅ | **Imágenes**: Garage (S3) en el compose, `Codigo` e `ImagenClave` en productos y categorías, endpoints para entregar/subir/quitar/reenlazar, fotos de ejemplo con créditos y la tienda mostrándolas ([§9 de la spec](../Especificaciones/CatalogoProductos.md)). **Productos (P-07):** el formulario debe permitir subir la foto con `PUT /api/v1/productos/{id}/imagen` y mostrarla; el `Codigo` lo genera la base si no se indica | M | K-04 | Pablo |
| K-09 | Datos de ejemplo realistas ✅ (sembrador `DatosDeEjemplo`, [§11 de la spec](../Especificaciones/CatalogoProductos.md)); falta el guion de demostración | S | K-06 | Pablo |

---

## Bloque C — Clientes (Fase 2) · *integrante del equipo* — · Spec: [Clientes.md](../Especificaciones/Clientes.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| C-01 | Reglas del dominio de `Cliente` (campos obligatorios, formato de teléfono; la entidad y su tabla ya existen) y pruebas unitarias | S | — | |
| C-02 | Endpoints de lectura de los catálogos: `GET /departamentos` y `/departamentos/{id}/ciudades` (con búsqueda por nombre y caché en memoria). Las entidades y los 14 + 262 registros **ya existen** (Fase 0.5) | S | — | Pablo (primero) |
| C-03 | Repositorio de clientes e integración con EF (consultas con filtros) y pruebas de integración | S | C-01 | |
| C-04 | Casos de uso `RegistrarCliente`, `ActualizarCliente`, `CambiarEstadoCliente` (con la regla de pedidos pendientes) y eventos de aplicación | M | C-01, C-03 | |
| C-05 | DTOs en `Contracts` y endpoints `/api/v1/clientes` con validación, permisos y pruebas de API | M | C-04 | |
| C-06 | `ClientesService` (cliente) que llama a la API y publica `EntidadCambiada`; pruebas | S | C-05 | |
| C-07 | `ClientesViewModel` (con evento `Cambiado`) con pruebas, sin navegador | M | C-06 | |
| C-08 | `ClientesPagina`: listado, alta/edición (correo opcional), baja/reactivación, selectores dependientes Departamento → Ciudad, validación visible | M | C-07 | |
| C-09 | Búsqueda de clientes (nombres, apellidos, teléfono, departamento, ciudad, estado) en API y página | M | C-08 | |
| C-10 | Spec y guion de demostración de Clientes actualizados | S | C-09 | |

## Bloque P — Productos (Fase 2) · *integrante del equipo* — · Spec: [Productos.md](../Especificaciones/Productos.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| P-01 ✅ | Entidad `Producto` con `DescontarStock/ReponerStock` (rechazan negativos) y pruebas unitarias — **ya hecho** al construir la tienda (`Producto.cs`, `StockTests`); solo faltan las reglas de los campos si se quieren | S | Fase 0.5 | |
| P-02 | Repositorio y configuración EF; pruebas de integración (`CHECK` de stock y precio) | S | P-01 | |
| P-03 | Casos de uso `RegistrarProducto`, `ActualizarProducto`, `CambiarEstadoProducto` y eventos de aplicación | M | P-01, P-02 | |
| P-04 | DTOs y endpoints `/api/v1/productos` con validación, permisos y pruebas | M | P-03 | |
| P-05 | `ProductosService` (cliente) con `EntidadCambiada`; pruebas | S | P-04 | |
| P-06 | `ProductosViewModel` con pruebas | M | P-05 | |
| P-07 | `ProductosPagina`: listado, alta/edición, baja/reactivación, indicador de stock bajo e interruptor "Mostrar existencias en la tienda" | M | P-06 | |
| P-08 | Búsqueda de productos (nombre/descripción, rango de precio, stock, fecha de ingreso, estado) | M | P-07 | |
| P-09 | Spec y guion de demostración de Productos | S | P-08 | |
| P-10 | Formulario de producto con selector de **Subcategoría** y un selector por filtro (aviso al cambiar de subcategoría) | M | K-07, P-07 | |
| P-11 | Búsqueda y listado por categoría, subcategoría y opciones de filtro | S | K-07, P-08 | |

## Bloque D — Pedidos y detalle de pedido (Fase 3) · *integrante del equipo* — · Spec: [Pedidos.md](../Especificaciones/Pedidos.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| D-01 | **Nota:** la tienda ya crea y cancela pedidos de cliente con reglas propias en `RealizarPedidoDeCliente` y `CancelarPedidoDeCliente` (`Application/Tienda/PedidosDeCliente.cs`); al hacer este dominio, **mueve esas reglas a `Pedido`** y deja que la tienda las use. Dominio `Pedido` y `DetallePedido`: líneas, total, transiciones de estado válidas, reglas de [Pedidos.md §3](../Especificaciones/Pedidos.md); pruebas unitarias exhaustivas | L | Fase 0.5 | |
| D-02 | Repositorio y carga de agregado con líneas; pruebas de integración | S | D-01 | |
| D-03 | Caso de uso `RegistrarPedido` (valida cliente y productos activos, copia precios, descuenta stock en una transacción) y evento `StockInsuficiente` | L | D-01, D-02, P-01, C-01 | |
| D-04 | Casos de uso `ActualizarPedido`, `CambiarEstadoPedido` (Entregado/Cancelado con reposición) y `DesactivarPedido` | L | D-03 | |
| D-05 | Concurrencia: pruebas de dos pedidos compitiendo por la última unidad y de doble cambio de estado | M | D-03 | |
| D-06 | `usp_Pedidos_Buscar` + `vw_PedidosResumen` integrados en el repositorio y probados | M | D-02 | |
| D-07 | DTOs y endpoints `/api/v1/pedidos` con validación, permisos y pruebas de API | M | D-04, D-06 | |
| D-08 | `PedidosService` (cliente) con `EntidadCambiada` y `StockInsuficiente`; pruebas | S | D-07 | |
| D-09 | `PedidosViewModel` con pruebas (maestro–detalle) | M | D-08 | |
| D-10 | `PedidosPagina`: cabecera + líneas editables, selección de cliente y productos, total en vivo, cambio de estado | L | D-09, C-08, P-07 | |
| D-11 | Búsquedas de pedidos en la página (cliente, producto, precio, estado, fechas, usuario) | M | D-10 | |
| D-12 | Spec y guion de demostración de Pedidos | S | D-11 | |

## Bloque H — Historial y notificaciones (Fases 2–4, Pablo) · Specs: [HistorialActividades.md](../Especificaciones/HistorialActividades.md), [PatronEventos.md](../Especificaciones/PatronEventos.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| H-01 | El `HistorialSubscriber` **ya existe** (Fase 1) y escucha `EntidadCambiada`: verificar que cada caso de uso nuevo de Clientes, Productos y Pedidos lo publique y que quede la fila en `Actividades` (prueba de integración por módulo) | S | Fase 1 | |
| H-02 | Endpoint y `HistorialPagina` (solo administrador) con filtros por usuario, entidad y fechas | M | H-01 | Pablo |
| H-03 | `Notificador` de barra de estado y suscripción de listados abiertos para refrescarse (`EntidadCambiada`) | M | C-06 | Pablo |
| H-04 | Verificar que los menús y botones de cada módulo respetan los permisos; prueba manual por rol | S | C-08, P-07 | Pablo |
| H-05 | Revisión transversal de eventos: baja de suscripciones (`-=`) en el `Dispose` de todas las páginas y ViewModels | S | D-10 | Pablo |

## Bloque E — Entrega (Fase 5, Pablo con apoyo de todos) · Spec: [RespaldoYEntrega.md](../Especificaciones/RespaldoYEntrega.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| E-01 | Seeder de **datos de demostración** (clientes, productos y pedidos ficticios y coherentes) | M | C, P, D | Pablo |
| E-02 | Respaldo FULL final, script `.sql` idempotente y **prueba de restauración** documentada | M | E-01 | Pablo |
| E-03 | Publicación del `.exe` y ensayo completo de la defensa local ([Infraestructura.md §5](../Infraestructura.md)) | M | todo | Pablo |
| E-04 | PDF de entrega con nombres y carnets, carpeta comprimida y verificación del contenido | S | E-02 | Pablo |
| E-05 | Guion de defensa, reparto de turnos y banco de preguntas probables; ensayo completo | M | todo | Todo el equipo |
| E-06 | Revisión de comentarios XML (RE-01) y de la rúbrica ([Analisis.md §2.4](../Analisis.md)) | S | todo | Pablo |

## Bloque W — Sitio público (Fase Web: Pablo, con tres funcionalidades provisionales para el equipo) · Spec: [PortalWeb.md](../Especificaciones/PortalWeb.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| W-01 (parcial ✅: faltan `mi-perfil` y el límite de peticiones al crear pedidos) | Endpoints públicos y de cliente en la API: `catalogo`, `auth/registro`, `mis-pedidos`, `mi-perfil`, con *rate limiting* y pruebas (incluye acceso a datos ajenos denegado) | L | Fase 1, C-05, P-04, D-07 | Pablo |
| W-02 ✅ | Área `Tienda/` en la `Ui` (la sesión de la web con `ProtectedSessionStorage` y el `ApiClient` **ya existen**) | S | — | Pablo |
| W-03 ✅ | Diseño base: layout, navegación, tema | M | W-02 | Pablo |
| W-04 ✅ | Catálogo: listado paginado con búsqueda y detalle de producto | M | W-01, W-03 | Pablo |
| W-05 ✅ | Registro e inicio de sesión de clientes (departamento → ciudad) | M | W-01, W-03 | Pablo |
| W-06 ✅ | Carrito y "Hacer pedido" | L | W-04, W-05 | Pablo |
| W-07a ✅ | **Ya hecho por Pablo** (no hace falta asignarlo; `MisPedidosPagina`). Historial de pedidos del cliente: lista y detalle de sus pedidos y cancelar si está `Pendiente` (`mis-pedidos`) | M | W-06 | Integrante (provisional; si no, Pablo) |
| W-07b | **Perfil del cliente**: ver y editar sus datos (`mi-perfil`) | S | W-05 | Integrante (provisional; si no, Pablo) |
| W-07c | **Cambio de contraseña del cliente** en la tienda (la lógica y la página base ya existen desde la Fase 1: reutilizarlas con el diseño de la tienda) | S | W-05 | Integrante (provisional; si no, Pablo) |
| W-08 (Playwright ✅; falta el recorrido manual) | Prueba Playwright del recorrido de la tienda y recorrido manual (el `Dockerfile` y el servicio `web` ya existen) | M | W-07a | Pablo |
