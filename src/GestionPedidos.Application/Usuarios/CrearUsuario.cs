using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Usuarios;

/// <summary>Caso de uso: alta de un usuario del personal con clave temporal (deberá cambiarla).</summary>
public sealed class CrearUsuario
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IRolRepository _roles;
    private readonly IHasherClave _hasher;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public CrearUsuario(IUsuarioRepository usuarios, IRolRepository roles, IHasherClave hasher, IUnitOfWork uow,
        EventosAplicacion eventos)
    {
        _usuarios = usuarios; _roles = roles; _hasher = hasher; _uow = uow; _eventos = eventos;
    }

    /// <summary>Crea el usuario.</summary>
    /// <param name="s">Datos del alta.</param>
    /// <param name="actorId">Administrador que lo crea.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(CrearUsuarioSolicitud s, int actorId, CancellationToken ct)
    {
        var error = Validaciones.Texto(s.NombreUsuario, "El nombre de usuario", 50, 3)
            ?? Validaciones.Texto(s.NombreCompleto, "El nombre completo", 150)
            ?? (Validaciones.EsCorreo(s.Correo) ? null : "El correo no es válido.")
            ?? PoliticaClave.Validar(s.ClaveInicial, s.NombreUsuario, s.Correo);
        if (error is not null)
            return Resultado<UsuarioDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);

        var rol = await _roles.ObtenerAsync(s.RolId, ct);
        if (rol is null)
            return Resultado<UsuarioDto>.Falla(TipoError.Validacion, CodigosError.Validacion, "El rol no existe.");
        if (rol.Nombre == NombresRol.Cliente)
            return Resultado<UsuarioDto>.Falla(TipoError.Regla, CodigosError.Regla,
                "Los usuarios con rol Cliente se registran desde la tienda web.");

        if (await _usuarios.ExisteNombreUsuarioAsync(s.NombreUsuario.Trim(), null, ct))
            return Resultado<UsuarioDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "El nombre de usuario ya existe.");
        if (await _usuarios.ExisteCorreoAsync(s.Correo.Trim(), null, ct))
            return Resultado<UsuarioDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "El correo ya está registrado.");

        var usuario = Usuario.Crear(s.NombreUsuario, s.NombreCompleto, s.Correo, _hasher.Hashear(s.ClaveInicial),
            rol.Id, debeCambiarClave: true);
        _usuarios.Agregar(usuario);
        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Usuario", EntidadId = usuario.Id, Tipo = TipoCambioServidor.Registrada, UsuarioId = actorId,
            Detalle = $"Usuario {usuario.NombreUsuario} creado con rol {rol.Nombre}",
        });
        var creado = await _usuarios.ObtenerPorIdAsync(usuario.Id, ct);
        return Resultado<UsuarioDto>.Ok(creado!.ADto());
    }
}
