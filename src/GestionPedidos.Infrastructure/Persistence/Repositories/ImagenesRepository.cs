using GestionPedidos.Application.Imagenes;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Claves de las imágenes de productos y categorías en la base de datos.</summary>
public sealed class ImagenesRepository : IImagenesRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public ImagenesRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<DestinoDeImagen?> ObtenerDestinoAsync(TipoImagen tipo, int id, CancellationToken ct) => tipo == TipoImagen.Producto
        ? await _db.Productos.AsNoTracking().Where(p => p.Id == id).Select(p => new DestinoDeImagen(p.Id, p.Codigo, p.Nombre, p.ImagenClave)).FirstOrDefaultAsync(ct)
        : await _db.Categorias.AsNoTracking().Where(c => c.Id == id).Select(c => new DestinoDeImagen(c.Id, c.Codigo, c.Nombre, c.ImagenClave)).FirstOrDefaultAsync(ct);

    /// <inheritdoc/>
    public Task<string?> ObtenerClavePorCodigoAsync(TipoImagen tipo, string codigo, CancellationToken ct) => tipo == TipoImagen.Producto
        ? _db.Productos.AsNoTracking().Where(p => p.Codigo == codigo && p.Activo).Select(p => p.ImagenClave).FirstOrDefaultAsync(ct)
        : _db.Categorias.AsNoTracking().Where(c => c.Codigo == codigo && c.Activo).Select(c => c.ImagenClave).FirstOrDefaultAsync(ct);

    /// <inheritdoc/>
    public async Task EstablecerClaveAsync(TipoImagen tipo, int id, string? clave, CancellationToken ct)
    {
        if (tipo == TipoImagen.Producto) { var p = await _db.Productos.FindAsync(new object[] { id }, ct); if (p is not null) p.ImagenClave = clave; }
        else { var c = await _db.Categorias.FindAsync(new object[] { id }, ct); if (c is not null) c.ImagenClave = clave; }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DestinoDeImagen>> PorCodigoAsync(TipoImagen tipo, CancellationToken ct) => tipo == TipoImagen.Producto
        ? (await _db.Productos.AsNoTracking().Select(p => new DestinoDeImagen(p.Id, p.Codigo, p.Nombre, p.ImagenClave)).ToListAsync(ct)).ToDictionary(d => d.Codigo)
        : (await _db.Categorias.AsNoTracking().Select(c => new DestinoDeImagen(c.Id, c.Codigo, c.Nombre, c.ImagenClave)).ToListAsync(ct)).ToDictionary(d => d.Codigo);
}
