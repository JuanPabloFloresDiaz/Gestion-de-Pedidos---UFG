using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Pedidos;

/// <summary>Estados de negocio de un pedido ([Pedidos.md §3]).</summary>
public static class EstadoPedido
{
    /// <summary>Recién registrado.</summary>
    public const string Pendiente = "Pendiente";
    /// <summary>Entregado (final).</summary>
    public const string Entregado = "Entregado";
    /// <summary>Cancelado (final); repone el stock.</summary>
    public const string Cancelado = "Cancelado";
}

/// <summary>
/// Cabecera de un pedido. En la Fase 0.5 solo define la forma persistida; las reglas
/// (líneas, totales, transiciones, stock) llegan en la Fase 3 ([Pedidos.md]).
/// </summary>
public class Pedido : IAuditable
{
    /// <summary>Identificador (número de pedido).</summary>
    public int Id { get; set; }
    /// <summary>Dueño del pedido.</summary>
    public int ClienteId { get; set; }
    /// <summary>Navegación al cliente.</summary>
    public Cliente? Cliente { get; set; }
    /// <summary>Quién hizo el pedido.</summary>
    public int UsuarioId { get; set; }
    /// <summary>Navegación al usuario.</summary>
    public Usuario? Usuario { get; set; }
    /// <summary>Fecha del pedido (UTC), asignada por el sistema.</summary>
    public DateTimeOffset FechaPedido { get; set; }
    /// <summary>Fecha de entrega prevista.</summary>
    public DateOnly FechaEntrega { get; set; }
    /// <summary>Comentarios opcionales.</summary>
    public string? Comentarios { get; set; }
    /// <summary>Estado de negocio.</summary>
    public string Estado { get; set; } = EstadoPedido.Pendiente;
    /// <summary>Total del pedido.</summary>
    public decimal Total { get; set; }
    /// <summary>Baja lógica: <c>false</c> = inactivo.</summary>
    public bool Activo { get; set; } = true;
    /// <summary>Cuándo se dio de baja (eliminado suave); <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; set; }
    /// <summary>Creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }
    /// <summary>Última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }
    /// <summary>Concurrencia optimista.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    /// <summary>Líneas del pedido.</summary>
    public List<DetallePedido> Detalles { get; set; } = new();
}

/// <summary>Línea de un pedido: producto, cantidad y precio por unidad copiado al crearla.</summary>
public class DetallePedido
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Pedido al que pertenece.</summary>
    public int PedidoId { get; set; }
    /// <summary>Navegación al pedido.</summary>
    public Pedido? Pedido { get; set; }
    /// <summary>Producto vendido.</summary>
    public int ProductoId { get; set; }
    /// <summary>Navegación al producto.</summary>
    public Producto? Producto { get; set; }
    /// <summary>Cantidad (mayor que cero).</summary>
    public int Cantidad { get; set; }
    /// <summary>Precio por unidad en el momento del pedido.</summary>
    public decimal PrecioUnitario { get; set; }
    /// <summary>Subtotal calculado por la base de datos (<c>Cantidad × PrecioUnitario</c>).</summary>
    public decimal Subtotal { get; private set; }
}
