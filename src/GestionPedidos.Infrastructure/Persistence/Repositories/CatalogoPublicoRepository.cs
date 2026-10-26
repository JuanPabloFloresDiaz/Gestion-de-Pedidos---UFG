using GestionPedidos.Application.Tienda;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Lectura del catálogo público sobre EF Core: solo ve lo activo y no expone datos internos.</summary>
public sealed class CatalogoPublicoRepository : ICatalogoPublicoRepository
{
    /// <summary>Máximo de unidades que se puede pedir de un mismo producto de una vez.</summary>
    private const int MaximoPorPedido = 10;

    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public CatalogoPublicoRepository(GestionPedidosDbContext db) => _db = db;

    /// <summary>Productos que el público puede ver: activos y, si están clasificados, bajo una clasificación activa.</summary>
    private IQueryable<Producto> Visibles() => _db.Productos.AsNoTracking().Where(p => p.Activo
        && (p.SubcategoriaId == null || (p.Subcategoria!.Activo && p.Subcategoria.Categoria!.Activo)));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoriaTiendaDto>> ObtenerCategoriasAsync(CancellationToken ct)
    {
        var conteos = await Visibles().Where(p => p.SubcategoriaId != null && p.CantidadStock > 0)
            .GroupBy(p => p.SubcategoriaId!.Value).Select(g => new { Id = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Total, ct);

        var categorias = await _db.Categorias.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Nombre)
            .Select(c => new
            {
                c.Id, c.Nombre, c.Descripcion, c.Codigo, c.ImagenClave,
                Subs = c.Subcategorias.Where(s => s.Activo).OrderBy(s => s.Nombre).Select(s => new
                {
                    s.Id, s.Nombre,
                    Filtros = s.Filtros.Where(f => f.Activo).OrderBy(f => f.Nombre).Select(f => new
                    {
                        f.Id, f.Nombre,
                        Opciones = f.Opciones.Where(o => o.Activo).OrderBy(o => o.Valor).Select(o => new OpcionTiendaDto(o.Id, o.Valor)).ToList(),
                    }).ToList(),
                }).ToList(),
            }).ToListAsync(ct);

        return categorias.Select(c =>
        {
            var subs = c.Subs.Select(s => new SubcategoriaTiendaDto(s.Id, s.Nombre, conteos.GetValueOrDefault(s.Id),
                s.Filtros.Where(f => f.Opciones.Count > 0).Select(f => new FiltroTiendaDto(f.Id, f.Nombre, f.Opciones)).ToList())).ToList();
            return new CategoriaTiendaDto(c.Id, c.Nombre, c.Descripcion, subs.Sum(s => s.TotalProductos), subs, c.Codigo,
                c.ImagenClave is null ? null : $"/imagenes/categorias/{c.Codigo}");
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ProductoTiendaDto> Elementos, int Total)> BuscarAsync(ConsultaCatalogo c, int pagina, int tamano, CancellationToken ct)
    {
        var q = Visibles();
        if (!string.IsNullOrEmpty(c.Q)) q = q.Where(p => p.Nombre.Contains(c.Q) || p.Descripcion.Contains(c.Q));
        if (c.SubcategoriaId is { } sub) q = q.Where(p => p.SubcategoriaId == sub);
        else if (c.CategoriaId is { } cat) q = q.Where(p => p.Subcategoria!.CategoriaId == cat);
        if (c.PrecioMin is { } min) q = q.Where(p => p.PrecioVenta >= min);
        if (c.PrecioMax is { } max) q = q.Where(p => p.PrecioVenta <= max);
        if (c.SoloDisponibles) q = q.Where(p => p.CantidadStock > 0);

        // Y entre filtros distintos, O entre opciones del mismo filtro: se agrupan las opciones elegidas por filtro
        if (c.OpcionIds is { Count: > 0 } ids)
        {
            var porFiltro = await _db.OpcionesFiltro.AsNoTracking().Where(o => ids.Contains(o.Id))
                .Select(o => new { o.Id, o.FiltroId }).ToListAsync(ct);
            foreach (var grupo in porFiltro.GroupBy(o => o.FiltroId))
            {
                var delFiltro = grupo.Select(o => o.Id).ToList();
                q = q.Where(p => p.Opciones.Any(po => delFiltro.Contains(po.OpcionFiltroId)));
            }
        }

        q = c.Orden switch
        {
            "precio-asc" => q.OrderBy(p => p.PrecioVenta).ThenBy(p => p.Id),
            "precio-desc" => q.OrderByDescending(p => p.PrecioVenta).ThenBy(p => p.Id),
            "nombre" => q.OrderBy(p => p.Nombre).ThenBy(p => p.Id),
            _ => q.OrderByDescending(p => p.FechaIngreso).ThenByDescending(p => p.Id),
        };

        var total = await q.CountAsync(ct);
        var productos = await Proyectar(q.Skip((pagina - 1) * tamano).Take(tamano)).ToListAsync(ct);
        return (productos.Select(ADto).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task<ProductoTiendaDto?> ObtenerAsync(int id, CancellationToken ct)
    {
        var p = await Proyectar(Visibles().Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        return p is null ? null : ADto(p);
    }

    private sealed record Fila(string Codigo, string? ImagenClave, int Id, string Nombre, string Descripcion, decimal Precio, int? SubId, string? Sub, int? CatId, string? Cat,
        int Stock, bool MostrarStock, DateOnly Fecha, List<DetalleProductoTiendaDto> Detalles);

    private static IQueryable<Fila> Proyectar(IQueryable<Producto> q) => q.Select(p => new Fila(p.Codigo, p.ImagenClave, p.Id, p.Nombre, p.Descripcion, p.PrecioVenta,
        p.SubcategoriaId, p.Subcategoria!.Nombre, p.Subcategoria.CategoriaId, p.Subcategoria.Categoria!.Nombre, p.CantidadStock, p.MostrarStock,
        p.FechaIngreso,
        p.Opciones.OrderBy(o => o.Filtro!.Nombre)
            .Select(o => new DetalleProductoTiendaDto(o.FiltroId, o.Filtro!.Nombre, o.OpcionFiltroId, o.Opcion!.Valor)).ToList()));

    // El stock exacto solo se publica si el administrador lo decidió; si no, apenas "disponible" o no.
    private static ProductoTiendaDto ADto(Fila f) => new(f.Id, f.Nombre, f.Descripcion, f.Precio, f.CatId, f.Cat, f.SubId, f.Sub,
        f.Stock > 0, f.MostrarStock ? f.Stock : null, Math.Min(f.Stock, MaximoPorPedido), f.Fecha, f.Detalles, f.Codigo,
        f.ImagenClave is null ? null : $"/imagenes/productos/{f.Codigo}");
}
