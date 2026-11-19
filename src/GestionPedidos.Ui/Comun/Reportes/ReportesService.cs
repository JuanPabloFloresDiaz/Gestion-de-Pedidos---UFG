using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using Microsoft.JSInterop;

namespace GestionPedidos.Ui.Comun.Reportes;

/// <summary>Formatos en los que la interfaz ofrece descargar un reporte.</summary>
public static class FormatosDeReporte
{
    /// <summary>PDF.</summary>
    public const string Pdf = "pdf";
    /// <summary>Excel (.xlsx).</summary>
    public const string Excel = "xlsx";
    /// <summary>CSV.</summary>
    public const string Csv = "csv";
}

/// <summary>
/// Pide un reporte a la API y se lo entrega a la persona como una descarga. La interfaz solo habla con la API
/// ([AGENTS.md]): el archivo se genera en el servidor, aquí solo se descarga.
/// </summary>
public sealed class ReportesService
{
    private readonly ApiClient _api;
    private readonly IJSRuntime _js;

    /// <summary>Crea el servicio.</summary>
    public ReportesService(ApiClient api, IJSRuntime js)
    {
        _api = api;
        _js = js;
    }

    /// <summary>Arma la ruta de la API de un reporte, con sus filtros (los valores vacíos se omiten).</summary>
    /// <param name="tipo">Identificador del reporte (por ejemplo <c>usuarios-por-rol</c>).</param>
    /// <param name="formato">pdf, xlsx o csv.</param>
    /// <param name="filtros">Filtros opcionales.</param>
    public static string Ruta(string tipo, string formato, IReadOnlyDictionary<string, string?>? filtros = null)
    {
        var partes = new List<string> { "formato=" + Uri.EscapeDataString(formato) };
        if (filtros is not null)
            partes.AddRange(filtros.Where(f => !string.IsNullOrWhiteSpace(f.Value))
                .Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value!)}"));
        return $"/api/v1/reportes/{Uri.EscapeDataString(tipo)}?" + string.Join('&', partes);
    }

    /// <summary>
    /// Arma los filtros que envía el botón de exportar: los de la pantalla, más la opción elegida (usuario o categoría) y el color de
    /// acento de la aplicación, para que el reporte salga con el mismo color que la persona está viendo.
    /// </summary>
    /// <param name="filtrosDePantalla">Filtros propios de la pantalla (fechas, agrupación…); puede ser <c>null</c>.</param>
    /// <param name="parametroOpcion">Nombre del filtro de la opción elegida (<c>usuarioId</c>, <c>categoriaId</c>); <c>null</c> si no hay lista.</param>
    /// <param name="opcionElegida">Identificador elegido; <c>null</c> = todas (no se envía nada).</param>
    /// <param name="acento">Acento de la aplicación (<c>azul</c>, <c>verde</c>…).</param>
    public static IReadOnlyDictionary<string, string?> FiltrosDelBoton(IReadOnlyDictionary<string, string?>? filtrosDePantalla,
        string? parametroOpcion, int? opcionElegida, string acento)
    {
        var filtros = new Dictionary<string, string?>(filtrosDePantalla ?? new Dictionary<string, string?>());
        if (parametroOpcion is not null && opcionElegida is { } id) filtros[parametroOpcion] = id.ToString();
        filtros["acento"] = acento;
        return filtros;
    }

    /// <summary>Opciones que se pueden elegir para acotar un reporte (usuarios o categorías); la lista la da la API.</summary>
    /// <param name="tipo">Identificador del reporte.</param>
    /// <param name="ct">Cancelación.</param>
    public Task<List<OpcionReporteDto>> OpcionesAsync(string tipo, CancellationToken ct = default) =>
        _api.GetAsync<List<OpcionReporteDto>>($"/api/v1/reportes/{Uri.EscapeDataString(tipo)}/opciones", ct);

    /// <summary>Descarga el reporte y se lo entrega al navegador o a la ventana de escritorio como archivo.</summary>
    /// <param name="tipo">Identificador del reporte.</param>
    /// <param name="formato">pdf, xlsx o csv.</param>
    /// <param name="filtros">Filtros opcionales.</param>
    /// <param name="ct">Cancelación.</param>
    /// <returns>El nombre del archivo descargado.</returns>
    public async Task<string> DescargarAsync(string tipo, string formato, IReadOnlyDictionary<string, string?>? filtros = null, CancellationToken ct = default)
    {
        var archivo = await _api.DescargarAsync(Ruta(tipo, formato, filtros), ct);
        await using var modulo = await _js.InvokeAsync<IJSObjectReference>("import", "./_content/GestionPedidos.Ui/js/descarga.js");
        await modulo.InvokeVoidAsync("descargar", archivo.Nombre, archivo.TipoContenido, archivo.Contenido);
        return archivo.Nombre;
    }
}
