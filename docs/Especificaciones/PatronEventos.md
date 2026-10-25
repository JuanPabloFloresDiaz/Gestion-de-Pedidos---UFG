# 🔔 Delegados y Eventos: Especificación

> **Fuente:** material del ingeniero [4 Delegados](../Apoyo/4%20Delegados.pdf) y [5 Eventos](../Apoyo/5%20Eventos.pdf). El objetivo de la materia es **usar delegados y eventos**; los ejemplos del material (formularios de Windows Forms) son ilustrativos. Cómo **escribir** el código (plantillas y reglas) está en [PautasCodigoEventos.md](../Equipo/PautasCodigoEventos.md); este documento define **qué eventos existen, quién los publica y quién reacciona**.

## 1. Qué Exige el Material (resumen)

* **Delegado**: tipo que referencia métodos con una firma concreta; sirve para **pasar métodos como parámetro** (`delegate int OperarDelegate(int a, int b);`).
* **Evento**: mecanismo con el que una clase **publicadora** notifica a clases **suscriptoras** que ocurrió algo. Se declara con un delegado (sufijo `EventHandler`) y la palabra `event`.
* Pasos: declarar el delegado → declarar el `event` → **comprobar que no sea `null`** y dispararlo `(this, args)` → suscribirse con `+=` → el manejador tiene la **misma firma** del delegado.
* Para **devolver datos** se crea una clase `...EventArgs` propia.
* El editor decide **cuándo** se lanza; el suscriptor decide **qué hacer**.

## 2. ¿Lo Hacen Entity Framework o Blazor por Nosotros?

No: delegados y eventos son del lenguaje C#. Las tecnologías **los usan** y conviven con nuestro patrón:

| Tecnología | Qué trae | Relación con el patrón |
|---|---|---|
| **Blazor** | `@onclick`, `@onchange`, `@oninput`, `@onkeydown`…; `EventCallback<T>` (un delegado con envoltorio que además repinta el componente) | Son eventos de **interfaz** y de comunicación entre componentes; nuestros eventos propios (de ViewModel y de servicios) se suscriben con `+=` como en el material |
| **EF Core** | `SavingChanges`, `SavedChanges`, `SaveChangesFailed`, `ChangeTracker.StateChanged`; interceptores | Eventos **técnicos** de persistencia; **no** generan eventos de negocio como "pedido registrado" |
| **LINQ** | `Where(p => p.Activo)` usa delegados (`Func<>`/expresiones) | Es "pasar un método como parámetro" sin declararlo |
| **Nuestro código** | Delegados y `event` propios | Eventos de **negocio**, de **ViewModel** y de **servicios** (§3) |

> `EventCallback` **no reemplaza** a los delegados del material: es la forma de Blazor de pasar un evento de un componente hijo a su padre. Los eventos de negocio se escriben con `delegate` + `event` + `EventArgs`.

## 3. Catálogo de Eventos

### 3.1 Eventos de la interfaz (`Ui/Comun/Eventos`)

| Evento | Delegado / Args | Publicadora | Suscriptores | Cuándo |
|---|---|---|---|---|
| `EntidadCambiada` | `EntidadCambiadaEventHandler(object sender, EntidadCambiadaEventArgs e)`<br>Args: `Entidad` (Cliente/Producto/Pedido/Usuario), `Id`, `Tipo` (**Registrada**/**Actualizada**/**EstadoCambiado**), `Descripcion`, `Activo` | `ClientesService`, `ProductosService`, `PedidosService`, `UsuariosService` | `Notificador` (aviso en pantalla); ViewModels abiertos del mismo módulo y de módulos relacionados (refrescan listados) | Tras respuesta **exitosa** de la API en alta, edición o baja/reactivación (RF-03/04/05) |
| `StockInsuficiente` | `StockInsuficienteEventHandler`<br>Args: `ProductoId`, `ProductoNombre`, `Solicitado`, `Disponible` | `PedidosService` | `Notificador`; `PedidosViewModel` (resalta la línea) | La API rechaza un pedido por falta de existencias |
| `OperacionFallida` | `OperacionFallidaEventHandler`<br>Args: `Operacion`, `Mensaje` | Servicios de módulo | `Notificador` (mensaje de error) | Error de negocio o de comunicación |
| `SesionIniciada` | `SesionEventHandler`<br>Args: `UsuarioId`, `NombreUsuario`, `Rol`, `Permisos` | `SesionService` | `MainLayout` (arma el menú por permisos), `Notificador` | Login exitoso |
| `SesionPorExpirar` | `SesionPorExpirarEventHandler`<br>Args: `MinutosRestantes` | `SesionService` (temporizador) | `MainLayout` (avisa para guardar el trabajo) | 5 minutos antes de que venza el JWT de 60 min |
| `SesionCerrada` / `SesionExpirada` | `EventHandler` | `SesionService` | `MainLayout` (cierra pestañas y muestra el login) | Logout / el JWT venció |
| `DocumentoAbierto` / `DocumentoCerrado` / `DocumentoActivado` | `DocumentoEventHandler`<br>Args: `Clave`, `Titulo` | `DocumentosService` | `MainLayout` (pestañas), `Notificador` | El usuario abre, cierra o cambia de pestaña |
| `CarritoCambiado` | `CarritoEventHandler`<br>Args: `Cantidad`, `Total` | `CarritoService` (tienda) | Barra superior (contador), página del carrito | Se agrega, cambia o quita una línea |

### 3.2 Eventos del ViewModel y de los componentes (por página)

Cada `ViewModel` expone un evento `Cambiado` (`EventHandler`) que la página usa para repintarse, y la página reexpone los eventos HTML hacia el ViewModel invocando sus métodos. Ejemplo de Clientes:

| Origen | Evento | Qué hace el ViewModel |
|---|---|---|
| `btnBuscar` `@onclick`, `Enter` `@onkeydown`, pausa tras `@oninput` | `BuscarAsync()` | Consulta la API con los filtros y dispara `Cambiado` |
| Fila `@ondblclick` / botón Nuevo | `EditarAsync(id)` / `Nuevo()` | Carga o limpia el panel de edición |
| `EditForm` `OnValidSubmit` | `GuardarAsync()` | Valida y llama al servicio |
| Botón Desactivar `@onclick` | `CambiarEstadoAsync()` | Confirma y llama al servicio |
| Selector de departamento `@onchange` | `DepartamentoSeleccionadoAsync(id)` | Carga las ciudades del departamento |
| `Paginador.PaginaCambiada` (`EventCallback<int>`) | `IrAPaginaAsync(n)` | Recarga la página de resultados |
| `Dispose()` de la página | — | Se da de baja de `Cambiado` y de los eventos de servicio |

### 3.3 Eventos de aplicación en el servidor (`Application`)

| Evento | Publicadora | Suscriptores | Cuándo |
|---|---|---|---|
| `EntidadCambiada` (servidor): `Entidad`, `EntidadId`, `Tipo`, `UsuarioId`, `Detalle` | Casos de uso de Clientes, Productos, Pedidos, Usuarios | `HistorialSubscriber` → inserta en `Actividades` | **Después** de confirmar la transacción |
| `AccesoRegistrado`: `NombreUsuario`, `UsuarioId?`, `Exitoso` | `Autenticar` | `HistorialSubscriber` | Cada intento de login |

> Los eventos del servidor y del cliente son **independientes**: el servidor no notifica a otros clientes (sin SignalR). El cliente genera sus notificaciones al recibir la respuesta de la API.

## 4. Delegados como Parámetro (PDF 4)

Usos previstos en el proyecto (detalle y plantillas en [PautasCodigoEventos.md §6](../Equipo/PautasCodigoEventos.md)):

| Uso | Delegado | Dónde |
|---|---|---|
| Ejecutar una operación con indicador de carga y manejo de errores uniforme | `Func<CancellationToken, Task>` | `ViewModelBase.EjecutarAsync(...)` |
| Reglas de validación componibles | `Func<T, string?>` | Validación de la interfaz y del dominio |
| Ordenar/filtrar en memoria listas pequeñas (catálogos) | `Func<T, bool>`, `Comparison<T>` | ViewModels |
| Estrategias intercambiables de cálculo (p. ej. totales/descuentos si se añaden) | `delegate decimal CalculoDelegate(...)` | Dominio de Pedidos |

## 5. Eventos de Sesión (Fase 1)

`SesionService` publica `SesionIniciada`, `SesionPorExpirar`, `SesionCerrada` y `SesionExpirada`. `MainLayout` solo reacciona a estos eventos para construir o desmontar la interfaz; ningún formulario consulta directamente "quién está logueado" fuera de lo que la sesión publica.

## 6. Plan de Pruebas

* La publicadora dispara el evento **una vez** con los `EventArgs` correctos al tener éxito.
* **No** lo dispara si la operación falla o la transacción se revierte.
* Un suscriptor falso (`+=` con una lambda) captura el evento y se verifica `sender` y datos.
* Sin suscriptores no ocurre ninguna excepción (`?.Invoke`).
* Un ViewModel reacciona a los eventos de un **servicio falso** (se disparan manualmente) y no depende de componentes ni de pantalla.
* En la interfaz real (Playwright) se comprueba que el aviso aparece tras una operación y que el listado se refresca.
* Tras cerrar la pestaña (`Dispose`), la página y su ViewModel ya no reciben eventos (se dio de baja con `-=`).
* Ver [Testing.md](Testing.md).

## 7. Decisiones

| Fecha | Decisión | Motivo |
|---|---|---|
| 2026-10-01 | Mecanismo = `delegate` + `event` + `EventArgs` | Lo pide el ingeniero |
| 2026-10-01 | Publicar tras confirmar la operación | Evitar reaccionar a cambios revertidos |
| 2026-10-03 | Un delegado genérico `EntidadCambiada` con tipo de cambio en lugar de uno por cada alta/edición/baja | Evita 9+ delegados casi idénticos; cumple el patrón y mantiene el catálogo pequeño |
| 2026-10-03 | MVP con eventos de vista | (reemplazado el 2026-10-21) |
| 2026-10-21 | Una sola interfaz Blazor (MAUI Hybrid + Web App) con Página + ViewModel, en lugar de WinForms + web separadas | El ingeniero aclaró que la tecnología es libre dentro de .NET; evita mantener dos interfaces |
