using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Pedidos;

/// <summary>
/// Servicio de operaciones de pedidos contra la API ([Pedidos.md §6]).
/// Publica <see cref="EntidadCambiada"/> únicamente tras la confirmación exitosa de la API.
/// </summary>
public sealed class PedidosService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio de pedidos.</summary>
    public PedidosService(ApiClient api) => _api = api;

    /// <summary>Se produce tras crear, editar, cambiar el estado o dar de baja un pedido.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    /// <summary>Se produce cuando una operación de pedido es rechazada por inventario insuficiente.</summary>
    public event EventHandler<string>? StockInsuficiente;

    /// <summary>Busca pedidos con filtros y paginación.</summary>
    public Task<PaginaDto<PedidoResumenDto>> BuscarAsync(FiltroPedidosSolicitud filtro, CancellationToken ct)
    {
        var parametros = new List<string>();
        if (filtro.Pagina.HasValue) parametros.Add($"pagina={filtro.Pagina.Value}");
        if (filtro.TamanoPagina.HasValue) parametros.Add($"tamanoPagina={filtro.TamanoPagina.Value}");
        if (filtro.ClienteId.HasValue) parametros.Add($"clienteId={filtro.ClienteId.Value}");
        if (!string.IsNullOrWhiteSpace(filtro.Cliente)) parametros.Add($"cliente={Uri.EscapeDataString(filtro.Cliente.Trim())}");
        if (filtro.ProductoId.HasValue) parametros.Add($"productoId={filtro.ProductoId.Value}");
        if (!string.IsNullOrWhiteSpace(filtro.Producto)) parametros.Add($"producto={Uri.EscapeDataString(filtro.Producto.Trim())}");
        if (filtro.PrecioMin.HasValue) parametros.Add($"precioMin={filtro.PrecioMin.Value}");
        if (filtro.PrecioMax.HasValue) parametros.Add($"precioMax={filtro.PrecioMax.Value}");
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) parametros.Add($"estado={Uri.EscapeDataString(filtro.Estado)}");
        if (filtro.PedidoDesde.HasValue) parametros.Add($"pedidoDesde={Uri.EscapeDataString(filtro.PedidoDesde.Value.ToString("o"))}");
        if (filtro.PedidoHasta.HasValue) parametros.Add($"pedidoHasta={Uri.EscapeDataString(filtro.PedidoHasta.Value.ToString("o"))}");
        if (filtro.EntregaDesde.HasValue) parametros.Add($"entregaDesde={filtro.EntregaDesde.Value:yyyy-MM-dd}");
        if (filtro.EntregaHasta.HasValue) parametros.Add($"entregaHasta={filtro.EntregaHasta.Value:yyyy-MM-dd}");
        if (filtro.UsuarioId.HasValue) parametros.Add($"usuarioId={filtro.UsuarioId.Value}");
        if (filtro.IncluirInactivos.HasValue) parametros.Add($"incluirInactivos={filtro.IncluirInactivos.Value.ToString().ToLowerInvariant()}");

        var qs = parametros.Count > 0 ? "?" + string.Join('&', parametros) : "";
        return _api.GetAsync<PaginaDto<PedidoResumenDto>>($"/api/v1/pedidos{qs}", ct);
    }

    /// <summary>Obtiene el detalle completo de un pedido.</summary>
    public Task<PedidoDetalleDto> ObtenerAsync(int id, CancellationToken ct) =>
        _api.GetAsync<PedidoDetalleDto>($"/api/v1/pedidos/{id}", ct);

    /// <summary>Registra un nuevo pedido y notifica el cambio.</summary>
    public async Task<PedidoDetalleDto> CrearAsync(CrearPedidoSolicitud solicitud, CancellationToken ct)
    {
        try
        {
            var creado = await _api.PostAsync<PedidoDetalleDto>("/api/v1/pedidos", solicitud, ct);
            OnEntidadCambiada(creado, TipoCambio.Registrada);
            return creado;
        }
        catch (ApiException ex) when (ex.Codigo == CodigosError.StockInsuficiente)
        {
            StockInsuficiente?.Invoke(this, ex.MensajeParaUsuario);
            throw;
        }
    }

    /// <summary>Actualiza las líneas y datos de entrega de un pedido existente.</summary>
    public async Task<PedidoDetalleDto> ActualizarAsync(int id, ActualizarPedidoSolicitud solicitud, CancellationToken ct)
    {
        try
        {
            var actualizado = await _api.PutAsync<PedidoDetalleDto>($"/api/v1/pedidos/{id}", solicitud, ct);
            OnEntidadCambiada(actualizado, TipoCambio.Actualizada);
            return actualizado;
        }
        catch (ApiException ex) when (ex.Codigo == CodigosError.StockInsuficiente)
        {
            StockInsuficiente?.Invoke(this, ex.MensajeParaUsuario);
            throw;
        }
    }

    /// <summary>Cambia el estado de un pedido (Entregado o Cancelado).</summary>
    public async Task<PedidoDetalleDto> CambiarEstadoAsync(int id, CambiarEstadoPedidoSolicitud solicitud, CancellationToken ct)
    {
        var cambiado = await _api.PatchAsync<PedidoDetalleDto>($"/api/v1/pedidos/{id}/estado", solicitud, ct);
        OnEntidadCambiada(cambiado, TipoCambio.EstadoCambiado);
        return cambiado;
    }

    /// <summary>Aplica baja lógica al pedido.</summary>
    public async Task DesactivarAsync(int id, CancellationToken ct)
    {
        await _api.PatchAsync<object?>($"/api/v1/pedidos/{id}/desactivar", null, ct);
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Pedido,
            Id = id,
            Tipo = TipoCambio.EstadoCambiado,
            Descripcion = $"Pedido #{id}",
            Activo = false
        });
    }

    /// <summary>Obtiene la lista de clientes activos para los selectores de búsqueda o alta.</summary>
    public Task<List<ClienteSelectorDto>> ClientesSelectorAsync(string? q, CancellationToken ct)
    {
        var qs = !string.IsNullOrWhiteSpace(q) ? $"?q={Uri.EscapeDataString(q.Trim())}" : "";
        return _api.GetAsync<List<ClienteSelectorDto>>($"/api/v1/pedidos/clientes-selector{qs}", ct);
    }

    /// <summary>Obtiene la lista de productos activos para agregar líneas al pedido.</summary>
    public Task<List<ProductoSelectorDto>> ProductosSelectorAsync(string? q, CancellationToken ct)
    {
        var qs = !string.IsNullOrWhiteSpace(q) ? $"?q={Uri.EscapeDataString(q.Trim())}" : "";
        return _api.GetAsync<List<ProductoSelectorDto>>($"/api/v1/pedidos/productos-selector{qs}", ct);
    }

    private void OnEntidadCambiada(PedidoDetalleDto p, TipoCambio tipo) =>
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Pedido,
            Id = p.Id,
            Tipo = tipo,
            Descripcion = $"Pedido #{p.Id} ({p.Cliente})",
            Activo = p.Activo
        });
}
