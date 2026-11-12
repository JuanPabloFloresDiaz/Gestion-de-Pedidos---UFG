using GestionPedidos.Contracts;
using GestionPedidos.Domain.Pedidos;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Métodos de extensión para mapear entidades del agregado <see cref="Pedido"/> a DTOs.</summary>
public static class PedidoMapeo
{
    /// <summary>Mapea un <see cref="Pedido"/> a su representación detallada con líneas.</summary>
    /// <param name="p">Pedido a mapear.</param>
    public static PedidoDetalleDto ADetalleDto(this Pedido p) => new(
        p.Id,
        p.ClienteId,
        p.Cliente is not null ? $"{p.Cliente.Nombres} {p.Cliente.Apellidos}" : "",
        p.Cliente?.Telefono ?? "",
        p.Cliente?.Direccion ?? "",
        p.UsuarioId,
        p.Usuario?.NombreCompleto ?? "",
        p.FechaPedido,
        p.FechaEntrega,
        p.Estado,
        p.Total,
        p.Activo,
        p.Comentarios,
        p.RowVersion,
        p.Detalles.OrderBy(d => d.Producto?.Nombre).Select(d => new LineaPedidoDetalleDto(
            d.ProductoId,
            d.Producto?.Nombre ?? "",
            d.Producto?.Codigo ?? "",
            d.Cantidad,
            d.PrecioUnitario,
            d.Cantidad * d.PrecioUnitario)).ToList());

    /// <summary>Mapea un <see cref="Pedido"/> a su resumen para listados.</summary>
    /// <param name="p">Pedido a mapear.</param>
    public static PedidoResumenDto AResumenDto(this Pedido p) => new(
        p.Id,
        p.ClienteId,
        p.Cliente is not null ? $"{p.Cliente.Nombres} {p.Cliente.Apellidos}" : "",
        p.Cliente?.Apellidos ?? "",
        p.UsuarioId,
        p.Usuario?.NombreCompleto ?? "",
        p.FechaPedido,
        p.FechaEntrega,
        p.Estado,
        p.Total,
        p.Activo,
        p.Comentarios,
        p.Detalles.Count);
}
