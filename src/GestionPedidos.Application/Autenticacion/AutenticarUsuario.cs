using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Autenticacion;

/// <summary>
/// Caso de uso: iniciar sesión. Un solo mensaje genérico ante cualquier fallo (usuario inexistente, inactivo,
/// bloqueado o clave incorrecta) y el mismo trabajo de hash aunque el usuario no exista.
/// </summary>
public sealed class AutenticarUsuario
{
    private const string MensajeGenerico = "Usuario o contraseña incorrectos.";

    private readonly IUsuarioRepository _usuarios;
    private readonly IRolRepository _roles;
    private readonly IHasherClave _hasher;
    private readonly IEmisorTokens _tokens;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _reloj;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public AutenticarUsuario(IUsuarioRepository usuarios, IRolRepository roles, IHasherClave hasher,
        IEmisorTokens tokens, IUnitOfWork uow, TimeProvider reloj, EventosAplicacion eventos)
    {
        _usuarios = usuarios; _roles = roles; _hasher = hasher; _tokens = tokens; _uow = uow;
        _reloj = reloj; _eventos = eventos;
    }

    /// <summary>Intenta iniciar sesión.</summary>
    /// <param name="solicitud">Identificador y contraseña.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<SesionDto>> EjecutarAsync(LoginSolicitud solicitud, CancellationToken ct)
    {
        var identificador = solicitud.Identificador?.Trim() ?? "";
        var ahora = _reloj.GetUtcNow();
        var usuario = identificador.Length == 0 ? null : await _usuarios.ObtenerPorIdentificadorAsync(identificador, ct);

        if (usuario is null || !usuario.Activo)
        {
            _hasher.GastarTiempo();   // mismo costo que una verificación real
            _eventos.OnAccesoRegistrado(new AccesoRegistradoEventArgs
                { Identificador = identificador, UsuarioId = usuario?.Id, Exitoso = false });
            return Fallo();
        }

        if (usuario.EstaBloqueado(ahora))
        {
            _hasher.GastarTiempo();
            _eventos.OnAccesoRegistrado(new AccesoRegistradoEventArgs
                { Identificador = identificador, UsuarioId = usuario.Id, Exitoso = false });
            return Fallo();
        }

        if (!_hasher.Verificar(usuario.ClaveHash, solicitud.Clave ?? ""))
        {
            usuario.RegistrarFalloAcceso(ahora);
            await _uow.GuardarAsync(ct);
            _eventos.OnAccesoRegistrado(new AccesoRegistradoEventArgs
                { Identificador = identificador, UsuarioId = usuario.Id, Exitoso = false });
            return Fallo();
        }

        usuario.RegistrarAccesoExitoso(ahora);
        await _uow.GuardarAsync(ct);

        var permisos = await _roles.CodigosPermisosAsync(usuario.RolId, ct);
        var actual = usuario.AActual(permisos);
        var (token, expira) = _tokens.Emitir(actual, usuario.DebeCambiarClave);

        // Solo tras confirmar el acceso se avisa a los suscriptores (historial).
        _eventos.OnAccesoRegistrado(new AccesoRegistradoEventArgs
            { Identificador = identificador, UsuarioId = usuario.Id, Exitoso = true });
        return Resultado<SesionDto>.Ok(new SesionDto(token, expira, usuario.DebeCambiarClave, actual));
    }

    private static Resultado<SesionDto> Fallo() =>
        Resultado<SesionDto>.Falla(TipoError.NoAutenticado, CodigosError.CredencialesInvalidas, MensajeGenerico);
}
