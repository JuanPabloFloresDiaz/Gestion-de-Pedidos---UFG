# 🧭 Pautas de Código: Delegados, Eventos y Estilo Común

> **Para todo el equipo y para los agentes de IA** (Antigravity u otros). Objetivo: que los cuatro integrantes escriban el **mismo tipo de código**, sin mezclar estilos. Si una sugerencia de un agente contradice este documento, **gana este documento**. Si algo no está cubierto, se propone un cambio aquí (por PR) antes de improvisar.
>
> Contexto: [PatronEventos.md](../Especificaciones/PatronEventos.md) define **qué** eventos existen; aquí se define **cómo se escriben**. La interfaz es **Blazor** (biblioteca `GestionPedidos.Ui`) y se muestra en escritorio (.NET MAUI Blazor Hybrid) y en web (Blazor Web App) ([Arquitectura.md](../Arquitectura.md)).

## 1. Cómo Usar este Documento

* **Antes de programar un módulo**: leer su spec en [Especificaciones/](../Especificaciones/README.md) y este documento completo.
* **Con un agente de IA**: pegarle el *prompt base* de §11 al iniciar la sesión y pedirle que cite la sección de estas pautas que aplica. Quien sube el código **lo entiende y puede defenderlo** (en la defensa se pregunta).
* **Plantilla real**: el módulo **Usuarios** (Fase 1) es el módulo de referencia; copiar su estructura, no inventar otra.

## 2. Reglas de Oro

1. **Las páginas `.razor` no tienen lógica.** Solo marcado y enlace de eventos; el estado y la lógica de interfaz van en el **ViewModel** (§5).
2. **Un evento de negocio se dispara solo cuando la operación ya tuvo éxito** (respuesta correcta de la API / transacción confirmada).
3. **Todo `+=` tiene su `-=`**, en `Dispose()` del componente o del ViewModel.
4. **Nombres fijos**: delegado `XEventHandler`, datos `XEventArgs`, evento `X`, manejador `Origen_Evento`, método de disparo `OnX` (§3).
5. **Disparar siempre con `?.Invoke(this, args)`**; nunca llamar al delegado sin comprobar `null`.
6. **Nunca bloquear la interfaz**: `async/await` de extremo a extremo; prohibido `.Result`, `.Wait()` y `Thread.Sleep`.
7. **`async void` solo en manejadores de eventos de C#** (no en métodos de Razor, que devuelven `Task`) y siempre con `try/catch` (se usa `EjecutarAsync`, §5).
8. **Sin SQL ni cadena de conexión en la `Ui`, `Web` ni `Desktop`**: solo hablan con la API.
9. **Sin cadenas SQL concatenadas** en la API: solo LINQ de EF Core o el procedimiento con parámetros.
10. **Todo tipo y miembro público lleva comentario XML en español** (§8).
11. **Nada se copia entre módulos**: lo repetido tres veces se extrae a `Comun/` (Ui) o `Common/` (Application).
12. **Una clase pública por archivo**; el archivo se llama como la clase.
13. **Nada exclusivo de un hospedaje dentro de `Ui`**: ni `HttpContext`, ni APIs de MAUI, ni `window`/`document` sin JS interop; lo específico entra por interfaces (`ISesionAlmacen`) registradas en `Web` y `Desktop`.
14. **Estilo solo desde el tema y el kit** de `Comun/Componentes`: sin colores, fuentes ni tamaños sueltos en una página (§5.1).

## 3. Anatomía de un Evento (plantilla)

Los cuatro elementos del material del ingeniero: **delegado → `EventArgs` → publicadora → suscriptor**.

```csharp
// Ui/Comun/Eventos/EntidadCambiadaEventArgs.cs
namespace GestionPedidos.Ui.Comun.Eventos;

/// <summary>Tipo de entidad a la que se refiere una notificación.</summary>
public enum TipoEntidad { Cliente, Producto, Pedido, Usuario }

/// <summary>Qué le ocurrió a la entidad.</summary>
public enum TipoCambio { Registrada, Actualizada, EstadoCambiado }

/// <summary>Datos del evento <see cref="IPublicadorDeCambios.EntidadCambiada"/>.</summary>
public class EntidadCambiadaEventArgs : EventArgs
{
    /// <summary>Tipo de entidad afectada.</summary>
    public required TipoEntidad Entidad { get; init; }

    /// <summary>Identificador de la entidad en la base de datos.</summary>
    public required int Id { get; init; }

    /// <summary>Qué ocurrió: alta, edición o cambio de estado.</summary>
    public required TipoCambio Tipo { get; init; }

    /// <summary>Texto legible para mostrar al usuario (ej. "Ana Pérez").</summary>
    public required string Descripcion { get; init; }

    /// <summary>Estado activo resultante (útil en cambios de estado).</summary>
    public bool Activo { get; init; } = true;
}
```

```csharp
// Ui/Comun/Eventos/EntidadCambiadaEventHandler.cs
/// <summary>Firma de los manejadores del evento <c>EntidadCambiada</c>.</summary>
/// <param name="sender">Objeto que publicó el evento (la clase publicadora).</param>
/// <param name="e">Datos del cambio.</param>
public delegate void EntidadCambiadaEventHandler(object sender, EntidadCambiadaEventArgs e);
```

```csharp
// Ui/Comun/Eventos/IPublicadorDeCambios.cs
/// <summary>Contrato de las clases que notifican altas, ediciones y cambios de estado.</summary>
public interface IPublicadorDeCambios
{
    /// <summary>Se produce después de que la API confirma un cambio.</summary>
    event EntidadCambiadaEventHandler? EntidadCambiada;
}
```

**Publicadora** (un servicio de módulo de la `Ui`):

```csharp
/// <summary>Operaciones de clientes contra la API; avisa cuando algo cambia.</summary>
public sealed class ClientesService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Se produce tras registrar, actualizar o cambiar el estado de un cliente.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    public ClientesService(ApiClient api) => _api = api;

    /// <summary>Registra un cliente y notifica el ingreso.</summary>
    public async Task<ClienteDto> RegistrarAsync(RegistrarClienteSolicitud solicitud, CancellationToken ct)
    {
        // 1) Primero la API: si falla, lanza excepción y NO se notifica nada.
        ClienteDto creado = await _api.PostAsync<ClienteDto>("/api/v1/clientes", solicitud, ct);

        // 2) Solo con la operación confirmada se avisa a los suscriptores.
        OnEntidadCambiada(new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Cliente,
            Id = creado.Id,
            Tipo = TipoCambio.Registrada,
            Descripcion = $"{creado.Nombres} {creado.Apellidos}",
        });
        return creado;
    }

    /// <summary>Dispara el evento si hay suscriptores.</summary>
    private void OnEntidadCambiada(EntidadCambiadaEventArgs e) => EntidadCambiada?.Invoke(this, e);
}
```

**Suscriptor** (el `Notificador`, recibe todos los servicios por DI):

```csharp
/// <summary>Muestra un aviso en pantalla por cada cambio notificado.</summary>
public sealed class Notificador : IDisposable
{
    private readonly IReadOnlyList<IPublicadorDeCambios> _publicadores;
    private readonly IAvisos _avisos;

    public Notificador(IEnumerable<IPublicadorDeCambios> publicadores, IAvisos avisos)
    {
        _avisos = avisos;
        _publicadores = publicadores.ToList();
        foreach (IPublicadorDeCambios p in _publicadores)
            p.EntidadCambiada += Publicador_EntidadCambiada;   // suscripción con +=
    }

    /// <summary>Manejador: misma firma que <see cref="EntidadCambiadaEventHandler"/>.</summary>
    private void Publicador_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) =>
        _avisos.Exito(Mensajes.Cambio(e));   // textos centralizados en Mensajes

    /// <summary>Se da de baja de todos los eventos (par obligatorio del +=).</summary>
    public void Dispose()
    {
        foreach (IPublicadorDeCambios p in _publicadores)
            p.EntidadCambiada -= Publicador_EntidadCambiada;
    }
}
```

### Reglas de eventos
* **Nombre del evento**: hecho en participio o sustantivo de suceso (`EntidadCambiada`, `StockInsuficiente`, `SesionIniciada`).
* **Siempre** `sender` = la publicadora (`this`) y datos en un `EventArgs` propio. Sin datos: `EventArgs.Empty`.
* Los `EventArgs` son **inmutables** (`init`) y no llevan lógica.
* **Hilo (Blazor)**: un evento puede llegar fuera del ciclo de render. El componente que lo atiende repinta con `await InvokeAsync(StateHasChanged)`, nunca con `StateHasChanged()` directo.
* Los suscriptores **no deben lanzar excepciones** ni hacer trabajo largo; si deben hacerlo, capturan y registran el error.
* **Prohibido** `static event` global y suscribir lambdas anónimas cuando luego haya que darse de baja (no se puede hacer `-=` de una lambda nueva).
* **Vida de los servicios**: en el hospedaje web los servicios `Scoped` viven por circuito (una sesión de navegador); en MAUI viven toda la aplicación. Un evento no llega a otras sesiones de navegador, y está bien (sin SignalR).
* En el **servidor** (`Application`) se sigue el mismo patrón, con una clase publicadora por módulo y el `HistorialSubscriber` como suscriptor; se publica **después** de `SaveChangesAsync`.

## 4. Dónde Va Cada Cosa

| Qué | Dónde | Notas |
|---|---|---|
| Delegado + `EventArgs` compartidos de la interfaz | `Ui/Comun/Eventos/` | Un archivo por tipo |
| Delegado + `EventArgs` propios de un módulo | `Ui/Modulos/<Modulo>/Eventos/` | Solo si el evento es exclusivo del módulo |
| Eventos de aplicación (servidor) | `Application/<Modulo>/Eventos/` | Publicados tras confirmar la transacción |
| Suscriptores del servidor | `Application/Historial/` | `HistorialSubscriber` |
| Publicadoras de la interfaz | `Ui/Modulos/<Modulo>/<Modulo>Service.cs` | Una por módulo |
| Suscriptores de la interfaz | `Ui/Comun/Notificaciones/`, ViewModels | `Notificador`, refrescos de listados |

## 5. Página + ViewModel con Eventos

Tres piezas por pantalla: la **página** (`.razor`, solo marcado), el **ViewModel** (estado y lógica, clase C# normal sin dependencias de Blazor) y el **servicio** del módulo. El ViewModel expone el evento `Cambiado` y la página se suscribe para repintarse.

```csharp
// Ui/Comun/ViewModelBase.cs
/// <summary>Base de los ViewModels: evento de cambio, carga y manejo uniforme de errores.</summary>
public abstract class ViewModelBase : IDisposable
{
    private CancellationTokenSource? _cts;

    /// <summary>Se produce cuando el estado cambió y la página debe repintarse.</summary>
    public event EventHandler? Cambiado;

    /// <summary>Indica si hay una operación en curso (deshabilita botones y muestra el indicador).</summary>
    public bool Cargando { get; private set; }

    /// <summary>Último error entendible por el usuario, o <c>null</c>.</summary>
    public string? Error { get; private set; }

    /// <summary>Avisa a los suscriptores de que el estado cambió.</summary>
    protected void OnCambiado() => Cambiado?.Invoke(this, EventArgs.Empty);

    /// <summary>Ejecuta <paramref name="operacion"/> mostrando carga y traduciendo errores a mensajes.</summary>
    /// <param name="operacion">Método a ejecutar (se recibe como delegado).</param>
    protected async Task EjecutarAsync(Func<CancellationToken, Task> operacion)
    {
        _cts?.Cancel();                          // cancela una búsqueda anterior en curso
        _cts = new CancellationTokenSource();
        Cargando = true; Error = null; OnCambiado();
        try { await operacion(_cts.Token); }
        catch (OperationCanceledException) { /* otra operación reemplazó a esta: se ignora */ }
        catch (ApiException ex) { Error = ex.MensajeParaUsuario; }
        finally { Cargando = false; OnCambiado(); }
    }

    /// <summary>Cancela lo pendiente al cerrar la pantalla.</summary>
    public virtual void Dispose() => _cts?.Cancel();
}
```

```csharp
// Ui/Modulos/Clientes/ClientesViewModel.cs
/// <summary>Estado y lógica de la pantalla de clientes (sin referencias a Blazor).</summary>
public sealed class ClientesViewModel : ViewModelBase
{
    private readonly ClientesService _clientes;

    /// <summary>Texto escrito en el cuadro de búsqueda.</summary>
    public string TextoBusqueda { get; set; } = "";

    /// <summary>Página de resultados mostrada.</summary>
    public PaginaDto<ClienteDto>? Pagina { get; private set; }

    public ClientesViewModel(ClientesService clientes)
    {
        _clientes = clientes;
        _clientes.EntidadCambiada += Clientes_EntidadCambiada;   // refrescar tras un cambio
    }

    /// <summary>Consulta la API con el texto actual y guarda la página de resultados.</summary>
    public Task BuscarAsync() =>
        EjecutarAsync(async ct => Pagina = await _clientes.BuscarAsync(TextoBusqueda, ct));

    // async void SOLO porque es un manejador de evento de C#; el try/catch vive en EjecutarAsync
    private async void Clientes_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) =>
        await BuscarAsync();

    /// <summary>Baja de todas las suscripciones (par obligatorio del +=).</summary>
    public override void Dispose()
    {
        _clientes.EntidadCambiada -= Clientes_EntidadCambiada;
        base.Dispose();
    }
}
```

```razor
@* Ui/Modulos/Clientes/ClientesPagina.razor — solo marcado y enlace de eventos *@
@page "/admin/clientes"
@implements IDisposable
@inject ClientesViewModel Vm

<PaginaModulo Titulo="Clientes">
    <Filtros>
        <CampoTexto Etiqueta="Buscar" @bind-Valor="Vm.TextoBusqueda" AlPresionarEnter="Vm.BuscarAsync" />
        <Boton Variante="Primario" Cargando="Vm.Cargando" AlHacerClic="Vm.BuscarAsync">Buscar</Boton>
    </Filtros>
    <Tabla>
        <TablaDatos Items="Vm.Pagina?.Elementos" />
    </Tabla>
</PaginaModulo>

@code {
    protected override async Task OnInitializedAsync()
    {
        Vm.Cambiado += Vm_Cambiado;           // suscripción con +=
        await Vm.BuscarAsync();
    }

    // El evento puede llegar fuera del ciclo de render: se repinta con InvokeAsync
    private void Vm_Cambiado(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    /// <summary>Baja de la suscripción al cerrar la pestaña.</summary>
    public void Dispose() => Vm.Cambiado -= Vm_Cambiado;
}
```

Reglas:
* El ViewModel **no** referencia `Microsoft.AspNetCore.Components`; así se prueba sin navegador ni pantalla.
* La página **no** llama a servicios ni a la API; solo enlaza eventos HTML a métodos del ViewModel.
* Un ViewModel por página; se registra por DI y vive mientras su pestaña esté abierta.
* Si el componente hijo debe avisar al padre, usa `EventCallback<T>` (p. ej. `Paginador.PaginaCambiada`).

### 5.1 Estilo: tema y kit de componentes

* Se usan **solo** los componentes de `Ui/Comun/Componentes` (`Boton`, `CampoTexto`, `TablaDatos<T>`, `Paginador`, `PaginaModulo`, `Dialogo`…) y las variables del tema ([InterfazUsuario.md §8](../Especificaciones/InterfazUsuario.md)).
* Prohibido en una página: `style="color:#..."`, tamaños fijos en píxeles para distribuir la pantalla y CSS suelto; el CSS vive en `wwwroot/` o junto al componente (`.razor.css`).
* Un componente nuevo que se necesite en dos módulos pasa al kit.
* Accesibilidad: etiqueta en cada campo, `aria-label` en botones de icono, orden de tabulación lógico.

## 6. Delegados como Parámetro (material "Delegados")

El material pide usar delegados para **enviar métodos como parámetros**. Usos obligatorios del proyecto:

* `EjecutarAsync(Func<CancellationToken, Task> operacion)` de `ViewModelBase` (§5): carga y errores uniformes.
* Reglas de validación componibles:

```csharp
// Ui/Comun/Validacion.cs
/// <summary>Regla de validación: devuelve el mensaje de error o <c>null</c> si es válida.</summary>
public delegate string? ReglaDeValidacion<in T>(T valor);

/// <summary>Aplica todas las reglas y devuelve los errores encontrados.</summary>
public static IReadOnlyList<string> Validar<T>(T valor, params ReglaDeValidacion<T>[] reglas) =>
    reglas.Select(regla => regla(valor)).OfType<string>().ToList();
```

Uso: `Validar(cliente, c => string.IsNullOrWhiteSpace(c.Nombres) ? "Los nombres son obligatorios." : null, ...)`.

* Otros usos aceptados: ordenar en memoria (`Comparison<T>`), filtrar catálogos pequeños (`Func<T,bool>`), estrategias de cálculo (`delegate decimal CalculoDescuentoDelegate(decimal subtotal)`). **No** inventar delegados donde un método normal basta (KISS).

## 7. Cliente HTTP y Errores

* Una sola clase `ApiClient` (tipada, vía `IHttpClientFactory`); ninguna página ni ViewModel crea `HttpClient`.
* Añade el JWT de la sesión y traduce `Problem Details` a `ApiException` con `MensajeParaUsuario` (en español, sin detalles técnicos).
* `401` → publica `SesionExpirada` y se vuelve al login. `403` → "No tiene permiso para esta acción". `409` → "El registro cambió; recargue". `422` → mensaje de la regla.
* Los servicios de módulo **no** capturan excepciones: las dejan subir al ViewModel (`EjecutarAsync`).

## 8. Comentarios y Documentación del Código (RE-01)

El enunciado exige **comentar el código explicando qué hace o cómo funciona**. Regla del equipo:

1. **Comentario XML (`///`) en español** en **todo** tipo y miembro `public`/`protected`/`internal` de C# (clase, interfaz, método, propiedad, evento, delegado): `<summary>` siempre; `<param>`, `<returns>` y `<exception>` cuando aporten.
2. En los **`.razor`**, un comentario `@* … *@` al inicio que diga qué pantalla o componente es, y comentarios en los bloques `@code` no triviales y en los parámetros (`[Parameter]`, con `///`).
3. **Comentarios en línea** para explicar **cómo** funciona un paso no obvio y **por qué** se hace; no repetir lo que ya dice el código.
4. Cada **evento** documenta cuándo se produce; cada **delegado**, su firma; cada **`EventArgs`**, qué significa cada propiedad.
5. Cada **regla de negocio** en el dominio cita su origen (por ejemplo "[Pedidos.md §3.5]").
6. `GenerateDocumentationFile` está activo y `TreatWarningsAsErrors` convierte un comentario faltante (CS1591) en **error de compilación**.
7. Los archivos generados (migraciones de EF Core, código generado de Razor) se excluyen.

> Esto **sustituye** la preferencia general de [Principios.md](../Principios.md) de "comentar solo el porqué": por requisito de la materia, aquí **sí** se documenta también el *qué*.

## 9. Estilo y Antipatrones

**Nomenclatura**: ver [Estructura.md §3](../Estructura.md).

**Idioma**: dominio, interfaz y comentarios en **español**; términos técnicos estándar (`Repository`, `Handler`, `Options`, `Service`, `ViewModel`) en inglés. Un mismo concepto no se nombra en dos idiomas.

**Antipatrones (rechazar en revisión)**:

| ❌ No | ✅ Sí |
|---|---|
| Lógica de negocio o llamadas a la API en el `@code` de una página | La página llama al ViewModel; este usa el servicio |
| Disparar el evento antes de que la API responda | Disparar después de la respuesta exitosa |
| `+=` sin `-=` | Baja en `Dispose` |
| `StateHasChanged()` directo desde un evento | `await InvokeAsync(StateHasChanged)` |
| `if (evento != null) evento(...)` copiado a mano en cada sitio | `OnX` con `?.Invoke` |
| `async void` fuera de manejadores de eventos de C# | `async Task` |
| `new HttpClient()` en una página | `ApiClient` inyectado |
| Usar `HttpContext`, APIs de MAUI o `window` dentro de `Ui` | Una interfaz registrada por cada hospedaje |
| Guardar tokens o contraseñas en `localStorage`, archivos o el historial | Memoria (escritorio) o `ProtectedSessionStorage` (web) |
| Un diálogo bloqueante por cada éxito | Aviso no bloqueante (*snackbar*); diálogo solo para confirmaciones y errores importantes |
| Textos repetidos en cada página | Clase `Mensajes` centralizada |
| Capturar `Exception` y no hacer nada | Capturar lo específico y mostrar/registrar |
| Números o textos "mágicos" | Constantes o enumeraciones |
| Colores, fuentes o CSS suelto en una página | Tema y kit de componentes |
| Copiar un módulo entero y cambiar nombres sin entenderlo | Usar la estructura del módulo de referencia y comprender cada archivo |

## 10. Lista de Verificación de un Pull Request

- [ ] Compila sin advertencias; `./scripts/test-docker.sh` pasa en verde (no hay CI).
- [ ] Hay pruebas del dominio, del caso de uso, de la API y del ViewModel (según corresponda) y **pruebas de eventos** ([Testing.md §3](../Especificaciones/Testing.md)).
- [ ] Las páginas no tienen lógica; los ViewModels no referencian Blazor.
- [ ] Cada evento: delegado `XEventHandler`, `XEventArgs` inmutable, método `OnX` con `?.Invoke`, publicado **después** del éxito.
- [ ] Cada `+=` tiene su `-=` en `Dispose`; los eventos atendidos en componentes repintan con `InvokeAsync`.
- [ ] Sin `.Result`/`.Wait()`/`Thread.Sleep`; `async void` solo en manejadores de eventos de C# y con `try/catch`.
- [ ] Nada exclusivo de un hospedaje dentro de `Ui`.
- [ ] Comentarios XML en español en todo lo público y comentarios de "cómo/por qué" en lo no obvio.
- [ ] Permisos: botones según permiso **y** endpoint protegido con prueba 401/403.
- [ ] Sin SQL concatenado, sin secretos, sin cadena de conexión fuera de la API.
- [ ] Mensajes de usuario en español claro, centralizados; estilo solo desde el tema y el kit.
- [ ] La spec del módulo está actualizada y el commit/PR es revisable por sí solo.
- [ ] **El autor puede explicar cada línea** (la defensa lo exige).

## 11. Prompt Base para el Agente de IA

Copiar al iniciar la sesión (ajustando el módulo):

> Trabajas en el proyecto **Gestión de Pedidos (UFG)**: una única interfaz **Blazor** (biblioteca `GestionPedidos.Ui`, **.NET 8**) hospedada en una app de escritorio **.NET MAUI Blazor Hybrid** y en una **Blazor Web App**, que consumen una API ASP.NET Core + EF Core + SQL Server, todo documentado en `docs/`. Antes de escribir código, lee `AGENTS.md`, `docs/Equipo/PautasCodigoEventos.md` y la spec del módulo en `docs/Especificaciones/`. Sigue **exactamente** esas pautas: páginas sin lógica (Página + ViewModel con eventos), eventos con delegado `XEventHandler` + `XEventArgs` + método `OnX` con `?.Invoke`, publicados solo tras confirmar la operación, `+=` siempre con su `-=` en `Dispose`, repintado con `InvokeAsync(StateHasChanged)`, comentarios XML en español en todo lo público, consultas siempre parametrizadas, nada exclusivo de un hospedaje dentro de `Ui`, estilo solo con el tema y el kit de componentes, nunca cadena de conexión ni secretos fuera de la API. Imita la estructura del módulo **Usuarios**. Escribe primero las pruebas del comportamiento. Si algo no está cubierto en la documentación, **pregunta** en lugar de inventar. No hagas commits ni toques el repositorio remoto: deja los cambios en el árbol de trabajo para que yo los revise.
