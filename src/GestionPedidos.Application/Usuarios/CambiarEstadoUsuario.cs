using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Usuarios;

/// <summary>
/// Caso de uso: baja lógica o reactivación de un usuario. Nadie puede darse de baja a sí mismo y siempre debe
/// quedar al menos un administrador activo.
/// </summary>
public sealed class CambiarEstadoUsuario
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public CambiarEstadoUsuario(IUsuarioRepository usuarios, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _usuarios = usuarios; _uow = uow; _eventos = eventos;
    }

    /// <summary>Cambia el estado del usuario.</summary>
    /// <param name="id">Usuario afectado.</param>
    /// <param name="activo">Estado deseado.</param>
    /// <param name="actorId">Administrador que lo cambia.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(id, ct);
        if (usuario is null)
            return Resultado<UsuarioDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El usuario no existe.");

        if (!activo)
        {
            if (usuario.Id == actorId)
                return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla, "No puede darse de baja a sí mismo.");
            if (usuario.Activo && usuario.Rol?.Nombre == NombresRol.Administrador
                && await _usuarios.ContarAdministradoresActivosAsync(ct) <= 1)
                return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla, "Debe quedar al menos un administrador activo.");
        }

        if (usuario.Activo != activo)
        {
            usuario.CambiarEstado(activo);
            await _uow.GuardarAsync(ct);
            _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
            {
                Entidad = "Usuario", EntidadId = usuario.Id, Tipo = TipoCambioServidor.EstadoCambiado, UsuarioId = actorId,
                Detalle = $"Usuario {usuario.NombreUsuario}: {(activo ? "reactivado" : "dado de baja")}",
            });
        }
        return Resultado<UsuarioDto>.Ok(usuario.ADto());
    }
}
