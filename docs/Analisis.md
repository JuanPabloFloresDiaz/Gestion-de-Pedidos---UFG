# 🔍 Análisis del Dominio y Requerimientos - Gestión de Pedidos (UFG)

> Fuente oficial: [Apoyo/2 Proyecto Desarrollo de una aplicación de escritorio para la gestión de pedidos.pdf](Apoyo/2%20Proyecto%20Desarrollo%20de%20una%20aplicaci%C3%B3n%20de%20escritorio%20para%20la%20gesti%C3%B3n%20de%20pedidos.pdf) (Ing. Luis Gavidia, Programación Orientada a Eventos, ciclo 02-2026). Material de apoyo: [4 Delegados](Apoyo/4%20Delegados.pdf) y [5 Eventos](Apoyo/5%20Eventos.pdf).

## 1. Contexto

Sistema para que una empresa **gestione los pedidos de sus clientes**, conectado a SQL Server, para la materia **Programación Orientada a Eventos**. El enunciado pide una **aplicación de escritorio**; como una tienda se usa sobre todo en la web, el sistema tiene **una sola interfaz Blazor con dos hospedajes sobre una misma API**: un **escritorio .NET MAUI Blazor Hybrid** y una **web Blazor Web App**. El ingeniero aclaró que los criterios del enunciado no son cerrados: la tecnología es **libre** mientras el sistema funcione y sea de la **familia .NET**. El objetivo del equipo va más allá del mínimo: un sistema **funcional, desplegable y reutilizable** como base de proyectos posteriores.

Equipo (4 integrantes, todos defienden): ver [Equipo](Especificaciones/InterfazUsuario.md#6-ventana-integrantes) para nombres y carnets tal como figuran en el enunciado.

## 2. Requisitos Oficiales (del enunciado)

### 2.1 Secciones mínimas de datos

| Sección | Campos pedidos |
|---|---|
| **Clientes** | Nombres, Apellidos, Teléfono, Dirección, Departamento, Ciudad |
| **Productos** | Producto, Descripción, Cantidad en stock, Precio de venta, Fecha |
| **Pedidos** | Fecha de pedido, Fecha de entrega, Comentarios, Precio por unidad, Producto, Dueño del pedido, Quién hizo el pedido |

### 2.2 Requerimientos funcionales (RF)

Los códigos RF son nuestros; cada uno cita el punto del enunciado que cubre.

| Código | Requerimiento | Origen | Spec |
|---|---|---|---|
| **RF-01** | **Aplicación de escritorio** (.NET MAUI Blazor Hybrid) y versión web de la **misma interfaz**, con todos los elementos para su ejecución correcta | Enunciado y libertad tecnológica confirmada por el ingeniero | [InterfazUsuario.md](Especificaciones/InterfazUsuario.md) |
| **RF-02** | Conexión a **base de datos SQL Server** | "conectada a una base de datos SQL server" | [BaseDatos.md](BaseDatos.md) |
| **RF-03** | **Clientes**: ingresar (notificando el ingreso), actualizar (notificando), dar de baja = pasar a inactivo (notificando el cambio de estado) | "Permita gestionar las tablas…" | [Clientes.md](Especificaciones/Clientes.md) |
| **RF-04** | **Productos**: ingresar, actualizar y dar de baja, con las mismas notificaciones | ídem | [Productos.md](Especificaciones/Productos.md) |
| **RF-05** | **Pedidos**: registrar, actualizar y dar de baja (activo → inactivo, notificando) | ídem | [Pedidos.md](Especificaciones/Pedidos.md) |
| **RF-06** | **Búsquedas** por distintos campos (pedidos por cliente, por producto, por precio de venta, por apellidos del cliente, etc.) | "Permitir hacer búsquedas…" | [Busquedas.md](Especificaciones/Busquedas.md) |
| **RF-07** | **Interfaz intuitiva**, con **vistas diferenciadas por rol** (administrador, operador) | "Interfaz de Usuario" | [InterfazUsuario.md](Especificaciones/InterfazUsuario.md) |
| **RF-08** | **Inicio de sesión** y **control de acceso por roles** (administrador, operador) | "Autenticación y Seguridad" | [AutenticacionYPermisos.md](Especificaciones/AutenticacionYPermisos.md) |
| **RF-09** | **Menú para ver los integrantes** del equipo (carnets y nombres completos) | "opción en menú para visualizar los integrantes" | [InterfazUsuario.md §6](Especificaciones/InterfazUsuario.md#6-ventana-integrantes) |
| **RF-10** | **Historial de actividades** (opcional en el enunciado; se incluye) | "Historial de Actividades (opcional)" | [HistorialActividades.md](Especificaciones/HistorialActividades.md) |
| **RF-11** | **Portal web público** (Blazor Web App, a cargo de Pablo; **se presenta el 12/11**): catálogo, registro e inicio de sesión de clientes, pedidos propios | Decisión del equipo; el ingeniero aceptó web | [PortalWeb.md](Especificaciones/PortalWeb.md) |

### 2.3 Requerimientos de entrega y calidad

| Código | Requerimiento | Spec |
|---|---|---|
| **RE-01** | **Comentar/documentar el código** explicando qué hace o cómo funciona | [PautasCodigoEventos.md](Equipo/PautasCodigoEventos.md) |
| **RE-02** | **Respaldo FULL (o script) de la BD** y comprobar que se puede restaurar; incluirlo en el comprimido | [RespaldoYEntrega.md](Especificaciones/RespaldoYEntrega.md) |
| **RE-03** | Entregar el documento del enunciado convertido a PDF con **nombres completos y carnets**, y todos los archivos en una carpeta comprimida subida a la U-virtual | [RespaldoYEntrega.md](Especificaciones/RespaldoYEntrega.md) |
| **RE-04** | **Demostración y defensa**: todos los integrantes defienden, ponderado. Sin presentación no se califica | [RespaldoYEntrega.md §4](Especificaciones/RespaldoYEntrega.md) |
| **RE-05** | Equipo de 3 o 4 integrantes (somos 4) | — |

**Fecha de defensa asignada por el ingeniero: jueves 12 de noviembre de 2026** (todos los integrantes defienden).

### 2.4 Rúbrica y dónde se cubre cada criterio

La rúbrica tiene 10 criterios de 1 a 4 puntos (máximo 40). Esta tabla es la lista de verificación de calidad del proyecto:

| Criterio de la rúbrica | Cómo lo cubrimos | Dónde |
|---|---|---|
| Diseña interfaces gráficas amigables | Tema y kit de componentes, validación visible, atajos, estados de carga | [InterfazUsuario.md](Especificaciones/InterfazUsuario.md) |
| Aplicación de escritorio de **interfaz de múltiples documentos (MDI)** | Shell con **pestañas de documentos**: varios módulos abiertos a la vez, sin duplicar, cada uno con su estado | [InterfazUsuario.md §2](Especificaciones/InterfazUsuario.md) |
| Codifica los diferentes **eventos** de la interfaz gráfica | Eventos de controles (`Click`, `Load`, `TextChanged`, `SelectedIndexChanged`, `CellDoubleClick`, `FormClosing`, `KeyDown`…) y eventos propios con delegados | [PatronEventos.md](Especificaciones/PatronEventos.md), [PautasCodigoEventos.md](Equipo/PautasCodigoEventos.md) |
| Agrega comentarios al código | Comentarios XML en español obligatorios en tipos y miembros públicos; revisión en cada PR | [PautasCodigoEventos.md §8](Equipo/PautasCodigoEventos.md) |
| Crea **conexiones seguras** a bases de datos | Usuario de BD con mínimo privilegio, TLS, secretos fuera del repositorio; ni el escritorio ni la web ven la cadena de conexión | [Seguridad.md §3](Seguridad.md) |
| Desarrolla interfaces gráficas de **mantenimiento** de BD | Formularios de alta/edición/baja y listados de Clientes, Productos, Pedidos, Usuarios | Specs de cada módulo |
| Demostración y defensa | Guion de demostración y datos de demostración preparados; todos dominan toda la arquitectura | [RespaldoYEntrega.md §4](Especificaciones/RespaldoYEntrega.md) |
| **Sentencias parametrizadas o procedimientos almacenados** | EF Core parametriza todas las consultas y la búsqueda de pedidos usa un procedimiento almacenado | [BaseDatos.md §6](BaseDatos.md) |
| Ejecución correcta sin errores | Pruebas automáticas (unitarias, integración con SQL Server real, arquitectura y extremo a extremo con Playwright), manejo de errores uniforme | [Testing.md](Especificaciones/Testing.md) |
| Funcionalidad de la aplicación | Todos los RF más pruebas de aceptación | Este documento |
| Trabajo en equipo | Reparto de tareas, ramas, revisiones y guías comunes | [Implementacion.md §4](Implementacion.md), [Tareas.md](Equipo/Tareas.md) |

## 3. Interpretaciones y Decisiones del Equipo

El enunciado es breve; estas interpretaciones fueron **confirmadas por el equipo el 03/10/2026** y son las que usa el resto de la documentación:

| # | Ambigüedad | Interpretación |
|---|---|---|
| I-01 | "Departamento" y "Ciudad" del cliente | Catálogos enlazados (lista dependiente), con el catálogo completo de El Salvador: **14 departamentos y 262 distritos** como "ciudades" ([BaseDatos.md §4.2](BaseDatos.md)) |
| I-02 | "Fecha" del producto | Fecha de ingreso al inventario |
| I-03 | "Producto" y "Precio por unidad" en pedidos | Un pedido con una o varias líneas; el precio unitario se copia a la línea |
| I-04 | "Dueño del pedido" | El **cliente** al que pertenece el pedido |
| I-05 | "Quién hizo el pedido" | El **usuario del sistema** que lo registró: operador o administrador desde la administración, o el propio cliente desde la tienda |
| I-06 | "Eliminar (cambiar el estado activo a inactivo)" | Baja lógica con `Activo`; además los pedidos tienen estado de negocio (Pendiente/Entregado/Cancelado) |
| I-07 | "Notificar" el ingreso/actualización/cambio de estado | Notificación en la interfaz generada mediante **eventos y delegados** (no solo un `MessageBox`) |
| I-08 | Roles | `Administrador` y `Operador` según el enunciado, más `Cliente` para la web pública |
| I-09 | "Windows Form" y "MDI" | El ingeniero aclaró que la tecnología es libre dentro de .NET: se usa **Blazor** y el MDI se resuelve con **pestañas de documentos** ([InterfazUsuario.md §2](Especificaciones/InterfazUsuario.md)) |
| I-10 | Estados del pedido | `Pendiente`, `Entregado` y `Cancelado`; el stock se descuenta al crear y se repone al cancelar |
| I-11 | Sesión | Un solo JWT de **60 minutos**, sin *refresh tokens* ([AutenticacionYPermisos.md](Especificaciones/AutenticacionYPermisos.md)) |
| I-12 | Clasificación de productos (**extensión**, no pedida por el enunciado) | Categorías, subcategorías, filtros y opciones para describir mejor los productos y filtrar la tienda, en versión básica (sin SKU ni variantes). No altera los campos que pide RF-04: es opcional y aditiva ([CatalogoProductos.md](Especificaciones/CatalogoProductos.md)) |

## 4. Restricciones Técnicas Decididas

| # | Restricción | Origen |
|---|---|---|
| RT-01 | Plataforma **.NET 8** (C# 12) como **máximo**: el ingeniero no permite .NET 9 ni .NET 10 | Decisión del ingeniero |
| RT-02 | **Interfaz Blazor única** (`Ui`) hospedada en **.NET MAUI Blazor Hybrid** (escritorio, Windows) y en **Blazor Web App** (web); administración con **pestañas de documentos** (interfaz de múltiples documentos) | El ingeniero aceptó tecnología libre de la familia .NET; decisión del equipo |
| RT-10 | **Delegados y eventos de C#** para las notificaciones, además de los eventos propios de Blazor | Material del ingeniero |
| RT-03 | **API .NET** con **Entity Framework Core** entre el cliente y la base de datos (permite alojarla en un servidor y reutilizar el backend) | Decisión del equipo, **aprobada por el ingeniero** (la única condición es que sean tecnologías de la familia .NET) |
| RT-04 | Base de datos **SQL Server** | Enunciado |
| RT-05 | **API y base de datos en Docker**; el cliente de escritorio se desarrolla y ejecuta en Windows con VS Code y el SDK de .NET 8 ([Infraestructura.md](Infraestructura.md)) | Decisión del equipo |
| RT-06 | **Delegados y eventos** como mecanismo de notificación | Enunciado y material de apoyo |
| RT-07 | Código limpio y principios de diseño ([Principios.md](Principios.md)) | Decisión del equipo |
| RT-08 | **Spec Driven Documentation**: se especifica antes de implementar | Convención del ecosistema |
| RT-09 | El proyecto **no depende** de otros proyectos externos del equipo (autenticación y demás son propios) | Indicación del ingeniero |

## 5. Requerimientos No Funcionales (RNF)

* **RNF-01 (Reproducibilidad)**: `docker compose up` levanta la API y SQL Server sin instalar nada más en el host.
* **RNF-02 (Integridad)**: operaciones multi-tabla atómicas (ACID, [Principios.md §3](Principios.md)); stock nunca negativo.
* **RNF-03 (Mantenibilidad)**: capas separadas, pruebas por capa, pautas de código comunes para todo el equipo (también para los agentes de IA que usen los compañeros).
* **RNF-04 (Seguridad)**: contraseñas con hash, JWT de corta duración, mínimo privilegio, validación de entrada ([Seguridad.md](Seguridad.md)).
* **RNF-05 (Usabilidad)**: operaciones comunes en pocos clics, mensajes claros en español, sin bloquear la interfaz durante llamadas a la API.
* **RNF-06 (Rendimiento)**: listados paginados; respuesta de consultas habituales < 1 s con el volumen de la demostración (miles de registros).
* **RNF-07 (Trazabilidad)**: acciones relevantes quedan en el historial ([HistorialActividades.md](Especificaciones/HistorialActividades.md)).
* **RNF-08 (Portabilidad de despliegue)**: la API y la web quedan listas para un servidor (Dokploy); el despliegue está **pospuesto** porque SQL Server no cabe en el servidor disponible ([Infraestructura.md §4](Infraestructura.md)).

## 6. Opciones Fuera del Alcance Actual

> Solo como opciones; no se implementan salvo decisión posterior.

* **Pagos en línea**: además de "Efectivo" y "Transferencia bancaria", ofrecer pago en línea en modo *sandbox* (por ejemplo, el sandbox de Pagadito), posiblemente mediante un microservicio de pagos propio. Pablo lo consultará con el ingeniero antes de decidir.
* **Otro motor de base de datos** (MySQL/MariaDB o PostgreSQL, más livianos) para poder alojarlo en el servidor; Pablo lo consultará con el ingeniero ([BaseDatos.md §11](BaseDatos.md)).
* **Movimientos de inventario**, **reportes** (PDF/Excel), **múltiples roles por usuario**.
