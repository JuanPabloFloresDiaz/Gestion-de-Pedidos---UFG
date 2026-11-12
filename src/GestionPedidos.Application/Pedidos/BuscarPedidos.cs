using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Caso de uso: búsqueda paginada y filtrada de pedidos ([Busquedas.md §2]).</summary>
public sealed class BuscarPedidos
{
    private readonly IPedidoRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public BuscarPedidos(IPedidoRepository repo) => _repo = repo;

    /// <summary>Realiza la búsqueda de pedidos aplicando los filtros y la paginación.</summary>
    /// <param name="filtro">Parámetros de filtrado y paginación.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<Resultado<PaginaDto<PedidoResumenDto>>> EjecutarAsync(FiltroPedidosSolicitud filtro, CancellationToken ct)
    {
        var (pagina, tamano) = Paginacion.Normalizar(filtro.Pagina, filtro.TamanoPagina ?? 20);
        var filtroNormalizado = filtro with { Pagina = pagina, TamanoPagina = tamano };

        var (elementos, total) = await _repo.BuscarAsync(filtroNormalizado, ct);
        return Resultado<PaginaDto<PedidoResumenDto>>.Ok(new PaginaDto<PedidoResumenDto>(elementos, total, pagina, tamano));
    }
}
