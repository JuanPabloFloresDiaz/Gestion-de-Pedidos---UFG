# 🔎 Búsquedas

> Cubre **RF-06**: "permitir hacer búsquedas por diferentes campos" (pedidos por cliente, por producto, por precio de venta, por apellidos del cliente, etc.).

## 1. Principios

* Todos los listados son **paginados**: `pagina` (≥ 1, por defecto 1) y `tamanoPagina` (por defecto 20, máximo 100). La respuesta incluye `total`, `pagina`, `tamanoPagina` y `elementos`.
* Los filtros son **opcionales y combinables** (se aplican con `Y`). Sin filtros devuelve todo lo **activo**.
* Los filtros de texto son *contiene*, sin distinguir mayúsculas ni acentos (intercalación `Latin1_General_CI_AI` de la columna o de la consulta).
* Parámetros por *query string*; **todas** las consultas son parametrizadas ([BaseDatos.md §1](../BaseDatos.md)).
* `incluirInactivos=true` (solo con permiso de desactivar) añade los registros dados de baja.
* Orden por defecto estable; `orden` (campo) y `direccion` (`asc`/`desc`) con una lista **blanca** de campos permitidos.

## 2. Filtros por Entidad

### Clientes (`GET /api/v1/clientes`)

| Parámetro | Busca en |
|---|---|
| `q` | Nombres, apellidos o teléfono (búsqueda general) |
| `nombres`, `apellidos` | Campo específico |
| `telefono`, `correo` | Teléfono / correo |
| `departamentoId`, `ciudadId` | Ubicación |

### Productos (`GET /api/v1/productos`)

| Parámetro | Busca en |
|---|---|
| `q` | Nombre o descripción |
| `precioMin`, `precioMax` | Rango de **precio de venta** |
| `stockMin`, `stockMax` | Rango de existencias |
| `ingresoDesde`, `ingresoHasta` | Rango de fecha de ingreso |
| `categoriaId`, `subcategoriaId` | Clasificación del producto ([CatalogoProductos.md](CatalogoProductos.md)); `sinClasificar=true` lista los que no tienen |
| `opcionIds` | Opciones de filtro (ej. Color = Negro). **Y** entre filtros distintos, **O** entre opciones del mismo filtro |

### Pedidos (`GET /api/v1/pedidos`, procedimiento `usp_Pedidos_Buscar`)

| Parámetro | Busca en |
|---|---|
| `clienteId` | Dueño del pedido |
| `cliente` | Nombres o **apellidos** del cliente (texto) |
| `productoId` / `producto` | Pedidos que contienen ese producto (por id o nombre) |
| `precioMin`, `precioMax` | **Precio unitario** de alguna línea (precio de venta aplicado) |
| `estado` | `Pendiente`, `Entregado`, `Cancelado` |
| `pedidoDesde`, `pedidoHasta` | Rango de fecha de pedido |
| `entregaDesde`, `entregaHasta` | Rango de fecha de entrega |
| `usuarioId` | Quién hizo el pedido |

### Usuarios e Historial
Ver [AutenticacionYPermisos.md](AutenticacionYPermisos.md) y [HistorialActividades.md](HistorialActividades.md).

## 3. Interfaz

* Cada página de módulo tiene un panel de filtros colapsable, botones **Buscar** y **Limpiar**, y un paginador reutilizable (`Comun/Componentes`).
* La búsqueda se dispara con: botón (`@onclick`), *Enter* en un cuadro (`@onkeydown`) y búsqueda general "al escribir" con **retraso (*debounce*) de 400 ms** sobre `@oninput`.
* Una búsqueda en curso se **cancela** si se lanza otra (`CancellationToken`), para no mostrar resultados antiguos.
* Sin resultados: mensaje claro, no una tabla vacía muda.

## 4. Pruebas

* **Integración**: cada filtro individual y combinado devuelve lo esperado; paginación (primera, última, fuera de rango); límites de rango (`>=`/`<=`); acentos y mayúsculas.
* **Seguridad**: intento de inyección en cada filtro de texto (`' OR 1=1 --`) devuelve 0 resultados y no falla; `orden` fuera de la lista blanca da `400`.
* **API**: `tamanoPagina` > 100 se limita o responde `400`.
