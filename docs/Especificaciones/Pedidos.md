# 🧾 Módulo Pedidos

> Cubre **RF-05** (y pedidos de RF-06). Tareas: bloque D de [Tareas.md](../Equipo/Tareas.md). Tablas: [BaseDatos.md §4.5](../BaseDatos.md). Es el módulo con más reglas: léase completo antes de programar.

## 1. Objetivo

Registrar los pedidos de los clientes (con una o varias líneas de producto), seguir su estado, actualizarlos y darlos de baja de forma lógica, manteniendo el **stock consistente** y notificando cada cambio.

## 2. Modelo

**Pedido (cabecera)**

| Campo del enunciado | Campo | Regla |
|---|---|---|
| Fecha de pedido | `FechaPedido` | La asigna el sistema (UTC) al crear; no editable |
| Fecha de entrega | `FechaEntrega` | Obligatoria; **no anterior** a la fecha del pedido |
| Comentarios | `Comentarios` | Opcional, ≤ 500 |
| Dueño del pedido | `ClienteId` | Cliente **activo** al crear |
| Quién hizo el pedido | `UsuarioId` | El usuario autenticado; lo asigna el sistema, no la pantalla. En la web es el propio cliente ([PortalWeb.md](PortalWeb.md)) |
| — | `Estado` | `Pendiente` → `Entregado` \| `Cancelado` |
| — | `Total` | Suma de subtotales; lo calcula el dominio |
| — | `Activo` | Baja lógica |

**Línea (`DetallePedido`)**: `Producto`, `Cantidad` (> 0), `PrecioUnitario` (**copiado** del producto al crear la línea; es el "precio por unidad" del enunciado) y `Subtotal = Cantidad × PrecioUnitario`. Un producto aparece **una sola vez** por pedido.

## 3. Reglas de Negocio

1. Un pedido debe tener **al menos una línea**; productos **activos**; cantidades > 0.
2. **Registrar**: valida cliente y productos, copia precios, **descuenta el stock** de cada producto y guarda todo en **una sola transacción**. Si algún producto no alcanza, **no se guarda nada** y se informa cuál (`StockInsuficiente`).
3. **Editar** (solo `Pendiente`): cambiar `FechaEntrega`, `Comentarios` y las líneas. Al cambiar cantidades o quitar/añadir productos se **ajusta el stock por diferencia** (reponer lo que sobra, descontar lo que falta, con la misma validación de existencias). Los precios de líneas ya existentes **no cambian**; una línea nueva toma el precio vigente.
4. **Transiciones de estado**:

| De | A | Efecto |
|---|---|---|
| `Pendiente` | `Entregado` | Estado final; el stock **no** cambia |
| `Pendiente` | `Cancelado` | **Repone** el stock de todas las líneas; estado final |
| `Entregado` / `Cancelado` | cualquiera | ❌ No permitido (`422`) |

5. **Baja lógica** (`pedidos.desactivar`): si el pedido está `Pendiente`, primero se **cancela** (reponiendo stock) y luego pasa a `Activo = 0`; si ya está en estado final, solo se marca inactivo. Un pedido inactivo **no se reactiva** ni se edita, y no aparece en los listados por defecto.
6. Un cliente con pedidos `Pendiente` no puede darse de baja ([Clientes.md §3](Clientes.md)).
7. **Concurrencia**: `RowVersion` en pedido y producto; ante conflicto (`DbUpdateConcurrencyException`) la API responde `409` y el cliente pide recargar. El `CHECK (CantidadStock >= 0)` es la última barrera.

## 4. Casos de Uso

| Caso de uso | Permiso | Resultado / errores |
|---|---|---|
| `RegistrarPedido` | `pedidos.crear` | `201`; `422` stock insuficiente / cliente o producto inactivo / sin líneas |
| `ActualizarPedido` | `pedidos.editar` | `200`; `409`; `422` si no está `Pendiente` |
| `CambiarEstadoPedido` | `pedidos.cambiar_estado` | `200`; `422` transición inválida |
| `DesactivarPedido` | `pedidos.desactivar` | `200`; `404` |
| `ObtenerPedido` / `BuscarPedidos` | `pedidos.ver` | Detalle con líneas / página (procedimiento `usp_Pedidos_Buscar`, [Busquedas.md](Busquedas.md)) |

## 5. API

| Método y ruta | Descripción |
|---|---|
| `GET /api/v1/pedidos` | Búsqueda paginada con filtros |
| `GET /api/v1/pedidos/{id}` | Cabecera + líneas |
| `POST /api/v1/pedidos` | Crea con líneas `[{productoId, cantidad}]` (el servidor pone precios y usuario) |
| `PUT /api/v1/pedidos/{id}` | Actualiza fecha de entrega, comentarios y líneas |
| `PATCH /api/v1/pedidos/{id}/estado` | `{ "estado": "Entregado" \| "Cancelado" }` |
| `PATCH /api/v1/pedidos/{id}/desactivar` | Baja lógica |

El cliente **nunca** envía precios ni el usuario creador: lo decide el servidor.

## 6. Eventos

| Origen | Evento | Reacción |
|---|---|---|
| `PedidosService` (cliente) | `EntidadCambiada(Pedido, Registrada \| Actualizada \| EstadoCambiado)` | `Notificador` ("Pedido #N registrado / entregado / cancelado / dado de baja"); listados abiertos se refrescan |
| `PedidosService` (cliente) | `StockInsuficiente` | `Notificador` + `PedidosViewModel` resalta la línea problemática |
| Casos de uso (servidor) | `EntidadCambiada` | `HistorialSubscriber` (incluye el detalle: número de pedido, cliente, total) |

## 7. Interfaz (`PedidosPagina`, en una pestaña)

* **Maestro–detalle**: cabecera (cliente con selector/búsqueda, fecha de entrega con selector de fecha, comentarios) y líneas en una tabla editable (producto, cantidad, precio unitario, subtotal) con **total calculado en vivo**.
* Al elegir un producto se muestra su stock disponible y se impide una cantidad mayor.
* Botones: Registrar, Guardar cambios, Marcar entregado, Cancelar pedido, Dar de baja (según permisos y estado).
* Panel de búsqueda con los filtros de [Busquedas.md](Busquedas.md).
* Eventos: `@onblur`/`@onchange` recalculan el total; `@onchange` del producto muestra su stock; `@onbeforeunload` avisa si hay cambios sin guardar.

## 8. Plan de Pruebas

* **Dominio** (exhaustivo): total, líneas duplicadas, todas las transiciones válidas e inválidas, reposición de stock, fecha de entrega.
* **Integración**: transacción atómica (si falla la 2.ª línea no queda nada), dos pedidos por la última unidad, doble cambio de estado simultáneo, `usp_Pedidos_Buscar`.
* **API**: 401/403/éxito, `409`, `422` con mensaje de producto.
* **ViewModel**: total en vivo, publicación de `EntidadCambiada` y `StockInsuficiente`.
