using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: dar de baja lógica o reactivar un producto.</summary>
public sealed class CambiarEstadoProducto
{
    private readonly IProductoRepository _productos;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    /// <param name="productos">Repositorio de productos.</param>
    /// <param name="uow">Unidad de trabajo.</param>
    /// <param name="eventos">Publicador de eventos del servidor.</param>
    /// <param name="reloj">Proveedor de tiempo.</param>
    public CambiarEstadoProducto(IProductoRepository productos, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _productos = productos;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Ejecuta el cambio de estado del producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="activo">Estado deseado (<c>false</c> = baja suave, <c>true</c> = reactivación).</param>
    /// <param name="actorId">Usuario que realiza la acción.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado con el producto resultante o el error correspondiente.</returns>
    public async Task<Resultado<ProductoDto>> EjecutarAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var producto = await _productos.ObtenerPorIdAsync(id, ct);
        if (producto is null)
            return Resultado<ProductoDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El producto no existe.");

        if (producto.Activo == activo)
            return Resultado<ProductoDto>.Ok(producto.ADto());

        producto.Activo = activo;
        producto.EliminadoEn = activo ? null : _reloj.GetUtcNow();

        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Producto",
            EntidadId = producto.Id,
            Tipo = TipoCambioServidor.EstadoCambiado,
            UsuarioId = actorId,
            Detalle = activo
                ? $"Producto {producto.Nombre} ({producto.Codigo}) reactivado"
                : $"Producto {producto.Nombre} ({producto.Codigo}) dado de baja",
        });

        return Resultado<ProductoDto>.Ok(producto.ADto());
    }
}
