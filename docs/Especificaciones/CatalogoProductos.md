# 🏷️ Módulo Catálogo de Productos (categorías, subcategorías, filtros y opciones)

> **Extensión acordada el 26/10/2026**, adaptada de un proyecto previo del autor (un motor de catálogo dinámico con categorías, subcategorías, filtros y opciones) pero **en versión básica**. No es un requerimiento del enunciado ([Analisis.md §3, I-12](../Analisis.md)): **suma valor** a RF-04 (productos) y a la tienda web sin cambiar lo que pide el ingeniero. Tareas: bloque K de [Tareas.md](../Equipo/Tareas.md). Tablas: [BaseDatos.md §4.7](../BaseDatos.md).

## 1. Objetivo

Poder **clasificar y describir** los productos con más detalle sin tocar el esquema cada vez:

* Una **categoría** agrupa subcategorías (ej.: *Calzado*).
* Una **subcategoría** define **qué se puede decir** de sus productos mediante **filtros** (ej.: *Zapatos deportivos* → Color, Talla, Marca).
* Un **filtro** tiene una lista de **opciones** (Color → Rojo, Negro, Blanco).
* Un **producto** pertenece a una subcategoría y elige, para cada filtro, **una opción** (el *detalle del producto*: Color = Negro, Talla = 42).

```
[ Categoría: Calzado ]
      └─► [ Subcategoría: Zapatos deportivos ]
                ├─► [ Filtro: Color ] ─► Rojo · Negro · Blanco
                ├─► [ Filtro: Talla ] ─► 40 · 41 · 42 · 43
                └─► [ Filtro: Marca ] ─► Nike · Adidas · Puma
```

Sirve para dos cosas: **describir mejor** el producto (administración) y **explorar/filtrar** en la tienda web ("Calzado → Zapatos deportivos → Talla 42").

## 2. Qué se adopta y qué se deja fuera (KISS)

| Concepto del proyecto original | Aquí |
|---|---|
| Categorías, subcategorías, filtros, opciones de filtro y detalle del producto | ✅ **Se adopta** |
| **SKU**, **variantes** (precio, stock y costo por variante), costo, stock mínimo | ❌ **Fuera**: el producto sigue siendo **una sola unidad vendible** con su stock y precio ([Productos.md](Productos.md)). Los filtros son **solo descriptivos** (no generan combinaciones con stock propio) |
| Tipo de dato del filtro (texto, numérico, lista) | ❌ Todos los filtros son **lista de opciones** |
| Imágenes de categoría y de producto, "destacado en tienda" | ❌ Fuera (el proyecto no maneja imágenes) |
| Identificadores `UUID`, esquema por empresa | ❌ `INT IDENTITY` y un solo esquema, como el resto de la base |

> Si más adelante se quisieran variantes con stock propio, el modelo crece sin romperse (tabla de variantes que apunte a `ProductosOpciones`), pero **no se hace ahora** (YAGNI).

## 3. Modelo y reglas

| Entidad | Campos | Reglas |
|---|---|---|
| **Categoría** | Nombre (1–100), Descripción (0–300), Activo, `EliminadoEn` | Nombre único (sin distinguir mayúsculas) |
| **Subcategoría** | Categoría, Nombre (1–100), Descripción (0–300), Activo, `EliminadoEn` | Nombre único **dentro de su categoría** |
| **Filtro** | Subcategoría, Nombre (1–100), Activo, `EliminadoEn` | Nombre único dentro de su subcategoría |
| **Opción de filtro** | Filtro, Valor (1–100), Activo, `EliminadoEn` | Valor único dentro de su filtro |
| **Detalle del producto** | Producto, Filtro, Opción | **Una opción por filtro** y por producto; la opción debe ser **de ese filtro** y el filtro **de la subcategoría del producto** |

Reglas de negocio:

1. **Baja suave siempre reversible** (`Activo = 0` + `EliminadoEn`), igual que en Usuarios ([AutenticacionYPermisos.md §5](AutenticacionYPermisos.md)). Un elemento inactivo **no se ofrece** al crear o editar productos, pero **se sigue mostrando** en los productos que ya lo tienen (no se pierde información histórica).
2. **No se puede dar de baja** una categoría con subcategorías activas, ni una subcategoría con **productos activos** (`422` con el motivo). Filtros y opciones se pueden dar de baja siempre.
3. **Eliminación definitiva** (`DELETE`): solo de elementos **ya dados de baja y sin uso** (categoría sin subcategorías; subcategoría sin productos ni filtros; filtro sin opciones; opción sin productos que la usen). Si hay uso, `409` con el motivo. En la interfaz pide confirmación (y escribir el nombre en categorías y subcategorías).
4. **Cambiar la subcategoría de un producto** descarta sus detalles anteriores (los filtros son otros); la pantalla lo avisa antes de guardar.
5. **Compatibilidad**: `Productos.SubcategoriaId` es **opcional** (los productos existentes quedan "Sin clasificar"); el módulo de Productos sigue funcionando igual si no se usa el catálogo.
6. Renombrar un elemento **no** rompe nada: los productos apuntan por id.

## 4. Casos de uso

| Caso de uso | Resultado | Errores |
|---|---|---|
| `ObtenerArbolDeCatalogo` | Categorías → subcategorías (con cantidades de filtros y productos) | — |
| `ObtenerFiltrosDeSubcategoria` | Filtros con sus opciones | `404` |
| `Registrar/Actualizar` Categoría · Subcategoría · Filtro · Opción | Elemento creado/actualizado, con evento | `400`; `404`; `409` nombre repetido o `RowVersion` viejo |
| `CambiarEstado…` (baja/reactivación) de cada uno | Baja suave o reactivación | `404`; `422` por la regla 2 |
| `Eliminar…` | Borrado definitivo | `404`; `409` en uso o activo |
| `ActualizarDetallesDeProducto` (dentro de `ActualizarProducto`) | Subcategoría y opciones del producto | `422` opción ajena a la subcategoría o dos opciones del mismo filtro |

## 5. API

Todas bajo `/api/v1`, con Problem Details y `RowVersion` en las actualizaciones.

| Método y ruta | Permiso | Descripción |
|---|---|---|
| `GET /categorias` | `categorias.ver` | Árbol completo (`?incluirInactivas=true`) |
| `POST /categorias` · `PUT /categorias/{id}` | `categorias.crear` · `.editar` | Alta / edición |
| `PATCH /categorias/{id}/estado` | `categorias.desactivar` | `{ "activo": false }` |
| `DELETE /categorias/{id}` | `categorias.eliminar` | Definitivo (inactiva y vacía) |
| `POST /categorias/{id}/subcategorias` | `categorias.crear` | Alta de subcategoría |
| `PUT` · `PATCH …/estado` · `DELETE /subcategorias/{id}` | `.editar` · `.desactivar` · `.eliminar` | Igual que arriba |
| `GET /subcategorias/{id}/filtros` | `categorias.ver` | Filtros con opciones |
| `POST /subcategorias/{id}/filtros` | `categorias.crear` | Alta de filtro |
| `PUT` · `PATCH …/estado` · `DELETE /filtros/{id}` | `.editar` · `.desactivar` · `.eliminar` | |
| `POST /filtros/{id}/opciones` | `categorias.crear` | Alta de opción |
| `PUT` · `PATCH …/estado` · `DELETE /opciones/{id}` | `.editar` · `.desactivar` · `.eliminar` | |

Cambios en lo existente (los entrega el bloque K junto con el de Productos, ver §9):

* `ProductoDto` añade `SubcategoriaId`, `Subcategoria`, `Categoria` y `Detalles` (`filtroId`, `filtro`, `opcionId`, `valor`). `ActualizarProductoSolicitud` y `RegistrarProductoSolicitud` aceptan `SubcategoriaId` (opcional) y `OpcionIds` (lista).
* `GET /productos` suma los filtros `categoriaId`, `subcategoriaId` y `opcionIds` ([Busquedas.md](Busquedas.md)).
* Tienda pública (anónima, [PortalWeb.md](PortalWeb.md)): `GET /tienda/categorias` (solo activas, con sus filtros y opciones) y `GET /catalogo?subcategoriaId=&opcionIds=` (**Y** entre filtros distintos, **O** entre opciones del mismo filtro).

### Permisos nuevos

Se **añaden al final** del catálogo de permisos (los ids de la semilla son la posición, ver [AutenticacionYPermisos.md §3](AutenticacionYPermisos.md)): `categorias.ver`, `categorias.crear`, `categorias.editar`, `categorias.desactivar` y `categorias.eliminar` (ids 29–33). Un solo grupo cubre categorías, subcategorías, filtros y opciones. Administrador: todos; Operador: solo `categorias.ver` (consulta, para ver cómo se clasifican los productos); Cliente: ninguno (la tienda usa los endpoints anónimos).

## 6. Eventos

Cada servicio de la `Ui` publica `EntidadCambiada` **solo tras** la respuesta exitosa de la API, con las entidades nuevas `Categoria`, `Subcategoria`, `Filtro` y `OpcionFiltro` ([PatronEventos.md](PatronEventos.md)): el `Notificador` avisa, el árbol se refresca y los selectores de Productos se actualizan. El servidor registra cada cambio en el historial ([HistorialActividades.md](HistorialActividades.md)) con detalle legible ("Opción «Rojo» añadida al filtro Color").

## 7. Interfaz

Una sola pantalla, **`CategoriasPagina`** ("Categorías y filtros", menú **Gestión**, `categorias.ver`), en una pestaña como el resto:

```
┌─ Categorías y filtros ───────────────────────────────────────────────────┐
│ [+ Categoría]                                   [ ] Mostrar inactivas     │
├───────────────────────────┬──────────────────────────────────────────────┤
│ ▾ Calzado          ● Activo│ Zapatos deportivos          ● Activo  [Editar]│
│    ▸ Zapatos deportivos   │ Filtros                          [+ Filtro]   │
│    ▸ Sandalias            │  Color   [Rojo ×] [Negro ×] [Blanco ×] [+]    │
│ ▸ Ropa             ● Activo│  Talla   [40 ×] [41 ×] [42 ×] [43 ×]   [+]    │
│ ▸ Tecnología       ○ Inact│  Marca   [Nike ×] [Adidas ×]            [+]    │
│ [+ Subcategoría]          │  (productos en esta subcategoría: 12)         │
└───────────────────────────┴──────────────────────────────────────────────┘
```

* Árbol a la izquierda (categorías → subcategorías); a la derecha, los **filtros de la subcategoría elegida** y sus opciones como *chips* editables (clic en el chip = cambiar estado con confirmación; "+" = agregar).
* Estado como **chip** con confirmación, **baja suave** y **eliminar definitivamente** con las mismas reglas visuales de Usuarios; botones según permiso.
* En **Productos** ([Productos.md](Productos.md)): el formulario suma un selector de **Subcategoría** y, debajo, un selector por cada filtro (una opción cada uno); cambiar la subcategoría avisa que se reiniciarán los detalles. El listado muestra categoría/subcategoría y se puede filtrar por ellas.
* Tienda web: navegación por categorías y filtros laterales, ya implementada ([PortalWeb.md](PortalWeb.md)).

## 8. Plan de pruebas

* **Dominio**: unicidad de nombres por padre, baja suave y reactivación, reglas de baja (categoría con subcategorías activas), una opción por filtro y opción ajena a la subcategoría.
* **Aplicación**: cada caso de uso con repositorio falso; eventos solo tras confirmar; detalle de historial.
* **Integración (SQL Server real)**: índices únicos filtrados, FK compuesta `(OpcionFiltroId, FiltroId)` y `UNIQUE (ProductoId, FiltroId)`, `DELETE` rechazado cuando hay uso, migración desde una base con productos ya cargados (quedan "Sin clasificar").
* **API**: 401/403/éxito por endpoint (el Operador solo consulta); `409` por nombre repetido y por `RowVersion`; catálogo anónimo sin inactivos y con la regla **Y/O** de filtros.
* **ViewModel**: selección de nodos, chips, confirmaciones, publicación de `EntidadCambiada`.
* **E2E (Playwright)**: crear categoría → subcategoría → filtro → opciones; clasificar un producto; verlo filtrado en la tienda; baja y reactivación; eliminar definitivamente solo lo vacío.

## 9. Orden de trabajo y coordinación con el equipo

El bloque **K** ([Tareas.md](../Equipo/Tareas.md)) lo lleva **Pablo** (administración transversal). Los integrantes que hacen **Productos (bloque P)** no se bloquean: `SubcategoriaId` es opcional y su trabajo sigue igual; cuando K-04 (API) esté listo, suman las tareas P-10/P-11 (selector de clasificación y búsqueda). Se **coordina el cambio de `ProductoDto`/endpoints** entre K-07 y P-04 para no pisarse en la migración (una migración a la vez).

## 10. Datos de ejemplo (sembrador de desarrollo)

Para que la administración y la futura tienda "se sientan vivas" y los integrantes vean sus pantallas con datos desde el primer día, la API puede **sembrar datos realistas** al arrancar (`DatosDeEjemplo`; ver [Estructura.md](../Estructura.md)):

| Qué | Cantidad y detalle |
|---|---|
| **Catálogo** | 6 categorías (Ropa, Tecnología, Hogar y cocina, Deportes, Belleza y cuidado personal, Papelería y escolar) con **23 subcategorías** (3 a 5 en Ropa y Tecnología; 3 o 4 en el resto), cada una con sus **filtros y opciones** (Clima, Color, Material, Marca, Talla, Almacenamiento…) y **64 productos** (de 2 a 3 por subcategoría) descritos con una opción por filtro, precios en dólares y stock variado (uno **agotado** y varios con **stock bajo**; una cuarta parte muestra la cantidad exacta en la tienda) |
| **Estados para probar la interfaz** | Una categoría **inactiva y vacía** ("Temporada pasada", se puede eliminar definitivamente), una opción dada de baja ("Fucsia"), 2 productos dados de baja, 2 clientes dados de baja y un operador dado de baja |
| **Personal** | `gerencia` (Administrador), `mramirez`, `jcastillo` y `lmartinez` (Operadores) y `rcruz` (Operador dado de baja). Contraseña de todos: `Demo_Sembrado_2026` (solo desarrollo) |
| **Clientes** | 48 clientes con nombres, teléfonos, direcciones y ciudades reales de El Salvador (65 % en San Salvador, La Libertad, Santa Ana y San Miguel); **6 con cuenta web** (rol Cliente; inician sesión con su correo y la misma contraseña) para probar la tienda |
| **Pedidos** | 90 pedidos con 1 a 4 líneas, repartidos en los últimos 75 días con estados coherentes con su antigüedad (`Pendiente`, `Entregado`, `Cancelado`), total correcto y stock descontado (el cancelado no descuenta) |
| **Historial** | Unas 400 actividades (altas, cambios de estado de pedidos, inicios de sesión) para probar filtros y paginación |

* **Determinista** (misma semilla = mismos datos) e **idempotente**: si la base ya tiene categorías, productos o clientes **no hace nada**. Para volver a sembrar desde cero: `./scripts/up.sh --limpio` (borra la base).
* **Se activa solo en desarrollo**: `SEMBRAR_DATOS_EJEMPLO=true` en `.env` (valor por defecto del compose y del `.env.example`); con `false` la base queda vacía (solo el administrador `admin`). **En la entrega a la universidad se decide** si se deja activado para la demostración.
* No forma parte de las migraciones: es código de la API (`Persistence/Seed/DatosDeEjemplo.cs` y `CatalogoDeEjemplo.cs`) con pruebas contra SQL Server real.

## 11. Decisiones abiertas

* **Orden de las opciones**: hoy alfabético; si hiciera falta un orden manual (tallas 38 < 40 < 42) se añade una columna `Orden`. Propuesta: no hasta que se necesite.
* **Filtros obligatorios por subcategoría** (que un producto deba elegir todos): hoy son opcionales.
