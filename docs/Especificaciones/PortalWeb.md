# 🛍️ Tienda Pública (área de clientes de la `Ui`)

> Cubre **RF-11**. Es el **área pública** de la misma interfaz Blazor (`GestionPedidos.Ui`, carpeta `Tienda/`); se ve en el hospedaje web y, si se quiere, también en el de escritorio. A cargo de **Pablo**. **Se presenta el 12/11/2026.** Consume la **misma API** y respeta las mismas reglas de negocio. La administración está en [InterfazUsuario.md](InterfazUsuario.md).

> **Reparto (provisional)**: Pablo lleva la tienda (catálogo, registro e inicio de sesión, carrito y hacer pedido). Tres funcionalidades del cliente —**historial de pedidos**, **perfil** y **cambio de contraseña**— se ofrecen a los integrantes del equipo; si se complican, las hace Pablo ([Tareas.md](../Equipo/Tareas.md#reparto-del-equipo-acordado-el-25102026)).

## 1. Objetivo y Alcance

Una tienda en línea mínima y funcional: un visitante explora el catálogo, se registra, inicia sesión, hace pedidos y consulta o cancela los suyos.

| Incluido | Fuera de alcance (por ahora) |
|---|---|
| Catálogo público con búsqueda | Pasarela de pago en línea (opción comentada en [Analisis.md §6](../Analisis.md)) |
| Registro e inicio de sesión de clientes | Verificación de correo, recuperación de contraseña por correo |
| Carrito y creación de pedidos propios | Panel de administración separado (la administración es otra área de la misma interfaz) |
| Mis pedidos (lista, detalle, cancelar si está `Pendiente`) | Envío de notificaciones por correo |
| Perfil del cliente | |

## 2. Páginas

| Ruta | Acceso | Contenido | Estado |
|---|---|---|---|
| `/` | Público | Portada: promoción, categorías en tarjetas pastel y "recién llegados" (en el escritorio redirige al ingreso) | ✅ |
| `/tienda` | Público | Catálogo: categorías y filtros a la izquierda; búsqueda, precio, "solo disponibles", orden y paginación. Los filtros viven en la dirección (`?sub=5&op=10,12&min=…`) | ✅ |
| `/producto/{id}` | Público | Detalle: imagen, precio, disponibilidad, cantidad (con tope por pedido), características y productos relacionados | ✅ |
| `/carrito` | Público | Líneas, cantidades y total; se guarda en el navegador. "Hacer pedido" pide iniciar sesión | ✅ |
| `/registro` · `/ingresar` | Público | Alta de cliente con departamento → distrito y enlace con un cliente existente · inicio de sesión (vuelve a donde se estaba con `?retorno=`) | ✅ |
| `/pedido` | Cliente | Confirmar: datos de entrega de la cuenta, fecha deseada y comentarios | ✅ |
| `/mis-pedidos` | Cliente | Lista paginada con estado, líneas y total; "Cancelar pedido" si está `Pendiente` | ✅ |
| `/perfil` | Cliente | Datos personales y cambio de contraseña | ⏳ W-07b y W-07c |

## 3. Reglas

* Un pedido hecho en la web es un `Pedido` normal: `ClienteId` = el cliente enlazado al usuario; `UsuarioId` = ese mismo usuario ([Pedidos.md](Pedidos.md)). Mismas reglas de stock, precio copiado y estados.
* El cliente solo ve y cancela **sus** pedidos; no puede editar líneas después de crearlo (cancela y vuelve a pedir).
* El catálogo público muestra solo productos **activos** y, según el interruptor `MostrarStock` de cada producto ([Productos.md](Productos.md)), la **cantidad exacta** o solo "Disponible" / "Agotado" (por defecto, lo segundo).
* La tienda se puede **explorar por categoría y subcategoría** y filtrar por las **opciones** de sus filtros (Talla 42, Color Negro…), con los endpoints anónimos `GET /tienda/categorias` y `GET /catalogo?subcategoriaId=&opcionIds=` ([CatalogoProductos.md §5](CatalogoProductos.md)); solo se muestran elementos activos. El detalle de producto lista sus opciones.
* La fecha de entrega deseada debe ser posterior a hoy (hora de El Salvador) y estar dentro de los próximos 60 días. Máximo **10 unidades** de un mismo producto por pedido. El pago es **al recibir** (no hay pasarela).
* **Dos clientes por la última unidad**: la concurrencia optimista (`RowVersion` del producto) deja pasar a uno y al otro le responde `409` con un mensaje claro; el stock nunca queda negativo.
* **Cancelar** un pedido `Pendiente` repone el stock en la misma transacción; un pedido que ya no está pendiente no se puede cancelar.
* **Registro** ([AutenticacionYPermisos.md §2](AutenticacionYPermisos.md)): crea el usuario (rol `Cliente`) y su `Clientes` en una transacción. Con el correo escrito:
  * **Ya existe una cuenta** con ese correo → "Ya existe una cuenta con este correo: inicia sesión o usa otro correo".
  * **Existe un cliente sin cuenta** con ese correo (lo registró un operador) → la web pregunta "Encontramos un cliente registrado con este correo. ¿Eres tú? Confirma tu teléfono para enlazar tu cuenta, o usa otro correo". Si el teléfono coincide con el registrado, se **enlaza** (no se duplica); si no, se rechaza. Con límite de intentos.
  * **No existe** → se crea un cliente nuevo.
* Los mismos datos geográficos (departamento → ciudad) con las listas dependientes, cargados desde la API.

## 4. API Pública y de Cliente

| Método y ruta | Acceso | Descripción |
|---|---|---|
| `GET /api/v1/tienda/categorias` | Anónimo | Menú: categorías → subcategorías → filtros → opciones activos, con cantidad de productos disponibles |
| `GET /api/v1/catalogo` · `GET /api/v1/catalogo/{id}` | Anónimo | Productos activos, paginados (máx. 48), con `q`, `categoriaId`, `subcategoriaId`, `opcionIds` (Y entre filtros, O dentro de uno), `precioMin`, `precioMax`, `soloDisponibles` y `orden` (`nuevos`, `precio-asc`, `precio-desc`, `nombre`). Con límite de peticiones por IP |
| `GET /api/v1/departamentos` · `/{id}/ciudades` | Anónimo | Catálogos para el registro |
| `POST /api/v1/auth/registro` · `POST /api/v1/auth/login` | Anónimo | Con límite de peticiones |
| `GET /api/v1/mis-pedidos/entrega` | `mispedidos.crear` | Datos de entrega del cliente de la cuenta (nombre, dirección, ciudad, teléfono) |
| `GET /api/v1/mis-pedidos` · `GET /api/v1/mis-pedidos/{id}` | `mispedidos.ver` | Solo los del cliente del token (uno ajeno responde 404) |
| `POST /api/v1/mis-pedidos` | `mispedidos.crear` | Crea con `[{productoId, cantidad}]`, fecha de entrega y comentarios. **El precio no viaja**: lo pone el servidor. En una transacción valida, descuenta stock y crea el pedido `Pendiente`. Errores: `400` (fecha, líneas), `409 STOCK_INSUFICIENTE` o por compra simultánea, `422` (producto ya no disponible) |
| `PATCH /api/v1/mis-pedidos/{id}/cancelar` | `mispedidos.cancelar` | Solo si es suyo y está `Pendiente` |
| `GET /api/v1/mi-perfil` · `PUT /api/v1/mi-perfil` | `perfil.editar` | Datos del cliente enlazado |

El catálogo devuelve un DTO **distinto** al de administración: sin stock exacto (solo si el administrador activó `MostrarStock`), con un **tope por pedido** (`MaximoPorPedido`, 10 o lo que quede) y sin campos internos.

## 5. Diseño

### 5.1 Dirección visual (investigada el 26/10/2026)

Antes de diseñar se revisaron referencias de tiendas en línea y guías de UX (Baymard Institute sobre filtros y listados, guías de portadas de comercio electrónico, tableros de Pinterest/Dribbble): [Baymard — filtros](https://baymard.com/learn/ecommerce-filter-ui), [Baymard — listados](https://baymard.com/blog/product-listing-page-plp-ux), [LogRocket — portadas](https://blog.logrocket.com/ux-design/homepage-design-for-ecommerce-ux/) y [Tailwind UI — comercio](https://tailwindcss.com/blog/designing-tailwind-ui-ecommerce). De ahí salen las decisiones:

| Idea | Cómo se aplicó |
|---|---|
| **Tarjetas suaves en tonos pastel** con un solo color de acción de alto contraste | Cada categoría tiene un tono (`TiendaVisual.Tono`) y un ícono; los botones usan el **acento** elegido |
| **Filtros laterales persistentes** en escritorio y panel desplegable en el móvil | Barra lateral con categorías, filtros de la subcategoría, precio y "solo disponibles"; botón "Filtros" en pantallas pequeñas |
| **Filtros específicos por categoría**, no solo precio y marca | Salen de los filtros y opciones del catálogo ([CatalogoProductos.md](CatalogoProductos.md)): Talla, Color, Memoria RAM… |
| **Mostrar los filtros aplicados** y poder quitarlos uno a uno | *Chips* encima de los resultados |
| **Conservar los filtros al volver atrás** | Los filtros viven en la dirección de la página |
| **Portada selectiva**: una promoción clara, categorías y solo productos disponibles | Héroe con collage, tarjetas de categoría y "recién llegados" |
| **Estados claros**: agotado, pocas unidades, sin resultados, esqueletos de carga | Insignias, página de "no encontramos productos" y esqueletos |

### 5.2 Apariencia (igual que la administración)

La tienda usa el **mismo tema central** (`tema.css`) y el mismo botón **Apariencia**: modo claro, oscuro o del sistema y los 7 colores de acento cambian la tienda completa (el fondo pastel de las categorías se atenúa solo en modo oscuro). Solo la disposición vive en `tienda.css`; los colores salen del tema.

* **Fotos desde Garage**: cada producto y categoría muestra su foto (`/imagenes/productos/CODIGO`, servida con caché) sobre un respaldo: el **ícono Lucide** de su categoría en su tono pastel (`VisualProducto`), que se ve mientras carga o si no hay foto ([CatalogoProductos.md §9](CatalogoProductos.md)).
* **Sin emojis**: todos los íconos son SVG de la librería Lucide (`Icono`), iguales en cualquier sistema y navegador.
* **Accesible**: contraste del tema, foco visible, `aria-label` en controles, `prefers-reduced-motion` respetado y diseño adaptable (móvil, tableta, escritorio).

### 5.3 Estructura

* Páginas Razor en `Ui/Tienda/` (`TiendaLayout`, `InicioPagina`, `CatalogoPagina`, `ProductoPagina`, `CarritoPagina`, `RegistroPagina`, `PedidoPagina`, `MisPedidosPagina`), cada una con su `ViewModel` y servicios igual que los módulos de administración ([PautasCodigoEventos.md](../Equipo/PautasCodigoEventos.md)); sin código exclusivo del hospedaje web.
* El **carrito** (`CarritoService`) notifica con un evento `Cambiado` al encabezado y se guarda en el almacenamiento local del navegador (solo productos y cantidades). El precio del carrito es informativo: el pedido usa siempre el precio vigente del servidor.
* En el **hospedaje web** (render de servidor) la `Ui` llama a la API desde el servidor y guarda el JWT con `ProtectedSessionStorage` ([Arquitectura.md §1.1](../Arquitectura.md)); no hay token legible en el navegador ni hace falta CORS.
* Los **eventos de C#** aplican en la tienda (carrito → encabezado, sesión → encabezado) y se complementan con `EventCallback` entre componentes.

## 6. Seguridad Específica

* *Rate limiting* por IP en `login`, `registro` y creación de pedidos.
* El login usa mensajes genéricos. El registro **sí** informa si el correo ya existe (decisión de usabilidad), por eso lleva *rate limiting*.
* El correo **no se verifica** (no hay envío de correos). Para que nadie reclame el historial de otra persona con solo escribir su correo, el **enlace con un cliente existente exige confirmar el teléfono registrado**, queda en el historial (`EnlazarCuenta`) y el administrador puede revisarlo.
* El catálogo no expone datos internos; los `mis-pedidos` filtran por el cliente del token, nunca por un id recibido.
* HTTPS obligatorio, cabeceras de seguridad y protección *antiforgery* de Blazor activa.
* Sin datos personales en logs. Detalle general en [Seguridad.md](../Seguridad.md).

## 7. Plan de Pruebas

* **API**: catálogo anónimo (sin stock exacto, solo activos), `mis-pedidos` sin sesión (401), con rol de personal (403), y **acceso al pedido de otro cliente denegado** (404/403).
* **Integración**: registro transaccional y enlace de cliente existente; pedido web descuenta stock; cancelación repone.
* **Extremo a extremo** (Playwright): registro, pedido desde el navegador y cancelación; más el recorrido manual completo para la defensa.

## 8. Despliegue

El hospedaje `web` (puerto **4392**) corre en el compose junto a `api` y `sqlserver` ([Infraestructura.md](../Infraestructura.md)); por ahora solo en local, con la publicación en Dokploy pospuesta ([Infraestructura.md §4](../Infraestructura.md)).
