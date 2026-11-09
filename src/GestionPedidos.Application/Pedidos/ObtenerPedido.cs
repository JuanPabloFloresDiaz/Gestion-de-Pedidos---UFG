using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Caso de uso: consulta individual de un pedido con sus líneas ([Pedidos.md §4]).</summary>
public sealed class ObtenerPedido
{
    private readonly IPedidoRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public ObtenerPedido(IPedidoRepository repo) => _repo = repo;

    /// <summary>Obtiene la cabecera y detalle de un pedido.</summary>
    /// <param name="id">Identificador del pedido.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<Resultado<PedidoDetalleDto>> EjecutarAsync(int id, CancellationToken ct)
    {
        var pedido = await _repo.ObtenerPorIdAsync(id, ct);
        return pedido is null
            ? Resultado<PedidoDetalleDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El pedido no existe.")
            : Resultado<PedidoDetalleDto>.Ok(pedido.ADetalleDto());
    }
}
