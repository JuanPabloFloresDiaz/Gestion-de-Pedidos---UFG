using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;

namespace GestionPedidos.Ui.Modulos.Catalogos;

/// <summary>Consulta de los catálogos geográficos (solo lectura).</summary>
public sealed class CatalogosService
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public CatalogosService(ApiClient api) => _api = api;

    /// <summary>Lista los 14 departamentos con su cantidad de ciudades.</summary>
    public Task<List<DepartamentoDto>> DepartamentosAsync(CancellationToken ct) =>
        _api.GetAsync<List<DepartamentoDto>>("/api/v1/departamentos", ct);

    /// <summary>Busca ciudades (distritos) con filtros y paginación.</summary>
    public Task<PaginaDto<CiudadDto>> CiudadesAsync(int? departamentoId, string? q, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamano}" };
        if (departamentoId is not null) consulta.Add($"departamentoId={departamentoId}");
        if (!string.IsNullOrWhiteSpace(q)) consulta.Add($"q={Uri.EscapeDataString(q.Trim())}");
        return _api.GetAsync<PaginaDto<CiudadDto>>($"/api/v1/ciudades?{string.Join('&', consulta)}", ct);
    }
}
