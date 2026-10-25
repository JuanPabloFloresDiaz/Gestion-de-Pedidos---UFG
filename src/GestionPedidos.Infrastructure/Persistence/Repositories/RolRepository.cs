using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de roles y permisos sobre EF Core.</summary>
public sealed class RolRepository : IRolRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public RolRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id, r.Nombre, r.Descripcion,
                Permisos = r.Permisos.Select(p => p.Permiso!.Codigo).OrderBy(c => c).ToList(),
            }).ToListAsync(ct);
        return roles.Select(r => new RolDto(r.Id, r.Nombre, r.Descripcion, r.Permisos)).ToList();
    }

    /// <inheritdoc/>
    public Task<Rol?> ObtenerAsync(int id, CancellationToken ct) => _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc/>
    public Task<Rol?> ObtenerPorNombreAsync(string nombre, CancellationToken ct) =>
        _db.Roles.FirstOrDefaultAsync(r => r.Nombre == nombre, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> CodigosPermisosAsync(int rolId, CancellationToken ct) =>
        await _db.RolesPermisos.AsNoTracking().Where(rp => rp.RolId == rolId)
            .Select(rp => rp.Permiso!.Codigo).OrderBy(c => c).ToListAsync(ct);
}
