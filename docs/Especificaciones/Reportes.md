# Reportes

## 1. Objetivo
Pedido del ingeniero (corrección del 26/11/2026): cada apartado administrativo ofrece un **reporte** que se descarga en **PDF, Excel (.xlsx) o CSV**. Este es el **modelo a seguir** por quien agregue un reporte nuevo (por ejemplo, pedidos por cliente): el apartado solo añade una consulta, una fila en la API y un botón.

## 2. Reportes disponibles
| Reporte | Pantalla donde está el botón | Qué muestra | Permiso |
|---|---|---|---|
| `clientes-por-zona` | Catálogos (departamentos y distritos) | Clientes por **departamento** (`agruparPor=departamento`, por defecto) o por **distrito** (`agruparPor=distrito`): total, activos e inactivos, con tarjetas de totales. En esta base los «municipios» son los distritos | `clientes.ver` |
| `productos-por-categoria` | Categorías y filtros | **Todas las categorías**: tarjetas de totales, resumen por categoría y subcategoría (cantidad, activos, existencias y valor del inventario) y, debajo, **un bloque por categoría con sus productos** (código, nombre, subcategoría, precio, existencias y estado; los sin clasificar al final). **Una categoría** (`categoriaId`): su ficha (código, nombre, descripción, estado), sus subcategorías con totales, **los filtros de cada subcategoría con sus opciones** y los productos que contiene | `categorias.ver` |
| `usuarios-por-rol` | Usuarios | Usuarios agrupados por rol: usuario, nombre, correo y estado, con tarjetas de totales. **Nunca** la clave ni su hash | `usuarios.ver` |
| `permisos-por-rol` | Roles y permisos | **Un bloque por rol**: arriba el rol (con su descripción, cuántos permisos y cuántos usuarios tiene) y debajo la lista de sus permisos (módulo, código y descripción) | `roles.ver` |
| `actividades-por-usuario` | Historial de actividades | **De un usuario** (`usuarioId`): su ficha (usuario, nombre, correo, rol, estado y período), cifras (actividades, tipos de acción, primera y última), un resumen por acción y el **detalle de cada actividad**. **Sin usuario**: un resumen por usuario y un bloque con las actividades de cada uno. Filtros `desde` y `hasta` (el botón envía el rango que se está viendo). **Máximo 5 000 filas**: si hay más, el reporte lo avisa al pie | `historial.ver` |
| `pedidos-por-estado` | Pedidos | Cantidad de pedidos agrupados por estado (Entregado, Cancelado, Pendiente), con detalle de activos e inactivos, monto total y tarjetas de totales | `pedidos.ver` |

**Elegir a quién o a qué acotar el reporte.** Los reportes de actividades y de productos muestran, junto a los botones, una lista para elegir el usuario o la categoría («Todos los usuarios» / «Todas las categorías» por defecto). La lista la da la API en `GET /api/v1/reportes/{tipo}/opciones` con el **mismo permiso que el reporte**; los demás reportes no tienen opciones (404).

**Color y logo.** El PDF y el Excel llevan el **logo de NoviXtore** y el **color de acento** que la persona tiene elegido en «Apariencia» (azul, turquesa, verde, ámbar, rosa, violeta o grafito; son los mismos valores de `tema.css`, variante clara). El botón de exportar envía el acento en el filtro `acento`; si falta, se usa el azul. El CSV es solo datos (sin color ni logo).

**Sin permisos nuevos**: cada reporte exige el mismo permiso que la pantalla de donde sale (quien puede ver los datos puede exportarlos), así que no hay migración ni cambio en los roles.

## 3. API
`GET /api/v1/reportes/{tipo}?formato=pdf|xlsx|csv[&filtros]` → el archivo, con `Content-Disposition` (`reporte-{tipo}-{AAAAMMDD-HHmm}.{ext}`, hora de El Salvador).

| Filtro | Qué hace |
|---|---|
| `agruparPor` | `departamento` o `distrito` (clientes) |
| `usuarioId` | solo las actividades de ese usuario |
| `categoriaId` | solo esa categoría (productos) |
| `desde`, `hasta` | rango de fechas (actividades y pedidos); el día final se incluye completo |
| `acento` | color del PDF y el Excel |

| Caso | Respuesta |
|---|---|
| Sin sesión / sin el permiso del reporte | `401` / `403` |
| `formato` faltante o distinto de `pdf`, `xlsx`, `csv`; `agruparPor` inválido; `desde` posterior a `hasta`; `acento` que no existe; `usuarioId` o `categoriaId` no positivos | `400 VALIDACION` |
| Reporte que no existe; **usuario o categoría pedidos que no existen** (no se devuelve un reporte vacío) | `404` |

## 4. Diseño
```
ReportesEndpoints ──▶ GenerarReporte (valida, elige la consulta) ──▶ IReportesRepository ──▶ TablaReporte
                                          │                                  (EF Core, solo lectura, agrupa en SQL)
                                          └──────────────▶ IExportadorDeReportes ──▶ PDF (QuestPDF) | Excel (ClosedXML) | CSV
```
* `TablaReporte` es **independiente del formato**: las consultas no saben nada de PDF ni de Excel, y los exportadores no saben de dónde salen los datos. Un reporte nuevo = **una consulta nueva**; los tres formatos salen gratis. Además de la tabla principal (título, descripción, columnas, filas, fecha y nota) puede traer tres piezas **opcionales**:
  * `Ficha`: pares etiqueta/valor sobre lo que se reporta (el usuario o la categoría elegidos).
  * `Indicadores`: cifras clave (se ven como tarjetas en el PDF).
  * `Secciones`: bloques con su propio título, subtítulo y tabla (un rol con sus permisos, una categoría con sus productos…). Si el reporte es solo de bloques, la tabla principal va vacía.
* **Seguridad**: en **CSV**, una celda de texto que empieza por `=`, `+`, `-` o `@` se escribe con un apóstrofo delante; en **Excel**, el texto se guarda siempre como texto (nunca como fórmula). Así un dato malicioso guardado en la base («`=cmd|...`») no se ejecuta al abrir el archivo.
* **CSV**: UTF-8 con BOM (Excel muestra bien las tildes), separado por comas, `\r\n`, números con punto decimal y sin separador de miles. Un reporte de **una sola tabla** sale solo con encabezado y filas (listo para importar). Un reporte **con bloques** sale como documento: título, descripción, ficha, indicadores, la tabla principal y cada bloque con su título.
* **Excel**: logo arriba, banda del título con el color de acento, ficha e indicadores, tablas con encabezado de color, filas alternadas, números como números (con formato), columnas autoajustadas, apaisado y ajustado a una hoja para imprimir; el encabezado queda fijo cuando el reporte es de una sola tabla.
* **PDF**: A4 (apaisado si alguna tabla tiene más de 4 columnas). Banda del título con el color de acento y el logo a la derecha; ficha; tarjetas de indicadores; tablas con encabezado de color y filas alternadas; cada bloque con su franja de título; pie con la fecha y «página X de Y». QuestPDF con licencia *Community* (gratuita para uso educativo).
* **Logo**: `Infrastructure/Reportes/Recursos/logo-reporte.png` va **dentro del ensamblado** (recurso incrustado), así el reporte no depende de ningún archivo en disco. Es el `logo-claro.svg` de la interfaz convertido a PNG y recortado.
* La **interfaz** solo descarga: `ReportesService` pide el archivo a la API y `descarga.js` lo entrega al navegador o a la ventana de escritorio (WebView2). El botón `BotonExportar` (kit de componentes) muestra el indicador de carga y el aviso de éxito o de error.

## 5. Cómo agregar un reporte nuevo (para los demás integrantes)
1. En `Application/Reportes/Reportes.cs`: añadir la constante en `TiposDeReporte`, el método en `IReportesRepository` y el `case` en `GenerarReporte`.
2. En `Infrastructure/Reportes/ReportesRepository.cs`: la consulta (`AsNoTracking`, agrupar en SQL) que devuelve una `TablaReporte`.
3. En `Api/Modulos/Reportes/ReportesEndpoints.cs`: una línea `Mapear(g, TiposDeReporte.MiReporte, CatalogoPermisos.MiPermisoVer)`.
4. En la pantalla: `<BotonExportar Tipo="mi-reporte" Etiqueta="…" Filtros="…" />`.
5. Si el reporte se puede acotar a un usuario o una categoría: agregar `conOpciones: true` en el `Mapear` y el caso en `GenerarReporte.OpcionesAsync`; en la pantalla, `ParametroOpcion="usuarioId"` (o `categoriaId`) en el `BotonExportar`.
6. Pruebas: una de la consulta (`Infrastructure.Tests/ReportesTests.cs`), añadir el tipo a la lista de `ReportesApiTests` y actualizar esta tabla.

## 6. Plan de pruebas
* **Aplicación**: formato/fechas/`agruparPor` inválidos (sin consultar), tipo inexistente, consulta que corresponde a cada tipo, día final incluido completo, máximo de filas y nombre del archivo con la hora de El Salvador.
* **Exportadores** (sin base de datos): CSV (BOM, comillas, números, neutralización de fórmulas), Excel (números como número, texto que empieza con `=` sigue siendo texto, nombre de hoja válido) y PDF (válido con 0 y con 400 filas).
* **Infraestructura** (SQL Server real, datos de ejemplo): los totales de cada reporte cuadran con las tablas; actividades por usuario, por fechas y recortadas al máximo.
* **API**: los 5 reportes × 3 formatos con su tipo de contenido y sus primeros bytes (`%PDF`, `PK`, BOM); `401` sin sesión y `403` con un cliente de la tienda; el reporte de usuarios nunca trae hashes.
* **Reportes con bloques**: permisos por rol (un bloque por rol, con la suma de permisos igual a la tabla), actividades de un usuario (ficha, resumen por acción y detalle) y de todos (un bloque por usuario), productos de una categoría (subcategorías, filtros con opciones y productos) y de todas (un producto por cada fila de los bloques); un usuario o una categoría inexistente es 404; las listas de opciones.
* **Color y logo**: el encabezado de Excel usa el color elegido (azul, rosa, verde…) y trae el logo; el PDF es válido en cada color; un color inventado es 400.
* **Interfaz**: filtros que arma el botón (opción elegida y color, sin tocar los de la pantalla), lista de opciones desde la API, ruta con escape de valores, entrega del archivo con su nombre, token enviado y errores de la API en español (sin entregar ningún archivo).
