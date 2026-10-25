using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Usuarios;

/// <summary>Caso de uso: listar roles con sus permisos.</summary>
public sealed class ListarRoles
{
    private readonly IRolRepository _roles;

    /// <summary>Crea el caso de uso.</summary>
    public ListarRoles(IRolRepository roles) => _roles = roles;

    /// <summary>Devuelve todos los roles.</summary>
    public Task<IReadOnlyList<RolDto>> EjecutarAsync(CancellationToken ct) => _roles.ListarAsync(ct);
}
