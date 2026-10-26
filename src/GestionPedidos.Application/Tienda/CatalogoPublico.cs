using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Tienda;

/// <summary>Puerto de lectura del catálogo público (solo productos y clasificaciones activos).</summary>
public interface ICatalogoPublicoRepository
{
    /// <summary>Árbol de categorías → subcategorías → filtros → opciones, todo activo.</summary>
    Task<IReadOnlyList<CategoriaTiendaDto>> ObtenerCategoriasAsync(CancellationToken ct);
    /// <summary>Busca productos visibles con la consulta normalizada.</summary>
    Task<(IReadOnlyList<ProductoTiendaDto> Elementos, int Total)> BuscarAsync(ConsultaCatalogo consulta, int pagina, int tamano, CancellationToken ct);
    /// <summary>Un producto visible, o <c>null</c> si no existe o no se puede mostrar.</summary>
    Task<ProductoTiendaDto?> ObtenerAsync(int id, CancellationToken ct);
}

/// <summary>Casos de uso de la tienda pública para explorar el catálogo (sin sesión).</summary>
public sealed class ConsultarCatalogoPublico
{
    /// <summary>Órdenes admitidos; cualquier otro valor cae en <c>nuevos</c>.</summary>
    public static readonly IReadOnlySet<string> Ordenes = new HashSet<string> { "nuevos", "precio-asc", "precio-desc", "nombre" };
    /// <summary>Tamaño máximo de página del catálogo público.</summary>
    public const int TamanoMaximo = 48;

    private readonly ICatalogoPublicoRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public ConsultarCatalogoPublico(ICatalogoPublicoRepository repo) => _repo = repo;

    /// <summary>Menú de categorías con sus filtros.</summary>
    public async Task<IReadOnlyList<CategoriaTiendaDto>> CategoriasAsync(CancellationToken ct) => await _repo.ObtenerCategoriasAsync(ct);

    /// <summary>Busca productos; normaliza la entrada (texto recortado, precios coherentes, orden válido, página y tamaño).</summary>
    public async Task<Resultado<PaginaDto<ProductoTiendaDto>>> BuscarAsync(ConsultaCatalogo c, CancellationToken ct)
    {
        if (c.PrecioMin < 0 || c.PrecioMax < 0)
            return Resultado<PaginaDto<ProductoTiendaDto>>.Falla(TipoError.Validacion, CodigosError.Validacion, "El precio no puede ser negativo.");
        if (c.PrecioMin is { } min && c.PrecioMax is { } max && min > max)
            return Resultado<PaginaDto<ProductoTiendaDto>>.Falla(TipoError.Validacion, CodigosError.Validacion, "El precio mínimo no puede superar al máximo.");

        var texto = c.Q?.Trim();
        var normalizada = c with
        {
            Q = string.IsNullOrEmpty(texto) ? null : texto[..Math.Min(texto.Length, 100)],
            OpcionIds = (c.OpcionIds ?? Array.Empty<int>()).Distinct().Take(30).ToList(),
            Orden = c.Orden is not null && Ordenes.Contains(c.Orden) ? c.Orden : "nuevos",
        };
        var pagina = Math.Max(c.Pagina ?? 1, 1);
        var tamano = Math.Clamp(c.Tamano ?? 12, 1, TamanoMaximo);
        var (elementos, total) = await _repo.BuscarAsync(normalizada, pagina, tamano, ct);
        return Resultado<PaginaDto<ProductoTiendaDto>>.Ok(new PaginaDto<ProductoTiendaDto>(elementos, total, pagina, tamano));
    }

    /// <summary>Detalle de un producto visible.</summary>
    public async Task<Resultado<ProductoTiendaDto>> ObtenerAsync(int id, CancellationToken ct)
    {
        var p = await _repo.ObtenerAsync(id, ct);
        return p is null
            ? Resultado<ProductoTiendaDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El producto no existe.")
            : Resultado<ProductoTiendaDto>.Ok(p);
    }
}
