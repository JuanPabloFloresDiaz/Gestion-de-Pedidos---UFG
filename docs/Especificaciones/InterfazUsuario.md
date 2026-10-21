# 🖥️ Interfaz de Usuario (Blazor: escritorio y web)

> Cubre **RF-01, RF-07, RF-09** y los criterios de la rúbrica de interfaz amigable, interfaz de múltiples documentos y eventos de la interfaz. Toda la interfaz está en la **Razor Class Library `GestionPedidos.Ui`** y se muestra en dos hospedajes: **escritorio** (.NET MAUI Blazor Hybrid) y **web** (Blazor Web App) ([Arquitectura.md §1](../Arquitectura.md)). La tienda pública está en [PortalWeb.md](PortalWeb.md).

## 1. Principios de Diseño

* **Intuitiva para no técnicos** (operadores y administradores): pocas pantallas, español claro, acciones frecuentes a un clic.
* **Consistente**: todas las pantallas de módulo siguen la misma disposición (§3), componentes del kit propio y colores del tema.
* **Nunca congelar la pantalla**: toda llamada a la API es asíncrona, con indicador de carga y botones deshabilitados mientras dura.
* **Prevenir antes que avisar**: botones inhabilitados si falta algo, validación mientras se escribe, confirmación antes de acciones destructivas (baja, cancelar pedido).
* **Mensajes útiles**: qué pasó y qué hacer ("El cliente tiene pedidos pendientes; entréguelos o cancélelos primero").
* **Accesible**: etiquetas asociadas a cada campo, orden de tabulación lógico, foco visible, contraste suficiente, navegación completa con teclado, `aria-label` en iconos.
* **Adaptable**: diseño responsivo (cuadrícula y *flex*); la tienda debe usarse bien en teléfono y la administración en pantallas de 1280 px en adelante.
* **Aspecto propio**: un único tema (variables CSS / tema de MudBlazor) y un kit de componentes ([§8](#8-tema-y-kit-de-componentes)).

## 2. Estructura: Shell con Pestañas de Documentos (múltiples documentos)

La rúbrica puntúa una "interfaz de múltiples documentos". En Blazor se logra con **pestañas de documentos**: cada módulo se abre en su propia pestaña, se pueden tener varios a la vez y se cambia entre ellos sin perder el estado.

```
┌─ MainLayout ─────────────────────────────────────────────────────────────┐
│ ☰  Gestión de Pedidos      Gestión ▾  Administración ▾  Ayuda ▾   ana ▾    │
├───────────────────────────────────────────────────────────────────────────┤
│ [Clientes ✕] [Pedidos ✕] [Pedido #12 ✕]                                    │
│ ┌───────────────────────────────────────────────────────────────────────┐ │
│ │  Contenido del documento activo                                       │ │
│ └───────────────────────────────────────────────────────────────────────┘ │
├───────────────────────────────────────────────────────────────────────────┤
│ Usuario: ana · Rol: Operador │ ✔ Cliente Ana Pérez registrado │ API: en línea│
└───────────────────────────────────────────────────────────────────────────┘
```

* **Servicio de documentos** (`DocumentosService`): lista de pestañas abiertas y la activa; publica el evento `DocumentoAbierto/Cerrado/Activado`. Abrir un módulo ya abierto **activa** su pestaña en lugar de duplicarla.
* Cada pestaña **conserva su estado** (filtros, página, borrador) porque su `ViewModel` vive mientras la pestaña esté abierta; al cerrarla se libera (`Dispose`).
* Cerrar una pestaña con cambios sin guardar pide confirmación.
* Atajos: `Ctrl+Tab` (siguiente pestaña) y `Ctrl+W` (cerrar).
* **Se avisa al ingeniero** de que el "MDI" del enunciado se resuelve con pestañas (es la forma habitual de interfaz de múltiples documentos en aplicaciones Blazor/web).

### 2.1 Menú según rol

| Menú | Opciones | Visible para |
|---|---|---|
| **Gestión** | Clientes · Productos · Pedidos | Quien tenga `*.ver` del módulo |
| **Administración** | Usuarios · Historial de actividades | `usuarios.ver` / `historial.ver` (administrador) |
| **Ayuda** | **Integrantes del equipo** · Acerca de | Todos |
| **Usuario** | Cambiar contraseña · Cerrar sesión | Todos |

El menú se arma al recibir `SesionIniciada`. Los botones de cada módulo (Nuevo, Desactivar…) se habilitan según el permiso. Es **comodidad**; la barrera real es la API ([Seguridad.md §5](../Seguridad.md)).

### 2.2 Qué se ve en cada hospedaje

* **Desktop**: abre en el login y luego en **Administración** (`admin.acceso`); un usuario `Cliente` es rechazado.
* **Web**: abre en la **tienda pública**; el personal puede iniciar sesión y usar también Administración, y el cliente solo ve la tienda ([PortalWeb.md](PortalWeb.md)).

## 3. Disposición de una Página de Módulo

```
┌─ <Módulo> ───────────────────────────────────────────────┐
│ [Filtros colapsables]            [Buscar] [Limpiar]       │
├───────────────────────────────────────────────────────────┤
│ Tabla (listado paginado)               │ Panel de edición │
│  · doble clic → editar                 │  campos + errores│
│  · filas inactivas en gris             │  [Guardar]       │
│                                        │  [Desactivar]    │
├───────────────────────────────────────────────────────────┤
│ « ‹ página 2 de 9 › »     [Nuevo]                         │
└───────────────────────────────────────────────────────────┘
```

* Filas **inactivas** en gris con la columna "Estado"; por defecto no se listan ("Mostrar inactivos" para quien pueda).
* Validación visible junto a cada campo; botón Guardar deshabilitado mientras la operación está en curso.
* Los listados son de solo lectura; se edita en el panel.
* Estructura común en un componente `PaginaModulo` con ranuras (`RenderFragment`) para filtros, tabla y panel, para no repetir la disposición (DRY).

## 4. Eventos de Interfaz que Debe Demostrar el Proyecto

La rúbrica puntúa codificar eventos de la interfaz. Los de Blazor y dónde se usan:

| Evento | Elemento | Uso |
|---|---|---|
| `@onclick` | Botones, filas, pestañas, menús | Acciones: buscar, guardar, nuevo, desactivar, abrir módulo |
| `@oninput` / `@onchange` | Cuadros de texto, selectores | Búsqueda con retraso (400 ms) y validación al escribir; `change` en listas |
| `@onkeydown` | Cuadros de texto y shell | *Enter* para buscar; `Ctrl+Tab` y `Ctrl+W` en las pestañas |
| `@onblur` / `@onfocus` | Campos | Validar al salir del campo |
| `@ondblclick` | Filas de tabla | Editar |
| `@onsubmit` (`EditForm`) | Formularios | Guardar con validación (`OnValidSubmit`/`OnInvalidSubmit`) |
| `EventCallback<T>` | Parámetros de componentes | El componente hijo (`Paginador`, `TablaDatos`, `CampoTexto`) avisa a la página (`PaginaCambiada`, `FilaSeleccionada`) |
| `OnInitializedAsync` / `Dispose` | Ciclo de vida del componente | Cargar datos al abrir; darse de baja de eventos al cerrar |
| `@onbeforeunload` (JS interop) | Ventana | Avisar si hay cambios sin guardar al cerrar la pestaña del navegador (web) |

Más los **eventos propios con delegados** (`EntidadCambiada`, `StockInsuficiente`, `Cambiado` del ViewModel…) de [PatronEventos.md](PatronEventos.md).

## 5. Notificaciones al Usuario

* Éxitos (alta, actualización, cambio de estado): aviso **no bloqueante** (*snackbar*) de ~5 s con icono ✔ y también en la barra de estado, generado por el `Notificador` al recibir `EntidadCambiada`.
* Avisos importantes y errores de negocio: panel de aviso o diálogo con título y botón claros.
* Confirmaciones destructivas: diálogo Sí/No con "No" por defecto.
* Los textos están centralizados en una clase `Mensajes` para mantener un tono uniforme.

## 6. Ventana Integrantes

Opción **Ayuda → Integrantes del equipo** (RF-09). Muestra **carnet y nombre completo** de todos, tal como figura en el enunciado:

| Carnet | Nombre completo |
|---|---|
| FD100125 | Juan Pablo Flores Díaz |
| FC100426 | Anderson Rene Figueroa Coto |
| MJ100326 | Adiel Enrique Montepeque Juarez |
| AC101325 | Daniel Alexander Álvarez Chávez |

Muestra además asignatura, docente (Ing. Luis Gavidia), ciclo 02-2026 y versión de la aplicación. Los datos viven en un solo lugar (`appsettings.json` o una clase de constantes). Es un diálogo modal con botón Cerrar.

## 7. Login

* Página de inicio de sesión (identificador y contraseña, mostrar/ocultar contraseña, botón Ingresar con *Enter*, mensajes genéricos ante error, indicador de carga).
* Si `debeCambiarClave`, se muestra el cambio de contraseña antes de entrar.
* En **Desktop**, un usuario sin el permiso `admin.acceso` (por ejemplo, un cliente) se rechaza con el mensaje genérico de credenciales incorrectas.
* **5 minutos antes** de vencer el token (60 min) se muestra un aviso para guardar el trabajo (`SesionPorExpirar`); al vencer, se cierran las pestañas y se vuelve al login.
* Cerrar sesión: cierra las pestañas, borra la sesión y vuelve al login.

## 8. Tema y Kit de Componentes

Para que el aspecto sea propio y uniforme, **todo sale de dos lugares compartidos** en `Ui`:

**Tema** (`wwwroot/css/tema.css` + `Tema.cs` si se usa MudBlazor): paleta, tipografía, espaciados y radios. Es el **único** lugar con colores y fuentes; cambiar el aspecto de toda la aplicación es cambiar el tema. Debe incluir modo claro y oscuro.

**Kit** (`Comun/Componentes`), construido sobre MudBlazor (a validar en la Fase 0) o HTML/CSS propio:

| Componente | Qué aporta |
|---|---|
| `Boton` (primario, secundario, peligro) | Estilos del tema, estado deshabilitado, indicador de carga |
| `CampoTexto` / `CampoSelector` | Etiqueta + entrada + mensaje de error integrado |
| `TablaDatos<T>` | Tabla de solo lectura con columnas por plantilla, filas alternas, filas inactivas en gris y `EventCallback` de selección |
| `Paginador` | « ‹ página 2 de 9 › » con `PaginaCambiada` |
| `PaginaModulo` | Disposición estándar de §3 con ranuras |
| `Dialogo` / `Aviso` | Confirmaciones y mensajes |

Reglas:
* Ninguna página usa colores, fuentes ni tamaños sueltos: usa el tema y los componentes del kit.
* Un componente que se necesite en **dos** módulos pasa al kit (regla de tres).
* Los componentes del kit exponen **`EventCallback`** para avisar a la página y, si corresponde, eventos propios con delegados.
* Los comentarios documentales (`///` en C#, `@* … *@` en Razor) son obligatorios ([PautasCodigoEventos.md §8](../Equipo/PautasCodigoEventos.md)).

## 9. Plan de Pruebas

* **ViewModels y servicios**: cada comportamiento sin navegador ni pantalla ([Testing.md](Testing.md)).
* **Componentes** (bUnit): kit y formularios clave (validación, eventos).
* **Aceptación manual por rol** en **ambos hospedajes**: lista de verificación (administrador vs operador vs cliente) con capturas para la defensa; incluye cambio de tamaño de ventana, zoom al 125 %/150 % y navegación solo con teclado.
