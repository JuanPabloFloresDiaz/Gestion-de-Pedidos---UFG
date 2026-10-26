using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Usuarios;

/// <summary>
/// Caso de uso: eliminar DEFINITIVAMENTE un usuario (el <c>DELETE</c> del CRUD). Solo se puede si el usuario ya fue
/// dado de baja (eliminado suave): primero se da de baja y, si de verdad hace falta, después se elimina. No se
/// eliminan a sí mismos ni usuarios con pedidos a su nombre.
/// </summary>
public sealed class EliminarUsuario
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public EliminarUsuario(IUsuarioRepository usuarios, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _usuarios = usuarios; _uow = uow; _eventos = eventos;
    }

    /// <summary>Elimina el usuario de forma definitiva.</summary>
    /// <param name="id">Usuario a eliminar.</param>
    /// <param name="actorId">Administrador que lo elimina.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado> EjecutarAsync(int id, int actorId, CancellationToken ct)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(id, ct);
        if (usuario is null)
            return Resultado.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El usuario no existe.");
        if (usuario.Id == actorId)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla, "No puede eliminarse a sí mismo.");
        if (usuario.Activo)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla,
                "Primero debe dar de baja al usuario; solo se pueden eliminar definitivamente los usuarios dados de baja.");
        if (await _usuarios.TienePedidosAsync(usuario.Id, ct))
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla,
                "El usuario tiene pedidos registrados a su nombre: no se puede eliminar, déjelo dado de baja.");

        var nombre = usuario.NombreUsuario;
        _usuarios.Eliminar(usuario);
        await _uow.GuardarAsync(ct);

        // Solo tras confirmar el borrado se avisa a los suscriptores (historial). El detalle conserva el nombre porque
        // la fila del usuario ya no existe.
        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Usuario", EntidadId = id, Tipo = TipoCambioServidor.Eliminada, UsuarioId = actorId,
            Detalle = $"Usuario {nombre} eliminado definitivamente",
        });
        return Resultado.Ok();
    }
}
