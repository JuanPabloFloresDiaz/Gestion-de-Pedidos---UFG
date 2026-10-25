using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Autenticacion;

/// <summary>
/// Caso de uso: registro de un cliente desde la web ([PortalWeb.md §3]). Crea el usuario (rol Cliente) y su
/// cliente en una sola transacción; si ya hay un cliente con ese correo sin cuenta, exige confirmar su
/// teléfono para enlazarlo en vez de duplicarlo.
/// </summary>
public sealed class RegistrarClienteWeb
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IRolRepository _roles;
    private readonly IRegistroClienteRepository _clientes;
    private readonly IHasherClave _hasher;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public RegistrarClienteWeb(IUsuarioRepository usuarios, IRolRepository roles, IRegistroClienteRepository clientes,
        IHasherClave hasher, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _usuarios = usuarios; _roles = roles; _clientes = clientes; _hasher = hasher; _uow = uow; _eventos = eventos;
    }

    /// <summary>Registra al cliente.</summary>
    public async Task<Resultado> EjecutarAsync(RegistroClienteSolicitud s, CancellationToken ct)
    {
        var error = Validaciones.Texto(s.Nombres, "Los nombres", 100)
            ?? Validaciones.Texto(s.Apellidos, "Los apellidos", 100)
            ?? Validaciones.Texto(s.Direccion, "La dirección", 250)
            ?? (Validaciones.NormalizarTelefono(s.Telefono).Length is >= 8 and <= 20 ? null : "El teléfono no es válido.")
            ?? (Validaciones.EsCorreo(s.Correo) ? null : "El correo no es válido.")
            ?? PoliticaClave.Validar(s.Clave, s.Correo, s.Correo);
        if (error is not null)
            return Resultado.Falla(TipoError.Validacion, CodigosError.Validacion, error);

        var correo = s.Correo.Trim().ToLowerInvariant();
        if (!await _clientes.ExisteCiudadAsync(s.CiudadId, ct))
            return Resultado.Falla(TipoError.Validacion, CodigosError.Validacion, "La ciudad no existe.");

        if (await _usuarios.ExisteCorreoAsync(correo, null, ct) || await _usuarios.ExisteNombreUsuarioAsync(correo, null, ct))
            return Resultado.Falla(TipoError.Conflicto, CodigosError.CorreoEnUso,
                "Ya existe una cuenta con este correo: inicia sesión o usa otro correo.");

        var rol = await _roles.ObtenerPorNombreAsync(NombresRol.Cliente, ct);
        if (rol is null)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla, "El rol Cliente no está configurado.");

        var existente = await _clientes.ObtenerClientePorCorreoAsync(correo, ct);
        if (existente is { UsuarioId: not null })
            return Resultado.Falla(TipoError.Conflicto, CodigosError.CorreoEnUso,
                "Ya existe una cuenta con este correo: inicia sesión o usa otro correo.");

        var enlaza = existente is not null;
        if (existente is not null)
        {
            if (string.IsNullOrWhiteSpace(s.TelefonoConfirmacion))
                return Resultado.Falla(TipoError.Conflicto, CodigosError.ClienteExistente,
                    "Encontramos un cliente registrado con este correo. Confirma tu teléfono para enlazar tu cuenta, o usa otro correo.");
            if (Validaciones.NormalizarTelefono(s.TelefonoConfirmacion) != Validaciones.NormalizarTelefono(existente.Telefono))
                return Resultado.Falla(TipoError.Validacion, CodigosError.TelefonoNoCoincide,
                    "El teléfono no coincide con el registrado.");
        }

        var usuario = Usuario.Crear(correo, $"{s.Nombres.Trim()} {s.Apellidos.Trim()}", correo,
            _hasher.Hashear(s.Clave), rol.Id, debeCambiarClave: false);
        _usuarios.Agregar(usuario);

        if (existente is not null) existente.Usuario = usuario;
        else
            _clientes.Agregar(new Cliente
            {
                Nombres = s.Nombres.Trim(), Apellidos = s.Apellidos.Trim(), Telefono = s.Telefono.Trim(),
                Direccion = s.Direccion.Trim(), Correo = correo, CiudadId = s.CiudadId, Usuario = usuario,
            });

        await _uow.GuardarAsync(ct);   // usuario y cliente en una sola transacción

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Usuario", EntidadId = usuario.Id, UsuarioId = usuario.Id,
            Tipo = enlaza ? TipoCambioServidor.CuentaEnlazada : TipoCambioServidor.Registrada,
            Detalle = enlaza ? $"Cuenta web enlazada con el cliente existente ({correo})" : $"Registro web de cliente ({correo})",
        });
        return Resultado.Ok();
    }
}
