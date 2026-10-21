# 👤 Módulo Clientes

> Cubre **RF-03** (y la parte de clientes de RF-06). Tareas: bloque C de [Tareas.md](../Equipo/Tareas.md). Tabla: [BaseDatos.md §4.3](../BaseDatos.md).

## 1. Objetivo

Registrar y mantener los clientes dueños de los pedidos, con **notificación** de cada ingreso, actualización y cambio de estado, y baja **lógica** (activo → inactivo).

## 2. Modelo

| Campo | Regla |
|---|---|
| Nombres | Obligatorio, 1–100, sin espacios sobrantes |
| Apellidos | Obligatorio, 1–100 |
| Teléfono | Obligatorio; solo dígitos, espacios, `+` y `-`; 8–20 caracteres (formato salvadoreño de 8 dígitos aceptado con o sin código `+503`) |
| Dirección | Obligatoria, 1–250 |
| Correo | **Opcional**; formato de correo válido, ≤ 150, único entre clientes. Lo usa la web para enlazar la cuenta |
| Departamento | Obligatorio (se elige primero); determina las ciudades disponibles |
| Ciudad | Obligatoria, debe pertenecer al departamento elegido |
| Activo | `true` al crear; solo cambia por baja/reactivación |
| Cuenta web (`UsuarioId`) | Opcional y único. Lo asigna el sistema cuando el cliente se registra en la web ([PortalWeb.md](PortalWeb.md)); la administración solo lo **muestra** ("Tiene cuenta web") y nunca lo edita |

Invariantes: no se guarda un cliente sin ciudad válida; el departamento no se almacena (se deduce de la ciudad). El teléfono **no** es único (una familia puede compartirlo).

## 3. Casos de Uso

| Caso de uso | Entrada | Resultado | Errores |
|---|---|---|---|
| `RegistrarCliente` | Datos del cliente | Cliente creado, `Activo = true` | `400` validación; `422` ciudad inexistente |
| `ActualizarCliente` | Id + datos (con `RowVersion`) | Cliente actualizado | `404`; `409` conflicto de concurrencia; `422` cliente inactivo |
| `CambiarEstadoCliente` | Id + `activo` | Baja o reactivación | `404`; **`422` si tiene pedidos `Pendiente`** al darlo de baja |
| `ObtenerCliente` / `BuscarClientes` | Id / filtros ([Busquedas.md](Busquedas.md)) | Detalle / página de resultados | `404` |

* Un cliente **inactivo** no puede ser dueño de un pedido nuevo, pero sus pedidos históricos siguen visibles.
* Editar un cliente inactivo no se permite: primero se reactiva.

## 4. API

| Método y ruta | Permiso | Descripción |
|---|---|---|
| `GET /api/v1/clientes` | `clientes.ver` | Lista paginada con filtros (§ Búsquedas) |
| `GET /api/v1/clientes/{id}` | `clientes.ver` | Detalle |
| `POST /api/v1/clientes` | `clientes.crear` | Crea; `201` con `Location` |
| `PUT /api/v1/clientes/{id}` | `clientes.editar` | Actualiza |
| `PATCH /api/v1/clientes/{id}/estado` | `clientes.desactivar` | Cuerpo `{ "activo": false }` |
| `GET /api/v1/departamentos` · `GET /api/v1/departamentos/{id}/ciudades` | `clientes.ver` | Catálogos para las listas dependientes |

## 5. Eventos

| Origen | Evento | Reacción |
|---|---|---|
| `ClientesService` (cliente) | `EntidadCambiada(Cliente, Registrada \| Actualizada \| EstadoCambiado)` | `Notificador`: "Cliente *Nombre Apellido* registrado/actualizado/desactivado"; listados de Clientes y Pedidos abiertos se refrescan |
| Caso de uso (servidor) | `EntidadCambiada` | `HistorialSubscriber` registra la acción |

## 6. Interfaz (`ClientesPagina`, en una pestaña)

* Listado paginado (`TablaDatos`) con búsqueda; panel de edición con los campos de §2; botones Nuevo, Guardar, Desactivar/Reactivar.
* El selector de departamento (`@onchange`) carga el selector de ciudad (lista dependiente).
* Validación visible junto a cada campo y botón Guardar deshabilitado mientras la operación está en curso.
* Los botones respetan los permisos del usuario. Eventos de vista en [PatronEventos.md §3.2](PatronEventos.md).

## 7. Plan de Pruebas

* **Dominio**: validaciones de campos y de teléfono.
* **Integración**: restricciones de BD, ciudad–departamento, baja con pedidos pendientes bloqueada, concurrencia.
* **API**: 401/403/éxito por endpoint; `400` con detalle de campos.
* **ViewModel**: guardar válido/ inválido, cambio de departamento recarga ciudades, publicación de `EntidadCambiada`.
