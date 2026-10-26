using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;

namespace GestionPedidos.Ui.Modulos.Historial;

/// <summary>Consulta del historial de actividades (solo lectura).</summary>
public sealed class HistorialService
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public HistorialService(ApiClient api) => _api = api;

    /// <summary>Busca actividades con filtros y paginación.</summary>
    public Task<PaginaDto<ActividadDto>> BuscarAsync(string? q, string? entidad, string? accion, DateOnly? desde, DateOnly? hasta,
        int pagina, int tamano, CancellationToken ct)
    {
        var consulta = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamano}" };
        if (!string.IsNullOrWhiteSpace(q)) consulta.Add($"q={Uri.EscapeDataString(q.Trim())}");
        if (!string.IsNullOrWhiteSpace(entidad)) consulta.Add($"entidad={Uri.EscapeDataString(entidad)}");
        if (!string.IsNullOrWhiteSpace(accion)) consulta.Add($"accion={Uri.EscapeDataString(accion)}");
        if (desde is not null) consulta.Add($"desde={desde:yyyy-MM-dd}");
        if (hasta is not null) consulta.Add($"hasta={hasta:yyyy-MM-dd}");
        return _api.GetAsync<PaginaDto<ActividadDto>>($"/api/v1/actividades?{string.Join('&', consulta)}", ct);
    }

    /// <summary>Entidades y acciones que existen en el historial.</summary>
    public Task<FiltrosHistorialDto> FiltrosAsync(CancellationToken ct) =>
        _api.GetAsync<FiltrosHistorialDto>("/api/v1/actividades/filtros", ct);
}
