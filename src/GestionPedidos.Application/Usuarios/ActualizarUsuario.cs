using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Usuarios;

/// <summary>
/// Caso de uso: editar nombre, correo y rol de un usuario. Un usuario no cambia su propio rol y siempre
/// debe quedar al menos un administrador activo ([AutenticacionYPermisos.md §5]).
/// </summary>
public sealed class ActualizarUsuario
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IRolRepository _roles;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public ActualizarUsuario(IUsuarioRepository usuarios, IRolRepository roles, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _usuarios = usuarios; _roles = roles; _uow = uow; _eventos = eventos;
    }

    /// <summary>Actualiza el usuario.</summary>
    /// <param name="id">Usuario a editar.</param>
    /// <param name="s">Nuevos datos.</param>
    /// <param name="actorId">Administrador que edita.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(int id, ActualizarUsuarioSolicitud s, int actorId, CancellationToken ct)
    {
        var error = Validaciones.Texto(s.NombreCompleto, "El nombre completo", 150)
            ?? (Validaciones.EsCorreo(s.Correo) ? null : "El correo no es válido.");
        if (error is not null)
            return Resultado<UsuarioDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);

        var usuario = await _usuarios.ObtenerPorIdAsync(id, ct);
        if (usuario is null)
            return Resultado<UsuarioDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El usuario no existe.");
        if (!usuario.Activo)
            return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla, "El usuario está inactivo: reactívelo antes de editarlo.");

        var rol = await _roles.ObtenerAsync(s.RolId, ct);
        if (rol is null)
            return Resultado<UsuarioDto>.Falla(TipoError.Validacion, CodigosError.Validacion, "El rol no existe.");

        var cambiaRol = rol.Id != usuario.RolId;
        if (cambiaRol)
        {
            if (usuario.Id == actorId)
                return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla, "No puede cambiar su propio rol.");
            if (usuario.Rol?.Nombre == NombresRol.Administrador && await _usuarios.ContarAdministradoresActivosAsync(ct) <= 1)
                return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla, "Debe quedar al menos un administrador activo.");
            if (rol.Nombre == NombresRol.Cliente)
                return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla, "El rol Cliente es solo para la tienda web.");
        }

        if (await _usuarios.ExisteCorreoAsync(s.Correo.Trim(), usuario.Id, ct))
            return Resultado<UsuarioDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "El correo ya está registrado.");

        if (!string.IsNullOrEmpty(s.RowVersion))
            _usuarios.FijarVersionOriginal(usuario, Convert.FromBase64String(s.RowVersion));

        usuario.Actualizar(s.NombreCompleto, s.Correo, rol.Id);
        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException)
        {
            return Resultado<UsuarioDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "El usuario fue modificado por otra persona. Recargue e intente de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Usuario", EntidadId = usuario.Id, Tipo = TipoCambioServidor.Actualizada, UsuarioId = actorId,
            Detalle = cambiaRol ? $"Usuario {usuario.NombreUsuario} actualizado; nuevo rol {rol.Nombre}" : $"Usuario {usuario.NombreUsuario} actualizado",
        });
        var actualizado = await _usuarios.ObtenerPorIdAsync(usuario.Id, ct);
        return Resultado<UsuarioDto>.Ok(actualizado!.ADto());
    }
}
