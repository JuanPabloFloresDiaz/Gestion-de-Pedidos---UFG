using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Pedidos;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Caso de uso: baja lógica de un pedido ([Pedidos.md §4]).</summary>
public sealed class DesactivarPedido
{
    private readonly IPedidoRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public DesactivarPedido(IPedidoRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Da de baja el pedido. Si estaba Pendiente, se cancela reponiendo existencias.</summary>
    /// <param name="id">Identificador del pedido.</param>
    /// <param name="usuarioId">Usuario que realiza la baja.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<Resultado> EjecutarAsync(int id, int usuarioId, CancellationToken ct)
    {
        var pedido = await _repo.ObtenerPorIdAsync(id, ct);
        if (pedido is null)
            return Resultado.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El pedido no existe.");

        if (!pedido.Activo)
            return Resultado.Ok();

        pedido.Desactivar(_reloj.GetUtcNow());

        try
        {
            await _uow.GuardarAsync(ct);
        }
        catch (ConcurrenciaException)
        {
            return Resultado.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "El pedido cambió mientras se daba de baja. Actualice la página e intente de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Pedido",
            EntidadId = pedido.Id,
            Tipo = TipoCambioServidor.EstadoCambiado,
            UsuarioId = usuarioId,
            Detalle = $"Pedido #{pedido.Id} dado de baja lógica"
        });

        return Resultado.Ok();
    }
}
