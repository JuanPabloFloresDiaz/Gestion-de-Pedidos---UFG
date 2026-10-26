namespace GestionPedidos.Contracts;

/// <summary>Una línea del pedido que el cliente quiere hacer.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="Cantidad">Unidades.</param>
public sealed record LineaPedidoSolicitud(int ProductoId, int Cantidad);

/// <summary>Pedido que hace un cliente desde la tienda. El precio nunca viaja: lo pone el servidor.</summary>
/// <param name="Lineas">Productos y cantidades.</param>
/// <param name="FechaEntrega">Fecha de entrega deseada (posterior a hoy).</param>
/// <param name="Comentarios">Comentarios opcionales.</param>
public sealed record CrearPedidoClienteSolicitud(IReadOnlyList<LineaPedidoSolicitud> Lineas, DateOnly FechaEntrega, string? Comentarios);

/// <summary>Línea de un pedido del cliente.</summary>
public sealed record LineaPedidoClienteDto(int ProductoId, string Producto, int Cantidad, decimal PrecioUnitario, decimal Subtotal);

/// <summary>Pedido del cliente con sus líneas.</summary>
public sealed record PedidoClienteDto(int Id, DateTimeOffset FechaPedido, DateOnly FechaEntrega, string Estado, decimal Total, string? Comentarios,
    bool PuedeCancelar, IReadOnlyList<LineaPedidoClienteDto> Lineas);

/// <summary>Datos de entrega del cliente que está por hacer un pedido (los de su registro).</summary>
public sealed record DatosEntregaDto(string Nombre, string Telefono, string Direccion, string Ciudad, string Departamento);
