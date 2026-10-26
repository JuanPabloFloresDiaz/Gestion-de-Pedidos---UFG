using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Consultas del catálogo público contra la API (sin sesión).</summary>
public sealed class TiendaService
{
    private readonly ApiClient _api;
    private IReadOnlyList<CategoriaTiendaDto>? _categorias;

    /// <summary>Crea el servicio.</summary>
    public TiendaService(ApiClient api) => _api = api;

    /// <summary>Menú de categorías con filtros; se guarda en memoria mientras dure la visita (cambia muy poco).</summary>
    public async Task<IReadOnlyList<CategoriaTiendaDto>> CategoriasAsync(CancellationToken ct) =>
        _categorias ??= await _api.GetAsync<List<CategoriaTiendaDto>>("/api/v1/tienda/categorias", ct);

    /// <summary>Olvida las categorías guardadas (para recargar la pestaña).</summary>
    public void Olvidar() => _categorias = null;

    /// <summary>Busca productos.</summary>
    public Task<PaginaDto<ProductoTiendaDto>> BuscarAsync(FiltrosCatalogo f, CancellationToken ct) =>
        _api.GetAsync<PaginaDto<ProductoTiendaDto>>($"/api/v1/catalogo?{f.ToQuery(paraApi: true)}", ct);

    /// <summary>Un producto.</summary>
    public Task<ProductoTiendaDto> ObtenerAsync(int id, CancellationToken ct) => _api.GetAsync<ProductoTiendaDto>($"/api/v1/catalogo/{id}", ct);
}
