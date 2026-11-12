using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Pedidos;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Caso de uso: cambio de estado de un pedido (Entregado o Cancelado) ([Pedidos.md §4]).</summary>
public sealed class CambiarEstadoPedido
{
    private readonly IPedidoRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso.</summary>
    public CambiarEstadoPedido(IPedidoRepository repo, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _repo = repo;
        _uow = uow;
        _eventos = eventos;
    }

    /// <summary>Ejecuta la transición de estado (reponiendo stock si se cancela).</summary>
    /// <param name="id">Identificador del pedido.</param>
    /// <param name="s">Nuevo estado y versión para concurrencia.</param>
    /// <param name="usuarioId">Usuario que ejecuta la acción.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<Resultado<PedidoDetalleDto>> EjecutarAsync(int id, CambiarEstadoPedidoSolicitud s, int usuarioId, CancellationToken ct)
    {
        if (s.RowVersion is not { Length: > 0 })
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Validacion, CodigosError.Validacion,
                "Falta la versión del pedido. Recargue la pantalla e intente de nuevo.");

        var pedido = await _repo.ObtenerPorIdAsync(id, ct);
        if (pedido is null)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El pedido no existe.");

        if (!pedido.Activo || pedido.Estado != EstadoPedido.Pendiente)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Regla, CodigosError.Regla,
                $"Solo se pueden cambiar de estado pedidos pendientes: este pedido está {pedido.Estado.ToLowerInvariant()}.");

        if (s.Estado != EstadoPedido.Entregado && s.Estado != EstadoPedido.Cancelado)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Validacion, CodigosError.Validacion,
                "El estado debe ser 'Entregado' o 'Cancelado'.");

        _repo.FijarVersionOriginal(pedido, s.RowVersion);

        if (s.Estado == EstadoPedido.Entregado)
        {
            pedido.MarcarEntregado();
        }
        else
        {
            pedido.Cancelar();
        }

        try
        {
            await _uow.GuardarAsync(ct);
        }
        catch (ConcurrenciaException)
        {
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "El pedido cambió mientras se procesaba su estado. Actualice la página e intente de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Pedido",
            EntidadId = pedido.Id,
            Tipo = TipoCambioServidor.EstadoCambiado,
            UsuarioId = usuarioId,
            Detalle = $"Pedido #{pedido.Id} marcado como {pedido.Estado.ToLowerInvariant()}" +
                      (pedido.Estado == EstadoPedido.Cancelado ? " (stock repuesto)" : "")
        });

        return Resultado<PedidoDetalleDto>.Ok(pedido.ADetalleDto());
    }
}
