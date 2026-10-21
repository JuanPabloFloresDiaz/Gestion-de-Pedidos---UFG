# ✅ Tareas del Equipo (Fases 2 a 5)

> Este es el **tablero de tareas**; la columna *Responsable* queda vacía para que Pablo asigne. Las Fases 0, 0.5 y 1 y la web (bloque W) las desarrolla Pablo ([Implementacion.md](../Implementacion.md)).

**Tallas**: **S** ≤ medio día · **M** 1–2 días · **L** 3–4 días. **Depende de** indica qué debe estar listo antes de empezar.

**Cómo leer cada tarea**: cada una termina con su **prueba** y su **spec** actualizada. Las pantallas se escriben en la `Ui` con el tema y el kit de componentes ([InterfazUsuario.md §8](../Especificaciones/InterfazUsuario.md)); cada una aparece igual en escritorio y en web.

## Sugerencia de reparto en 4 paquetes (balanceados)

| Paquete | Contenido | Observación |
|---|---|---|
| **A — Clientes** | Bloque **C** (incluye catálogos geográficos) | Independiente |
| **B — Productos y datos de demo** | Bloque **P** + **E1** | Independiente; Productos expone `DescontarStock/ReponerStock` que usa Pedidos |
| **C — Pedidos** | Bloque **D** | Arranca con el dominio (D1), luego necesita C y P |
| **D — Transversal y entrega** | Bloques **H** y **E2–E6** | Se apoya en todos; empieza con historial y notificaciones |

> Al inicio de la Fase 2, **A y B acuerdan en 2 días los DTOs** (`ClienteDto`, `ProductoDto`) y los contratos de la API, para que Pedidos pueda avanzar sin esperar.

---

## Bloque C — Clientes y catálogos geográficos (Fase 2) · Spec: [Clientes.md](../Especificaciones/Clientes.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| C-01 | Entidad `Cliente` con reglas (campos obligatorios, formato de teléfono) y pruebas unitarias | S | Fase 0.5 | |
| C-02 | Catálogos `Departamento` y `Ciudad`: entidades, carga de la semilla desde `seed/el-salvador-geo.json` (14 departamentos y 262 ciudades; el archivo ya está en el repositorio), endpoints de lectura | M | Fase 0.5 | |
| C-03 | Repositorio de clientes e integración con EF (consultas con filtros) y pruebas de integración | S | C-01 | |
| C-04 | Casos de uso `RegistrarCliente`, `ActualizarCliente`, `CambiarEstadoCliente` (con la regla de pedidos pendientes) y eventos de aplicación | M | C-01, C-03 | |
| C-05 | DTOs en `Contracts` y endpoints `/api/v1/clientes` con validación, permisos y pruebas de API | M | C-04 | |
| C-06 | `ClientesService` (cliente) que llama a la API y publica `EntidadCambiada`; pruebas | S | C-05 | |
| C-07 | `ClientesViewModel` (con evento `Cambiado`) con pruebas, sin navegador | M | C-06 | |
| C-08 | `ClientesPagina`: listado, alta/edición (correo opcional), baja/reactivación, selectores dependientes Departamento → Ciudad, validación visible | M | C-07 | |
| C-09 | Búsqueda de clientes (nombres, apellidos, teléfono, departamento, ciudad, estado) en API y página | M | C-08 | |
| C-10 | Spec y guion de demostración de Clientes actualizados | S | C-09 | |

## Bloque P — Productos (Fase 2) · Spec: [Productos.md](../Especificaciones/Productos.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| P-01 | Entidad `Producto` con `DescontarStock/ReponerStock` (rechazan negativos) y pruebas unitarias | S | Fase 0.5 | |
| P-02 | Repositorio y configuración EF; pruebas de integración (`CHECK` de stock y precio) | S | P-01 | |
| P-03 | Casos de uso `RegistrarProducto`, `ActualizarProducto`, `CambiarEstadoProducto` y eventos de aplicación | M | P-01, P-02 | |
| P-04 | DTOs y endpoints `/api/v1/productos` con validación, permisos y pruebas | M | P-03 | |
| P-05 | `ProductosService` (cliente) con `EntidadCambiada`; pruebas | S | P-04 | |
| P-06 | `ProductosViewModel` con pruebas | M | P-05 | |
| P-07 | `ProductosPagina`: listado, alta/edición, baja/reactivación, indicador de stock bajo e interruptor "Mostrar existencias en la tienda" | M | P-06 | |
| P-08 | Búsqueda de productos (nombre/descripción, rango de precio, stock, fecha de ingreso, estado) | M | P-07 | |
| P-09 | Spec y guion de demostración de Productos | S | P-08 | |

## Bloque D — Pedidos (Fase 3) · Spec: [Pedidos.md](../Especificaciones/Pedidos.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| D-01 | Dominio `Pedido` y `DetallePedido`: líneas, total, transiciones de estado válidas, reglas de [Pedidos.md §3](../Especificaciones/Pedidos.md); pruebas unitarias exhaustivas | L | Fase 0.5 | |
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

## Bloque H — Transversal (Fases 2–4) · Specs: [HistorialActividades.md](../Especificaciones/HistorialActividades.md), [PatronEventos.md](../Especificaciones/PatronEventos.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| H-01 | `HistorialSubscriber` (suscriptor de eventos de aplicación → tabla `Actividades`) con pruebas | M | Fase 1 | |
| H-02 | Endpoint y `HistorialPagina` (solo administrador) con filtros por usuario, entidad y fechas | M | H-01 | |
| H-03 | `Notificador` de barra de estado y suscripción de listados abiertos para refrescarse (`EntidadCambiada`) | M | C-06 | |
| H-04 | Verificar que los menús y botones de cada módulo respetan los permisos; prueba manual por rol | S | C-08, P-07 | |
| H-05 | Revisión transversal de eventos: baja de suscripciones (`-=`) en el `Dispose` de todas las páginas y ViewModels | S | D-10 | |

## Bloque E — Entrega (Fase 5) · Spec: [RespaldoYEntrega.md](../Especificaciones/RespaldoYEntrega.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| E-01 | Seeder de **datos de demostración** (clientes, productos y pedidos ficticios y coherentes) | M | C, P, D | |
| E-02 | Respaldo FULL final, script `.sql` idempotente y **prueba de restauración** documentada | M | E-01 | |
| E-03 | Publicación del `.exe` y ensayo completo de la defensa local ([Infraestructura.md §5](../Infraestructura.md)) | M | todo | |
| E-04 | PDF de entrega con nombres y carnets, carpeta comprimida y verificación del contenido | S | E-02 | |
| E-05 | Guion de defensa, reparto de turnos y banco de preguntas probables; ensayo completo | M | todo | |
| E-06 | Revisión de comentarios XML (RE-01) y de la rúbrica ([Analisis.md §2.4](../Analisis.md)) | S | todo | |

## Bloque W — Portal web público (Fase Web, a cargo de Pablo) · Spec: [PortalWeb.md](../Especificaciones/PortalWeb.md)

| ID | Tarea | Talla | Depende de | Responsable |
|---|---|---|---|---|
| W-01 | Endpoints públicos y de cliente en la API: `catalogo`, `auth/registro`, `mis-pedidos`, `mi-perfil`, con *rate limiting* y pruebas (incluye acceso a datos ajenos denegado) | L | Fase 1, C-05, P-04, D-07 | Pablo |
| W-02 | Área `Tienda/` en la `Ui` y sesión de la web con `ProtectedSessionStorage` | M | Fase 0 | Pablo |
| W-03 | Diseño base: layout, navegación, tema | M | W-02 | Pablo |
| W-04 | Catálogo: listado paginado con búsqueda y detalle de producto | M | W-01, W-03 | Pablo |
| W-05 | Registro e inicio de sesión de clientes (departamento → ciudad) | M | W-01, W-03 | Pablo |
| W-06 | Carrito y "Hacer pedido" | L | W-04, W-05 | Pablo |
| W-07 | Mis pedidos (lista, detalle, cancelar) y perfil | M | W-06 | Pablo |
| W-08 | `Dockerfile` y servicio `web` en los compose; pruebas con bUnit y recorrido manual | M | W-07 | Pablo |
