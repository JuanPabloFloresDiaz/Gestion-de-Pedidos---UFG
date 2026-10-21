# 📜 Historial de Actividades

> Cubre **RF-10** (opcional en el enunciado: "registro de todas las acciones realizadas en la aplicación"). Tabla: [BaseDatos.md §4.6](../BaseDatos.md).

## 1. Qué se Registra

| Acción | Entidad | Detalle de ejemplo |
|---|---|---|
| `Login` / `LoginFallido` | Usuario | Nombre de usuario intentado (sin clave) |
| `Registrar` | Cliente, Producto, Pedido, Usuario | "Pedido #12, cliente Ana Pérez, total 45.50" |
| `Actualizar` | ídem | Campos modificados (nombre del campo, no valores sensibles) |
| `CambiarEstado` | Cliente, Producto, Pedido, Usuario | "Pedido #12: Pendiente → Cancelado (stock repuesto)" |
| `CambiarRol`, `CambiarClave` | Usuario | Sin la clave |
| `Registro`, `EnlazarCuenta` | Usuario, Cliente | Registro web de un cliente / enlace de su cuenta con un cliente existente |

Nunca se registran contraseñas, tokens ni cadenas de conexión.

## 2. Cómo se Genera (con eventos)

El historial es un **suscriptor** de los eventos de aplicación ([PatronEventos.md §3.3](PatronEventos.md)): los casos de uso no saben que existe un historial.

```
Caso de uso ──(tras confirmar la transacción)──► EntidadCambiada ──► HistorialSubscriber ──► INSERT Actividades
Autenticar  ─────────────────────────────────► AccesoRegistrado ──┘
```

* Se registra **después** de confirmar la operación, con su propia transacción; si falla el registro se **loguea el error** pero no se revierte la operación del usuario.
* Cada suscriptor captura sus excepciones para no cortar a los demás.

## 3. Consulta (`HistorialPagina`, solo administrador)

* `GET /api/v1/actividades` (permiso `historial.ver`), paginado, con filtros: `usuarioId`, `entidad`, `accion`, `desde`, `hasta`.
* Listado de solo lectura, más reciente primero. No hay edición ni borrado: el usuario de BD no tiene `UPDATE/DELETE` sobre la tabla.

## 4. Pruebas

* Cada caso de uso que publica `EntidadCambiada` deja **una** fila coherente.
* Un fallo del suscriptor no afecta la operación principal.
* El usuario de BD de la aplicación **no puede** actualizar ni borrar filas de `Actividades` (prueba de integración).
* Filtros y paginación del endpoint; el operador recibe `403`.
