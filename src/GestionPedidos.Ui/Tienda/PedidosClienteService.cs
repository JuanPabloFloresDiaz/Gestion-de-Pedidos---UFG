using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Pedidos del cliente contra la API (requiere sesión de cliente).</summary>
public sealed class PedidosClienteService
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public PedidosClienteService(ApiClient api) => _api = api;

    /// <summary>Datos de entrega del cliente (los de su registro).</summary>
    public Task<DatosEntregaDto> EntregaAsync(CancellationToken ct) => _api.GetAsync<DatosEntregaDto>("/api/v1/mis-pedidos/entrega", ct);

    /// <summary>Hace el pedido.</summary>
    public Task<PedidoClienteDto> CrearAsync(CrearPedidoClienteSolicitud s, CancellationToken ct) => _api.PostAsync<PedidoClienteDto>("/api/v1/mis-pedidos", s, ct);

    /// <summary>Pedidos del cliente, del más reciente al más antiguo.</summary>
    public Task<PaginaDto<PedidoClienteDto>> ListarAsync(int pagina, CancellationToken ct) =>
        _api.GetAsync<PaginaDto<PedidoClienteDto>>($"/api/v1/mis-pedidos?pagina={pagina}&tamanoPagina=5", ct);

    /// <summary>Cancela un pedido pendiente.</summary>
    public Task<PedidoClienteDto> CancelarAsync(int id, CancellationToken ct) =>
        _api.PatchAsync<PedidoClienteDto>($"/api/v1/mis-pedidos/{id}/cancelar", null, ct);
}
