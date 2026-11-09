using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Puerto del repositorio de pedidos y sus líneas ([Pedidos.md §4]).</summary>
public interface IPedidoRepository
{
    /// <summary>Obtiene un pedido por su identificador incluyendo líneas y productos para edición.</summary>
    Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct);

    /// <summary>Obtiene un cliente por su identificador.</summary>
    Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken ct);

    /// <summary>Obtiene una lista de productos por sus identificadores con seguimiento de cambios.</summary>
    Task<IReadOnlyList<Producto>> ObtenerProductosAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>Agrega un nuevo pedido a la unidad de trabajo.</summary>
    void Agregar(Pedido pedido);

    /// <summary>Establece el token de concurrencia optimista original para detectar modificaciones concurrentes.</summary>
    void FijarVersionOriginal(Pedido pedido, byte[] rowVersion);

    /// <summary>Busca pedidos con filtros combinables y paginación mediante el procedimiento almacenado o consulta.</summary>
    Task<(IReadOnlyList<PedidoResumenDto> Elementos, int Total)> BuscarAsync(FiltroPedidosSolicitud filtro, CancellationToken ct);

    /// <summary>Lista clientes activos para selectores en formularios.</summary>
    Task<IReadOnlyList<ClienteSelectorDto>> ListarClientesSelectorAsync(string? q, CancellationToken ct);

    /// <summary>Lista productos activos con existencias para el carrito o detalle del pedido.</summary>
    Task<IReadOnlyList<ProductoSelectorDto>> ListarProductosSelectorAsync(string? q, CancellationToken ct);
}
