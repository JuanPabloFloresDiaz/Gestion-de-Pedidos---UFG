using GestionPedidos.Application.Catalogo;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de la clasificación del catálogo (categorías, subcategorías, filtros y opciones) sobre EF Core.</summary>
public sealed class ClasificacionRepository : IClasificacionRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public ClasificacionRepository(GestionPedidosDbContext db) => _db = db;

    private static string Version(byte[] v) => Convert.ToBase64String(v);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoriaDto>> ObtenerArbolAsync(bool incluirInactivas, CancellationToken ct)
    {
        var categorias = await _db.Categorias.AsNoTracking().Where(c => incluirInactivas || c.Activo).OrderBy(c => c.Nombre)
            .Select(c => new
            {
                c.Id, c.Nombre, c.Descripcion, c.Activo, c.EliminadoEn, c.RowVersion,
                Subcategorias = c.Subcategorias.Where(s => incluirInactivas || s.Activo).OrderBy(s => s.Nombre)
                    .Select(s => new
                    {
                        s.Id, s.CategoriaId, s.Nombre, s.Descripcion, s.Activo, s.EliminadoEn, s.RowVersion,
                        TotalFiltros = s.Filtros.Count(f => incluirInactivas || f.Activo),
                        TotalProductos = _db.Productos.Count(p => p.SubcategoriaId == s.Id && p.Activo),
                    }).ToList(),
            }).ToListAsync(ct);

        return categorias.Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo, c.EliminadoEn, Version(c.RowVersion),
            c.Subcategorias.Select(s => new SubcategoriaDto(s.Id, s.CategoriaId, s.Nombre, s.Descripcion, s.Activo, s.EliminadoEn,
                Version(s.RowVersion), s.TotalFiltros, s.TotalProductos)).ToList())).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FiltroDto>> ObtenerFiltrosAsync(int subcategoriaId, bool incluirInactivos, CancellationToken ct)
    {
        var filtros = await _db.Filtros.AsNoTracking().Where(f => f.SubcategoriaId == subcategoriaId && (incluirInactivos || f.Activo))
            .OrderBy(f => f.Nombre)
            .Select(f => new
            {
                f.Id, f.SubcategoriaId, f.Nombre, f.Activo, f.EliminadoEn, f.RowVersion,
                Opciones = f.Opciones.Where(o => incluirInactivos || o.Activo).OrderBy(o => o.Valor)
                    .Select(o => new { o.Id, o.FiltroId, o.Valor, o.Activo, o.EliminadoEn, o.RowVersion }).ToList(),
            }).ToListAsync(ct);

        return filtros.Select(f => new FiltroDto(f.Id, f.SubcategoriaId, f.Nombre, f.Activo, f.EliminadoEn, Version(f.RowVersion),
            f.Opciones.Select(o => new OpcionFiltroDto(o.Id, o.FiltroId, o.Valor, o.Activo, o.EliminadoEn, Version(o.RowVersion))).ToList())).ToList();
    }

    /// <inheritdoc/>
    public Task<T?> ObtenerAsync<T>(int id, CancellationToken ct) where T : ElementoDeCatalogo
    {
        IQueryable<T> consulta = _db.Set<T>();
        // El padre se carga porque las reglas de reactivación lo consultan (no se reactiva bajo un padre inactivo).
        if (typeof(T) == typeof(Subcategoria)) consulta = consulta.Include("Categoria");
        else if (typeof(T) == typeof(Filtro)) consulta = consulta.Include("Subcategoria");
        else if (typeof(T) == typeof(OpcionFiltro)) consulta = consulta.Include("Filtro");
        return consulta.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    /// <inheritdoc/>
    public void Agregar<T>(T elemento) where T : ElementoDeCatalogo => _db.Set<T>().Add(elemento);

    /// <inheritdoc/>
    public void Eliminar<T>(T elemento) where T : ElementoDeCatalogo => _db.Set<T>().Remove(elemento);

    /// <inheritdoc/>
    public void FijarVersionOriginal(ElementoDeCatalogo elemento, byte[] rowVersion) =>
        _db.Entry(elemento).Property(e => e.RowVersion).OriginalValue = rowVersion;

    /// <inheritdoc/>
    public Task<bool> ExisteCategoriaAsync(string nombre, int? exceptoId, CancellationToken ct) =>
        _db.Categorias.AnyAsync(c => c.Nombre == nombre && c.Id != exceptoId, ct);

    /// <inheritdoc/>
    public Task<bool> ExisteSubcategoriaAsync(int categoriaId, string nombre, int? exceptoId, CancellationToken ct) =>
        _db.Subcategorias.AnyAsync(s => s.CategoriaId == categoriaId && s.Nombre == nombre && s.Id != exceptoId, ct);

    /// <inheritdoc/>
    public Task<bool> ExisteFiltroAsync(int subcategoriaId, string nombre, int? exceptoId, CancellationToken ct) =>
        _db.Filtros.AnyAsync(f => f.SubcategoriaId == subcategoriaId && f.Nombre == nombre && f.Id != exceptoId, ct);

    /// <inheritdoc/>
    public Task<bool> ExisteOpcionAsync(int filtroId, string valor, int? exceptoId, CancellationToken ct) =>
        _db.OpcionesFiltro.AnyAsync(o => o.FiltroId == filtroId && o.Valor == valor && o.Id != exceptoId, ct);

    /// <inheritdoc/>
    public Task<int> ContarSubcategoriasAsync(int categoriaId, bool soloActivas, CancellationToken ct) =>
        _db.Subcategorias.CountAsync(s => s.CategoriaId == categoriaId && (!soloActivas || s.Activo), ct);

    /// <inheritdoc/>
    public Task<int> ContarFiltrosAsync(int subcategoriaId, CancellationToken ct) =>
        _db.Filtros.CountAsync(f => f.SubcategoriaId == subcategoriaId, ct);

    /// <inheritdoc/>
    public Task<int> ContarProductosAsync(int subcategoriaId, bool soloActivos, CancellationToken ct) =>
        _db.Productos.CountAsync(p => p.SubcategoriaId == subcategoriaId && (!soloActivos || p.Activo), ct);

    /// <inheritdoc/>
    public Task<int> ContarOpcionesAsync(int filtroId, CancellationToken ct) =>
        _db.OpcionesFiltro.CountAsync(o => o.FiltroId == filtroId, ct);

    /// <inheritdoc/>
    public Task<int> ContarUsosDeOpcionAsync(int opcionId, CancellationToken ct) =>
        _db.ProductosOpciones.CountAsync(po => po.OpcionFiltroId == opcionId, ct);
}
