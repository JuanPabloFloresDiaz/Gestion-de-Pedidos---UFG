# 📦 Módulo Productos

> Cubre **RF-04** (y la parte de productos de RF-06). Tareas: bloque P de [Tareas.md](../Equipo/Tareas.md). Tabla: [BaseDatos.md §4.4](../BaseDatos.md).

## 1. Objetivo

Mantener el catálogo de productos con su stock y precio de venta, notificando ingresos, actualizaciones y cambios de estado, con baja lógica.

## 2. Modelo

| Campo | Regla |
|---|---|
| Nombre (producto) | Obligatorio, 1–150 |
| Descripción | Obligatoria, 1–500 |
| Cantidad en stock | Entero ≥ 0 |
| Precio de venta | Decimal ≥ 0, 2 decimales |
| Fecha | **Fecha de ingreso** al inventario ([Analisis.md §3](../Analisis.md), I-02); por defecto hoy; no puede ser futura |
| Mostrar existencias en la tienda (`MostrarStock`) | Interruptor que decide el administrador por producto. Apagado (por defecto): la web muestra "Disponible" o "Agotado"; encendido: muestra la cantidad exacta |
| Activo | `true` al crear |

Comportamiento del dominio (usado por Pedidos):
* `DescontarStock(cantidad)`: rechaza `cantidad <= 0` y resultados negativos.
* `ReponerStock(cantidad)`: rechaza `cantidad <= 0`.
* El stock **solo** cambia por estos métodos o por la edición explícita del producto (ajuste de inventario).

## 3. Casos de Uso

| Caso de uso | Resultado | Errores |
|---|---|---|
| `RegistrarProducto` | Producto creado activo | `400` |
| `ActualizarProducto` (con `RowVersion`) | Producto actualizado (incluye ajuste de stock) | `404`; `409`; `422` inactivo |
| `CambiarEstadoProducto` | Baja o reactivación | `404` |
| `ObtenerProducto` / `BuscarProductos` | Detalle / página | `404` |

* Un producto **inactivo** no se puede añadir a pedidos nuevos; las líneas históricas no cambian.
* Cambiar el **precio** no afecta pedidos ya creados (copian el precio, [Pedidos.md §2](Pedidos.md)).
* **Stock bajo**: umbral fijo de 5 unidades para destacarlo en el listado (constante de configuración; no es un campo en BD, YAGNI).

## 4. API

| Método y ruta | Permiso | Descripción |
|---|---|---|
| `GET /api/v1/productos` | `productos.ver` | Lista paginada con filtros |
| `GET /api/v1/productos/{id}` | `productos.ver` | Detalle |
| `POST /api/v1/productos` | `productos.crear` | Crea |
| `PUT /api/v1/productos/{id}` | `productos.editar` | Actualiza |
| `PATCH /api/v1/productos/{id}/estado` | `productos.desactivar` | `{ "activo": false }` |

## 5. Eventos

`ProductosService` publica `EntidadCambiada(Producto, …)` tras cada respuesta exitosa; el `Notificador` muestra el mensaje y los listados de Productos y el selector de productos de Pedidos se refrescan. El servidor publica `EntidadCambiada` para el historial.

## 6. Interfaz (`ProductosPagina`, en una pestaña)

Listado con indicador visual de **stock bajo**, panel de edición con validación en línea, selector de fecha, interruptor "Mostrar existencias en la tienda" y campo de precio con formato monetario. Botones sujetos a permisos ([AutenticacionYPermisos.md §3](AutenticacionYPermisos.md)): el operador **consulta** pero no crea ni edita.

## 7. Plan de Pruebas

* **Dominio**: `DescontarStock`/`ReponerStock` (límites 0, negativos, mayor al stock), validaciones.
* **Integración**: `CHECK` de stock y precio, concurrencia sobre stock.
* **API**: 401/403/éxito, `409` por `RowVersion` desactualizado.
* **ViewModel**: validación, publicación de `EntidadCambiada`.
