using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Clientes;

/// <summary>Caso de uso: dar de baja lógica o reactivar un cliente ([Clientes.md §3]).</summary>
public sealed class CambiarEstadoCliente
{
    private readonly IClienteRepository _clientes;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public CambiarEstadoCliente(IClienteRepository clientes, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _clientes = clientes;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Cambia el estado del cliente.</summary>
    public async Task<Resultado<ClienteDto>> EjecutarAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var cliente = await _clientes.ObtenerPorIdAsync(id, ct);
        if (cliente is null)
            return Resultado<ClienteDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El cliente no existe.");

        if (!activo && cliente.Activo)
        {
            if (await _clientes.TienePedidosPendientesAsync(cliente.Id, ct))
            {
                return Resultado<ClienteDto>.Falla(TipoError.Regla, CodigosError.Regla,
                    "No se puede dar de baja a un cliente que tiene pedidos pendientes.");
            }
        }

        if (cliente.Activo != activo)
        {
            cliente.CambiarEstado(activo, _reloj.GetUtcNow());
            try { await _uow.GuardarAsync(ct); }
            catch (ConcurrenciaException)
            {
                return Resultado<ClienteDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                    "El cliente fue modificado por otra persona. Recargue la pantalla e intente de nuevo.");
            }

            _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
            {
                Entidad = "Cliente",
                EntidadId = cliente.Id,
                Tipo = TipoCambioServidor.EstadoCambiado,
                UsuarioId = actorId,
                Detalle = $"Cliente {cliente.NombreCompleto}: {(activo ? "reactivado" : "dado de baja")}",
            });
        }

        return Resultado<ClienteDto>.Ok(cliente.ADto());
    }
}
