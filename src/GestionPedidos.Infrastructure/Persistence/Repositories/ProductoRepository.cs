using GestionPedidos.Application.Productos;
using GestionPedidos.Domain.Productos;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Implementación del repositorio de productos con Entity Framework Core.</summary>
public sealed class ProductoRepository : IProductoRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio con el contexto de base de datos.</summary>
    /// <param name="db">Contexto de base de datos.</param>
    public ProductoRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        _db.Productos
            .Include(p => p.Subcategoria)
                .ThenInclude(s => s!.Categoria)
            .Include(p => p.Opciones)
                .ThenInclude(o => o.Filtro)
            .Include(p => p.Opciones)
                .ThenInclude(o => o.Opcion)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Producto> Elementos, int Total)> BuscarAsync(string? q, int? categoriaId, int? subcategoriaId, bool? activo, int pagina, int tamano, CancellationToken ct)
    {
        IQueryable<Producto> query = _db.Productos.AsNoTracking()
            .Include(p => p.Subcategoria)
                .ThenInclude(s => s!.Categoria)
            .Include(p => p.Opciones)
                .ThenInclude(o => o.Filtro)
            .Include(p => p.Opciones)
                .ThenInclude(o => o.Opcion);

        if (activo is not null)
            query = query.Where(p => p.Activo == activo.Value);

        if (categoriaId is not null)
            query = query.Where(p => p.Subcategoria != null && p.Subcategoria.CategoriaId == categoriaId.Value);

        if (subcategoriaId is not null)
            query = query.Where(p => p.SubcategoriaId == subcategoriaId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = q.Trim();
            query = query.Where(p => p.Nombre.Contains(texto) || p.Descripcion.Contains(texto) || p.Codigo.Contains(texto));
        }

        var total = await query.CountAsync(ct);
        var elementos = await query
            .OrderBy(p => p.Nombre)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);

        return (elementos, total);
    }

    /// <inheritdoc/>
    public Task<bool> ExisteNombreAsync(string nombre, int? exceptoId, CancellationToken ct) =>
        _db.Productos.AnyAsync(p => p.Nombre == nombre && (exceptoId == null || p.Id != exceptoId.Value), ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(int FiltroId, int OpcionId)>> ObtenerOpcionesValidasAsync(int subcategoriaId, IEnumerable<int> opcionIds, CancellationToken ct)
    {
        var ids = opcionIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<(int, int)>();

        var validas = await _db.OpcionesFiltro
            .Where(o => ids.Contains(o.Id) && o.Filtro.SubcategoriaId == subcategoriaId)
            .Select(o => new { o.FiltroId, OpcionId = o.Id })
            .ToListAsync(ct);

        return validas.Select(x => (x.FiltroId, x.OpcionId)).ToList();
    }

    /// <inheritdoc/>
    public void FijarVersionOriginal(Producto producto, byte[] rowVersion) =>
        _db.Entry(producto).Property(p => p.RowVersion).OriginalValue = rowVersion;

    /// <inheritdoc/>
    public void Agregar(Producto producto) => _db.Productos.Add(producto);
}
