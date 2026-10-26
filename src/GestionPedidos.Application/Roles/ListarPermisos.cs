using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Roles;

/// <summary>Caso de uso: listar el catálogo de permisos del sistema.</summary>
public sealed class ListarPermisos
{
    private readonly IRolRepository _roles;

    /// <summary>Crea el caso de uso.</summary>
    public ListarPermisos(IRolRepository roles) => _roles = roles;

    /// <summary>Devuelve todos los permisos.</summary>
    public Task<IReadOnlyList<PermisoDto>> EjecutarAsync(CancellationToken ct) => _roles.ListarPermisosAsync(ct);
}
