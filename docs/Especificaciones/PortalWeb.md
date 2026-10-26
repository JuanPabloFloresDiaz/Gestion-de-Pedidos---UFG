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

| Ruta | Acceso | Contenido |
|---|---|---|
| `/` | Público | Presentación y productos destacados |
| `/catalogo` | Público | Listado paginado con búsqueda por nombre y rango de precio |
| `/catalogo/{id}` | Público | Detalle del producto y botón "Agregar al carrito" |
| `/registro` · `/ingresar` | Público | Alta de cliente (nombres, apellidos, teléfono, dirección, departamento, ciudad, correo, contraseña) · inicio de sesión |
| `/carrito` | Cliente | Líneas, cantidades, total; fecha de entrega deseada y comentarios; botón "Hacer pedido" |
| `/mis-pedidos` · `/mis-pedidos/{id}` | Cliente | Lista y detalle con estado; "Cancelar pedido" si está `Pendiente` |
| `/perfil` | Cliente | Datos personales y cambio de contraseña |

## 3. Reglas

* Un pedido hecho en la web es un `Pedido` normal: `ClienteId` = el cliente enlazado al usuario; `UsuarioId` = ese mismo usuario ([Pedidos.md](Pedidos.md)). Mismas reglas de stock, precio copiado y estados.
* El cliente solo ve y cancela **sus** pedidos; no puede editar líneas después de crearlo (cancela y vuelve a pedir).
* El catálogo público muestra solo productos **activos** y, según el interruptor `MostrarStock` de cada producto ([Productos.md](Productos.md)), la **cantidad exacta** o solo "Disponible" / "Agotado" (por defecto, lo segundo).
* La fecha de entrega deseada debe ser posterior a hoy.
* **Registro** ([AutenticacionYPermisos.md §2](AutenticacionYPermisos.md)): crea el usuario (rol `Cliente`) y su `Clientes` en una transacción. Con el correo escrito:
  * **Ya existe una cuenta** con ese correo → "Ya existe una cuenta con este correo: inicia sesión o usa otro correo".
  * **Existe un cliente sin cuenta** con ese correo (lo registró un operador) → la web pregunta "Encontramos un cliente registrado con este correo. ¿Eres tú? Confirma tu teléfono para enlazar tu cuenta, o usa otro correo". Si el teléfono coincide con el registrado, se **enlaza** (no se duplica); si no, se rechaza. Con límite de intentos.
  * **No existe** → se crea un cliente nuevo.
* Los mismos datos geográficos (departamento → ciudad) con las listas dependientes, cargados desde la API.

## 4. API Pública y de Cliente

| Método y ruta | Acceso | Descripción |
|---|---|---|
| `GET /api/v1/catalogo` · `GET /api/v1/catalogo/{id}` | Anónimo | Productos activos, paginados, con filtros (`q`, `precioMin`, `precioMax`) |
| `GET /api/v1/departamentos` · `/{id}/ciudades` | Anónimo | Catálogos para el registro |
| `POST /api/v1/auth/registro` · `POST /api/v1/auth/login` | Anónimo | Con límite de peticiones |
| `GET /api/v1/mis-pedidos` · `GET /api/v1/mis-pedidos/{id}` | `mispedidos.ver` | Solo los del cliente del token |
| `POST /api/v1/mis-pedidos` | `mispedidos.crear` | Crea con `[{productoId, cantidad}]`, fecha de entrega y comentarios |
| `PATCH /api/v1/mis-pedidos/{id}/cancelar` | `mispedidos.cancelar` | Solo si es suyo y está `Pendiente` |
| `GET /api/v1/mi-perfil` · `PUT /api/v1/mi-perfil` | `perfil.editar` | Datos del cliente enlazado |

El catálogo devuelve un DTO **distinto** al de administración (sin stock exacto ni campos internos).

## 5. Diseño

* Páginas Razor en `Ui/Tienda/`, con `ViewModel` y servicios igual que los módulos de administración ([PautasCodigoEventos.md](../Equipo/PautasCodigoEventos.md)); sin código exclusivo del hospedaje web.
* En el **hospedaje web** (render de servidor) la `Ui` llama a la API desde el servidor y guarda el JWT con `ProtectedSessionStorage` ([Arquitectura.md §1.1](../Arquitectura.md)); no hay token legible en el navegador ni hace falta CORS.
* Los **eventos de C#** también aplican en la tienda (por ejemplo, el carrito notifica a la barra superior) y se complementan con `EventCallback` entre componentes.
* Estilo con el mismo tema central que la administración ([InterfazUsuario.md §8](InterfazUsuario.md)); el diseño de la tienda lo decide Pablo.

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
