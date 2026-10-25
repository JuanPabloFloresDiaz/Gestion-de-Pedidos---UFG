using GestionPedidos.Application.Common;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Autenticacion;

/// <summary>Caso de uso: datos y permisos del usuario autenticado.</summary>
public sealed class ObtenerUsuarioActual
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IRolRepository _roles;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public ObtenerUsuarioActual(IUsuarioRepository usuarios, IRolRepository roles)
    {
        _usuarios = usuarios; _roles = roles;
    }

    /// <summary>Devuelve el usuario con sus permisos.</summary>
    public async Task<Resultado<UsuarioActualDto>> EjecutarAsync(int usuarioId, CancellationToken ct)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, ct);
        if (usuario is null || !usuario.Activo)
            return Resultado<UsuarioActualDto>.Falla(TipoError.NoAutenticado, CodigosError.CredencialesInvalidas, "Sesión no válida.");
        return Resultado<UsuarioActualDto>.Ok(usuario.AActual(await _roles.CodigosPermisosAsync(usuario.RolId, ct)));
    }
}
