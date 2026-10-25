using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Usuarios;

/// <summary>Caso de uso: obtener un usuario por identificador.</summary>
public sealed class ObtenerUsuario
{
    private readonly IUsuarioRepository _usuarios;

    /// <summary>Crea el caso de uso.</summary>
    public ObtenerUsuario(IUsuarioRepository usuarios) => _usuarios = usuarios;

    /// <summary>Devuelve el usuario o un error de no encontrado.</summary>
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(int id, CancellationToken ct)
    {
        var u = await _usuarios.ObtenerPorIdAsync(id, ct);
        return u is null
            ? Resultado<UsuarioDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El usuario no existe.")
            : Resultado<UsuarioDto>.Ok(u.ADto());
    }
}
