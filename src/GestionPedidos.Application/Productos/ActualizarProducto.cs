using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: actualización de los datos de un producto existente.</summary>
public sealed class ActualizarProducto
{
    private readonly IProductoRepository _productos;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    /// <param name="productos">Repositorio de productos.</param>
    /// <param name="uow">Unidad de trabajo.</param>
    /// <param name="eventos">Publicador de eventos del servidor.</param>
    public ActualizarProducto(IProductoRepository productos, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _productos = productos;
        _uow = uow;
        _eventos = eventos;
    }

    /// <summary>Ejecuta la actualización del producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="s">Datos actualizados.</param>
    /// <param name="actorId">Usuario que realiza la actualización.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado con el producto actualizado o el error correspondiente.</returns>
    public async Task<Resultado<ProductoDto>> EjecutarAsync(int id, ActualizarProductoSolicitud s, int actorId, CancellationToken ct)
    {
        var error = Validaciones.Texto(s.Nombre, "El nombre del producto", 150)
            ?? Validaciones.Texto(s.Descripcion, "La descripción", 500);
        if (error is not null)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);

        if (s.PrecioVenta < 0)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, "El precio de venta no puede ser negativo.");

        if (s.CantidadStock < 0)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, "El stock no puede ser negativo.");

        if (s.FechaIngreso > DateOnly.FromDateTime(DateTime.Today))
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, "La fecha de ingreso no puede ser futura.");

        var producto = await _productos.ObtenerPorIdAsync(id, ct);
        if (producto is null)
            return Resultado<ProductoDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El producto no existe.");

        if (!producto.Activo)
            return Resultado<ProductoDto>.Falla(TipoError.Regla, CodigosError.Regla, "No se puede editar un producto inactivo. Primero debe reactivarlo.");

        if (await _productos.ExisteNombreAsync(s.Nombre.Trim(), id, ct))
            return Resultado<ProductoDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Ya existe otro producto con ese nombre.");

        if (!string.IsNullOrEmpty(s.RowVersion))
            _productos.FijarVersionOriginal(producto, Convert.FromBase64String(s.RowVersion));

        producto.Nombre = s.Nombre.Trim();
        producto.Descripcion = s.Descripcion.Trim();
        producto.PrecioVenta = s.PrecioVenta;
        producto.CantidadStock = s.CantidadStock;
        producto.FechaIngreso = s.FechaIngreso;
        producto.MostrarStock = s.MostrarStock;

        if (s.SubcategoriaId is { } subId)
        {
            var opcionesValidas = s.OpcionIds is not null && s.OpcionIds.Count > 0
                ? await _productos.ObtenerOpcionesValidasAsync(subId, s.OpcionIds, ct)
                : Array.Empty<(int, int)>();

            producto.EstablecerClasificacion(subId, opcionesValidas);
        }
        else
        {
            producto.EstablecerClasificacion(null, Array.Empty<(int, int)>());
        }

        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Producto",
            EntidadId = producto.Id,
            Tipo = TipoCambioServidor.Actualizada,
            UsuarioId = actorId,
            Detalle = $"Producto {producto.Nombre} ({producto.Codigo}) actualizado",
        });

        var guardado = await _productos.ObtenerPorIdAsync(producto.Id, ct);
        return Resultado<ProductoDto>.Ok(guardado!.ADto());
    }
}
