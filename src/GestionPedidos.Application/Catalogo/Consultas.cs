using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Application.Catalogo;

/// <summary>Caso de uso: consultar el árbol categorías → subcategorías (solo lectura).</summary>
public sealed class ObtenerArbolDeCatalogo
{
    private readonly IClasificacionRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public ObtenerArbolDeCatalogo(IClasificacionRepository repo) => _repo = repo;

    /// <summary>Devuelve el árbol; las inactivas solo si se piden.</summary>
    public async Task<IReadOnlyList<CategoriaDto>> EjecutarAsync(bool incluirInactivas, CancellationToken ct) =>
        await _repo.ObtenerArbolAsync(incluirInactivas, ct);
}

/// <summary>Caso de uso: consultar los filtros de una subcategoría con sus opciones (solo lectura).</summary>
public sealed class ObtenerFiltrosDeSubcategoria
{
    private readonly IClasificacionRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public ObtenerFiltrosDeSubcategoria(IClasificacionRepository repo) => _repo = repo;

    /// <summary>Devuelve los filtros, o 404 si la subcategoría no existe.</summary>
    public async Task<Resultado<IReadOnlyList<FiltroDto>>> EjecutarAsync(int subcategoriaId, bool incluirInactivos, CancellationToken ct)
    {
        if (await _repo.ObtenerAsync<Subcategoria>(subcategoriaId, ct) is null)
            return Resultado<IReadOnlyList<FiltroDto>>.Falla(ClasificacionComun.NoExiste("La subcategoría"));
        return Resultado<IReadOnlyList<FiltroDto>>.Ok(await _repo.ObtenerFiltrosAsync(subcategoriaId, incluirInactivos, ct));
    }
}
