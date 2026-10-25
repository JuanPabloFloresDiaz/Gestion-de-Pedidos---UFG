using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Autenticacion;

/// <summary>Caso de uso: cambiar la contraseña propia (también cuando es obligatorio hacerlo).</summary>
public sealed class CambiarClave
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasherClave _hasher;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public CambiarClave(IUsuarioRepository usuarios, IHasherClave hasher, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _usuarios = usuarios; _hasher = hasher; _uow = uow; _eventos = eventos;
    }

    /// <summary>Cambia la contraseña del usuario autenticado.</summary>
    /// <param name="usuarioId">Usuario autenticado.</param>
    /// <param name="solicitud">Clave actual y nueva.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado> EjecutarAsync(int usuarioId, CambiarClaveSolicitud solicitud, CancellationToken ct)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, ct);
        if (usuario is null || !usuario.Activo)
            return Resultado.Falla(TipoError.NoAutenticado, CodigosError.CredencialesInvalidas, "Sesión no válida.");

        if (!_hasher.Verificar(usuario.ClaveHash, solicitud.ClaveActual ?? ""))
            return Resultado.Falla(TipoError.Validacion, CodigosError.ClaveInvalida, "La contraseña actual no es correcta.");

        var error = PoliticaClave.Validar(solicitud.ClaveNueva, usuario.NombreUsuario, usuario.Correo);
        if (error is null && solicitud.ClaveNueva == solicitud.ClaveActual)
            error = "La contraseña nueva debe ser distinta de la actual.";
        if (error is not null)
            return Resultado.Falla(TipoError.Validacion, CodigosError.ClaveInvalida, error);

        usuario.CambiarClave(_hasher.Hashear(solicitud.ClaveNueva));
        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Usuario", EntidadId = usuario.Id, Tipo = TipoCambioServidor.ClaveCambiada,
            UsuarioId = usuario.Id, Detalle = $"Cambió su contraseña ({usuario.NombreUsuario})",
        });
        return Resultado.Ok();
    }
}
