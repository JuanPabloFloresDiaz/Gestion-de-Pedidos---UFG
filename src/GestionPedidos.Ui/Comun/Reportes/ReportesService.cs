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
