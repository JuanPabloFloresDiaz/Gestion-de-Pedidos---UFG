using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Usuarios;

/// <summary>
/// Caso de uso: baja lógica (eliminado suave, con fecha en <c>EliminadoEn</c>) o reactivación de un usuario. Nadie puede darse de baja a sí mismo y siempre debe
/// quedar al menos un administrador activo.
/// </summary>
public sealed class CambiarEstadoUsuario
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Mínimo de caracteres de la justificación de una baja.</summary>
    public const int LongitudMinimaMotivo = 10;
    /// <summary>Máximo de caracteres de la justificación de una baja.</summary>
    public const int LongitudMaximaMotivo = 500;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public CambiarEstadoUsuario(IUsuarioRepository usuarios, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _usuarios = usuarios; _uow = uow; _eventos = eventos; _reloj = reloj;
    }

    /// <summary>Cambia el estado del usuario.</summary>
    /// <param name="id">Usuario afectado.</param>
    /// <param name="activo">Estado deseado.</param>
    /// <param name="actorId">Administrador que lo cambia.</param>
    /// <param name="ct">Cancelación.</param>
    /// <param name="motivo">Justificación de la baja: es obligatoria al dar de baja (de 10 a 500 caracteres) y se le envía por correo al usuario.</param>
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(int id, bool activo, int actorId, CancellationToken ct, string? motivo = null)
    {
        motivo = motivo?.Trim();
        if (!activo && (motivo is null || motivo.Length < LongitudMinimaMotivo || motivo.Length > LongitudMaximaMotivo))
            return Resultado<UsuarioDto>.Falla(TipoError.Validacion, CodigosError.Validacion,
                $"Escribe la justificación de la baja ({LongitudMinimaMotivo} a {LongitudMaximaMotivo} caracteres).");

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
            usuario.CambiarEstado(activo, _reloj.GetUtcNow());   // dar de baja = eliminado suave (guarda EliminadoEn)
            await _uow.GuardarAsync(ct);
            _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
            {
                Entidad = "Usuario", EntidadId = usuario.Id, Tipo = TipoCambioServidor.EstadoCambiado, UsuarioId = actorId,
                Detalle = activo
                    ? $"Usuario {usuario.NombreUsuario}: reactivado"
                    : $"Usuario {usuario.NombreUsuario}: dado de baja. Motivo: {motivo}",
            });
            if (!activo)
                _eventos.OnUsuarioDadoDeBaja(new UsuarioDadoDeBajaEventArgs
                {
                    UsuarioId = usuario.Id, NombreCompleto = usuario.NombreCompleto, Correo = usuario.Correo, Motivo = motivo!, ActorId = actorId,
                });
        }
        return Resultado<UsuarioDto>.Ok(usuario.ADto());
    }
}
