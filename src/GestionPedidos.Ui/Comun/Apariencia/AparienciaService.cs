using Microsoft.JSInterop;

namespace GestionPedidos.Ui.Comun.Apariencia;

/// <summary>Opción de la pantalla de apariencia.</summary>
/// <param name="Id">Identificador (se guarda y se usa como valor del atributo <c>data-modo</c> / <c>data-acento</c>).</param>
/// <param name="Nombre">Nombre visible.</param>
/// <param name="Descripcion">Texto de ayuda.</param>
public sealed record OpcionApariencia(string Id, string Nombre, string Descripcion);

/// <summary>Catálogo de modos y colores de acento disponibles (los colores concretos viven en <c>tema.css</c>).</summary>
public static class OpcionesDeApariencia
{
    /// <summary>Modo por defecto: sigue la configuración del sistema operativo.</summary>
    public const string ModoPorDefecto = "sistema";
    /// <summary>Acento por defecto.</summary>
    public const string AcentoPorDefecto = "azul";

    /// <summary>Modos de color.</summary>
    public static readonly IReadOnlyList<OpcionApariencia> Modos = new[]
    {
        new OpcionApariencia("claro", "Claro", "Fondo claro, ideal con mucha luz"),
        new OpcionApariencia("oscuro", "Oscuro", "Fondo oscuro, descansa la vista"),
        new OpcionApariencia("sistema", "Sistema", "Sigue la configuración de Windows o del navegador"),
    };

    /// <summary>Colores de acento (botones, enlaces y selección).</summary>
    public static readonly IReadOnlyList<OpcionApariencia> Acentos = new[]
    {
        new OpcionApariencia("azul", "Azul", "Clásico"),
        new OpcionApariencia("turquesa", "Turquesa", "Fresco"),
        new OpcionApariencia("verde", "Verde", "Natural"),
        new OpcionApariencia("ambar", "Ámbar", "Cálido"),
        new OpcionApariencia("rosa", "Rosa", "Vivo"),
        new OpcionApariencia("violeta", "Violeta", "Creativo"),
        new OpcionApariencia("grafito", "Grafito", "Sobrio"),
    };

    /// <summary>Indica si el modo existe.</summary>
    public static bool EsModoValido(string? id) => Modos.Any(m => m.Id == id);
    /// <summary>Indica si el acento existe.</summary>
    public static bool EsAcentoValido(string? id) => Acentos.Any(a => a.Id == id);
}

/// <summary>
/// Apariencia de la aplicación (modo y acento), igual en la web y en el escritorio: aplica la preferencia con un
/// pequeño script (<c>apariencia.js</c>) que la guarda en el almacenamiento del propio navegador o WebView.
/// </summary>
public sealed class AparienciaService
{
    private readonly IJSRuntime _js;

    /// <summary>Crea el servicio.</summary>
    public AparienciaService(IJSRuntime js) => _js = js;

    /// <summary>Se produce cuando cambia el modo o el acento.</summary>
    public event EventHandler? Cambiado;

    /// <summary>Modo actual (<c>claro</c>, <c>oscuro</c> o <c>sistema</c>).</summary>
    public string Modo { get; private set; } = OpcionesDeApariencia.ModoPorDefecto;
    /// <summary>Acento actual.</summary>
    public string Acento { get; private set; } = OpcionesDeApariencia.AcentoPorDefecto;
    /// <summary>Indica si ya se leyó la preferencia guardada.</summary>
    public bool Lista { get; private set; }

    /// <summary>Lee la preferencia guardada (solo se puede llamar después del primer render, cuando hay JavaScript).</summary>
    public async Task InicializarAsync()
    {
        if (Lista) return;
        try
        {
            var valores = await _js.InvokeAsync<string[]>("gpApariencia.leer");
            if (valores.Length == 2)
            {
                Modo = OpcionesDeApariencia.EsModoValido(valores[0]) ? valores[0] : OpcionesDeApariencia.ModoPorDefecto;
                Acento = OpcionesDeApariencia.EsAcentoValido(valores[1]) ? valores[1] : OpcionesDeApariencia.AcentoPorDefecto;
            }
        }
        catch (JSException) { /* sin almacenamiento disponible: se queda con los valores por defecto */ }
        catch (InvalidOperationException) { /* el circuito aún no puede usar JavaScript */ }
        Lista = true;
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cambia el modo de color y lo recuerda.</summary>
    public Task EstablecerModoAsync(string modo) =>
        OpcionesDeApariencia.EsModoValido(modo) ? AplicarAsync(modo, Acento) : Task.CompletedTask;

    /// <summary>Cambia el color de acento y lo recuerda.</summary>
    public Task EstablecerAcentoAsync(string acento) =>
        OpcionesDeApariencia.EsAcentoValido(acento) ? AplicarAsync(Modo, acento) : Task.CompletedTask;

    private async Task AplicarAsync(string modo, string acento)
    {
        Modo = modo;
        Acento = acento;
        try { await _js.InvokeVoidAsync("gpApariencia.establecer", modo, acento); }
        catch (JSException) { /* sin almacenamiento: el cambio rige solo mientras la ventana esté abierta */ }
        Cambiado?.Invoke(this, EventArgs.Empty);
    }
}
