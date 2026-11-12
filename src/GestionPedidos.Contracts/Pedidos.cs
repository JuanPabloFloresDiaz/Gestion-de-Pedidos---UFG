namespace GestionPedidos.Contracts;

/// <summary>Estados de negocio de un pedido ([Pedidos.md §2]).</summary>
public static class EstadosPedido
{
    /// <summary>Recién registrado.</summary>
    public const string Pendiente = "Pendiente";
    /// <summary>Entregado (final).</summary>
    public const string Entregado = "Entregado";
    /// <summary>Cancelado (final); repone el stock.</summary>
    public const string Cancelado = "Cancelado";
}

/// <summary>Resumen de un pedido para listados y búsquedas ([Busquedas.md §2]).</summary>
/// <param name="Id">Número de pedido.</param>
/// <param name="ClienteId">Identificador del cliente.</param>
/// <param name="Cliente">Nombre completo del cliente.</param>
/// <param name="ClienteApellidos">Apellidos del cliente (para ordenación/búsqueda).</param>
/// <param name="UsuarioId">Usuario que registró el pedido.</param>
/// <param name="Usuario">Nombre completo del usuario.</param>
/// <param name="FechaPedido">Fecha de registro (UTC).</param>
/// <param name="FechaEntrega">Fecha de entrega prevista.</param>
/// <param name="Estado">Estado de negocio: Pendiente, Entregado o Cancelado.</param>
/// <param name="Total">Monto total del pedido.</param>
/// <param name="Activo">Si está activo o dado de baja lógica.</param>
/// <param name="Comentarios">Comentarios opcionales.</param>
/// <param name="Lineas">Cantidad de líneas de producto que contiene.</param>
public sealed record PedidoResumenDto(
    int Id,
    int ClienteId,
    string Cliente,
    string ClienteApellidos,
    int UsuarioId,
    string Usuario,
    DateTimeOffset FechaPedido,
    DateOnly FechaEntrega,
    string Estado,
    decimal Total,
    bool Activo,
    string? Comentarios,
    int Lineas);

/// <summary>Detalle de una línea de producto en la consulta de un pedido.</summary>
/// <param name="ProductoId">Identificador del producto.</param>
/// <param name="Producto">Nombre del producto.</param>
/// <param name="Codigo">Código único del producto (PRD-...).</param>
/// <param name="Cantidad">Cantidad de unidades.</param>
/// <param name="PrecioUnitario">Precio por unidad copiado al crear la línea.</param>
/// <param name="Subtotal">Subtotal calculado (<c>Cantidad × PrecioUnitario</c>).</param>
public sealed record LineaPedidoDetalleDto(
    int ProductoId,
    string Producto,
    string Codigo,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);

/// <summary>Información completa de un pedido con cabecera y todas sus líneas ([Pedidos.md §2]).</summary>
/// <param name="Id">Número de pedido.</param>
/// <param name="ClienteId">Identificador del cliente.</param>
/// <param name="Cliente">Nombre completo del cliente.</param>
/// <param name="TelefonoCliente">Teléfono del cliente.</param>
/// <param name="DireccionCliente">Dirección de entrega.</param>
/// <param name="UsuarioId">Identificador del usuario creador.</param>
/// <param name="Usuario">Nombre del usuario creador.</param>
/// <param name="FechaPedido">Fecha de creación (UTC).</param>
/// <param name="FechaEntrega">Fecha prevista de entrega.</param>
/// <param name="Estado">Estado actual (Pendiente, Entregado, Cancelado).</param>
/// <param name="Total">Total calculado del pedido.</param>
/// <param name="Activo">Estado de baja lógica.</param>
/// <param name="Comentarios">Observaciones o comentarios.</param>
/// <param name="RowVersion">Token de concurrencia optimista.</param>
/// <param name="Lineas">Líneas de productos del pedido.</param>
public sealed record PedidoDetalleDto(
    int Id,
    int ClienteId,
    string Cliente,
    string TelefonoCliente,
    string DireccionCliente,
    int UsuarioId,
    string Usuario,
    DateTimeOffset FechaPedido,
    DateOnly FechaEntrega,
    string Estado,
    decimal Total,
    bool Activo,
    string? Comentarios,
    byte[] RowVersion,
    IReadOnlyList<LineaPedidoDetalleDto> Lineas);

/// <summary>Solicitud para registrar un pedido administrativo ([Pedidos.md §5]).</summary>
/// <param name="ClienteId">Cliente dueño del pedido.</param>
/// <param name="FechaEntrega">Fecha prevista de entrega.</param>
/// <param name="Comentarios">Comentarios u observaciones.</param>
/// <param name="Lineas">Líneas de producto y cantidad (precios fijados por el servidor).</param>
public sealed record CrearPedidoSolicitud(
    int ClienteId,
    DateOnly FechaEntrega,
    string? Comentarios,
    IReadOnlyList<LineaPedidoSolicitud> Lineas);

/// <summary>Solicitud para actualizar un pedido existente (solo en estado Pendiente).</summary>
/// <param name="FechaEntrega">Nueva fecha de entrega.</param>
/// <param name="Comentarios">Nuevos comentarios.</param>
/// <param name="Lineas">Nueva colección de líneas de productos.</param>
/// <param name="RowVersion">Token de concurrencia optimista.</param>
public sealed record ActualizarPedidoSolicitud(
    DateOnly FechaEntrega,
    string? Comentarios,
    IReadOnlyList<LineaPedidoSolicitud> Lineas,
    byte[] RowVersion);

/// <summary>Solicitud para cambiar el estado de un pedido (Entregado o Cancelado).</summary>
/// <param name="Estado">Nuevo estado solicitado.</param>
/// <param name="RowVersion">Token de concurrencia optimista.</param>
public sealed record CambiarEstadoPedidoSolicitud(string Estado, byte[] RowVersion);

/// <summary>Elemento ligero de cliente para selectores en formularios de pedidos.</summary>
/// <param name="Id">Identificador del cliente.</param>
/// <param name="Nombre">Nombre completo.</param>
/// <param name="Telefono">Teléfono de contacto.</param>
/// <param name="Direccion">Dirección de entrega.</param>
public sealed record ClienteSelectorDto(int Id, string Nombre, string Telefono, string Direccion);

/// <summary>Elemento ligero de producto con existencias para el formulario de pedidos.</summary>
/// <param name="Id">Identificador del producto.</param>
/// <param name="Codigo">Código de catálogo.</param>
/// <param name="Nombre">Nombre del producto.</param>
/// <param name="PrecioVenta">Precio vigente.</param>
/// <param name="CantidadStock">Existencias disponibles.</param>
public sealed record ProductoSelectorDto(int Id, string Codigo, string Nombre, decimal PrecioVenta, int CantidadStock);

/// <summary>Filtros de búsqueda para pedidos ([Busquedas.md §2]).</summary>
public sealed record FiltroPedidosSolicitud(
    int? ClienteId = null,
    string? Cliente = null,
    int? ProductoId = null,
    string? Producto = null,
    decimal? PrecioMin = null,
    decimal? PrecioMax = null,
    string? Estado = null,
    DateTimeOffset? PedidoDesde = null,
    DateTimeOffset? PedidoHasta = null,
    DateOnly? EntregaDesde = null,
    DateOnly? EntregaHasta = null,
    int? UsuarioId = null,
    bool? IncluirInactivos = null,
    int? Pagina = null,
    int? TamanoPagina = null);
