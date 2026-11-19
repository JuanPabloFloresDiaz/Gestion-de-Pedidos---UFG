# Reportes

## 1. Objetivo
Pedido del ingeniero (corrección del 26/11/2026): cada apartado administrativo ofrece un **reporte** que se descarga en **PDF, Excel (.xlsx) o CSV**. Este es el **modelo a seguir** por quien agregue un reporte nuevo (por ejemplo, pedidos por cliente): el apartado solo añade una consulta, una fila en la API y un botón.

## 2. Reportes disponibles
| Reporte | Pantalla donde está el botón | Qué muestra | Permiso |
|---|---|---|---|
| `clientes-por-zona` | Catálogos (departamentos y distritos) | Clientes por **departamento** (`agruparPor=departamento`, por defecto) o por **distrito** (`agruparPor=distrito`): total, activos e inactivos. En esta base los «municipios» son los distritos | `clientes.ver` |
| `productos-por-categoria` | Categorías y filtros | Productos por categoría y subcategoría: cantidad, activos, existencias y valor del inventario a precio de venta. Los productos sin clasificar salen aparte | `categorias.ver` |
| `usuarios-por-rol` | Usuarios | Usuarios agrupados por rol: usuario, nombre, correo y estado. **Nunca** la clave ni su hash | `usuarios.ver` |
| `permisos-por-rol` | Roles y permisos | Cada permiso (código y descripción) que tiene cada rol | `roles.ver` |
| `actividades-por-usuario` | Historial de actividades | Acciones del historial agrupadas por usuario (más reciente primero), con filtros `usuarioId`, `desde` y `hasta` (el botón envía el rango de fechas que se está viendo). **Máximo 5 000 filas**: si hay más, el reporte lo avisa al pie | `historial.ver` |
| `pedidos-por-estado` | Pedidos | Cantidad de pedidos agrupados por estado (Entregado, Cancelado, Pendiente), con detalle de activos e inactivos y monto total | `pedidos.ver` |

**Sin permisos nuevos**: cada reporte exige el mismo permiso que la pantalla de donde sale (quien puede ver los datos puede exportarlos), así que no hay migración ni cambio en los roles.

## 3. API
`GET /api/v1/reportes/{tipo}?formato=pdf|xlsx|csv[&filtros]` → el archivo, con `Content-Disposition` (`reporte-{tipo}-{AAAAMMDD-HHmm}.{ext}`, hora de El Salvador).

| Caso | Respuesta |
|---|---|
| Sin sesión / sin el permiso del reporte | `401` / `403` |
| `formato` faltante o distinto de `pdf`, `xlsx`, `csv`; `agruparPor` inválido; `desde` posterior a `hasta` | `400 VALIDACION` |
| Reporte que no existe | `404` |

## 4. Diseño
```
ReportesEndpoints ──▶ GenerarReporte (valida, elige la consulta) ──▶ IReportesRepository ──▶ TablaReporte
                                          │                                  (EF Core, solo lectura, agrupa en SQL)
                                          └──────────────▶ IExportadorDeReportes ──▶ PDF (QuestPDF) | Excel (ClosedXML) | CSV
```
* `TablaReporte` es **independiente del formato** (título, descripción, columnas, filas, fecha y nota): las consultas no saben nada de PDF ni de Excel, y los exportadores no saben de dónde salen los datos. Un reporte nuevo = **una consulta nueva**; los tres formatos salen gratis.
* **Seguridad**: en **CSV**, una celda de texto que empieza por `=`, `+`, `-` o `@` se escribe con un apóstrofo delante; en **Excel**, el texto se guarda siempre como texto (nunca como fórmula). Así un dato malicioso guardado en la base («`=cmd|...`») no se ejecuta al abrir el archivo.
* **CSV**: UTF-8 con BOM (Excel muestra bien las tildes), separado por comas, `\r\n`, números con punto decimal y sin separador de miles.
* **Excel**: encabezado en azul, números como números (con formato), columnas autoajustadas y encabezado fijo.
* **PDF**: A4 (apaisado si hay más de 4 columnas), encabezado, filas alternadas y «página X de Y». QuestPDF con licencia *Community* (gratuita para uso educativo).
* La **interfaz** solo descarga: `ReportesService` pide el archivo a la API y `descarga.js` lo entrega al navegador o a la ventana de escritorio (WebView2). El botón `BotonExportar` (kit de componentes) muestra el indicador de carga y el aviso de éxito o de error.

## 5. Cómo agregar un reporte nuevo (para los demás integrantes)
1. En `Application/Reportes/Reportes.cs`: añadir la constante en `TiposDeReporte`, el método en `IReportesRepository` y el `case` en `GenerarReporte`.
2. En `Infrastructure/Reportes/ReportesRepository.cs`: la consulta (`AsNoTracking`, agrupar en SQL) que devuelve una `TablaReporte`.
3. En `Api/Modulos/Reportes/ReportesEndpoints.cs`: una línea `Mapear(g, TiposDeReporte.MiReporte, CatalogoPermisos.MiPermisoVer)`.
4. En la pantalla: `<BotonExportar Tipo="mi-reporte" Etiqueta="…" Filtros="…" />`.
5. Pruebas: una de la consulta (`Infrastructure.Tests/ReportesTests.cs`), añadir el tipo a la lista de `ReportesApiTests` y actualizar esta tabla.

## 6. Plan de pruebas
* **Aplicación**: formato/fechas/`agruparPor` inválidos (sin consultar), tipo inexistente, consulta que corresponde a cada tipo, día final incluido completo, máximo de filas y nombre del archivo con la hora de El Salvador.
* **Exportadores** (sin base de datos): CSV (BOM, comillas, números, neutralización de fórmulas), Excel (números como número, texto que empieza con `=` sigue siendo texto, nombre de hoja válido) y PDF (válido con 0 y con 400 filas).
* **Infraestructura** (SQL Server real, datos de ejemplo): los totales de cada reporte cuadran con las tablas; actividades por usuario, por fechas y recortadas al máximo.
* **API**: los 5 reportes × 3 formatos con su tipo de contenido y sus primeros bytes (`%PDF`, `PK`, BOM); `401` sin sesión y `403` con un cliente de la tienda; el reporte de usuarios nunca trae hashes.
* **Interfaz**: ruta con escape de valores, entrega del archivo con su nombre, token enviado y errores de la API en español (sin entregar ningún archivo).
