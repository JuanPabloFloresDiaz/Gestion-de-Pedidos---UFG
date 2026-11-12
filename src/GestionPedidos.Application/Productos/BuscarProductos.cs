using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: búsqueda y listado paginado de productos para la administración.</summary>
public sealed class BuscarProductos
{
    private readonly IProductoRepository _productos;

    /// <summary>Crea el caso de uso con el repositorio.</summary>
    /// <param name="productos">Repositorio de productos.</param>
    public BuscarProductos(IProductoRepository productos) => _productos = productos;

    /// <summary>Ejecuta la búsqueda paginada.</summary>
    /// <param name="q">Texto de búsqueda.</param>
    /// <param name="categoriaId">Filtro por categoría.</param>
    /// <param name="subcategoriaId">Filtro por subcategoría.</param>
    /// <param name="activo">Filtro por estado.</param>
    /// <param name="pagina">Página.</param>
    /// <param name="tamanoPagina">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Página de DTOs de productos.</returns>
    public async Task<PaginaDto<ProductoDto>> EjecutarAsync(string? q, int? categoriaId, int? subcategoriaId, bool? activo, int? pagina, int? tamanoPagina, CancellationToken ct)
    {
        var (p, t) = Paginacion.Normalizar(pagina, tamanoPagina);
        var (elementos, total) = await _productos.BuscarAsync(q, categoriaId, subcategoriaId, activo, p, t, ct);
        var dtos = elementos.Select(x => x.ADto()).ToList();
        return new PaginaDto<ProductoDto>(dtos, total, p, t);
    }
}
