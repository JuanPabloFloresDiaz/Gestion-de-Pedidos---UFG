using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Tienda;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: actualización de los datos de un producto existente.</summary>
public sealed class ActualizarProducto
{
    private readonly IProductoRepository _productos;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    /// <param name="productos">Repositorio de productos.</param>
    /// <param name="uow">Unidad de trabajo.</param>
    /// <param name="eventos">Publicador de eventos del servidor.</param>
    /// <param name="reloj">Proveedor de tiempo para pruebas de fecha de ingreso.</param>
    public ActualizarProducto(IProductoRepository productos, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _productos = productos;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Ejecuta la actualización del producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="s">Datos actualizados.</param>
    /// <param name="actorId">Usuario que realiza la actualización.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado con el producto actualizado o el error correspondiente.</returns>
    public async Task<Resultado<ProductoDto>> EjecutarAsync(int id, ActualizarProductoSolicitud s, int actorId, CancellationToken ct)
    {
        var hoy = RealizarPedidoDeCliente.Hoy(_reloj);   // fecha de El Salvador, no la del servidor
        var error = ValidacionesProducto.Validar(s.Nombre, s.Descripcion, s.PrecioVenta, s.CantidadStock, s.FechaIngreso, hoy);
        if (error is not null)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);

        var (errorVersion, version) = ValidacionesProducto.LeerVersion(s.RowVersion);
        if (errorVersion is not null)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, errorVersion);

        var (errorClasificacion, opciones) = await ValidacionesProducto.ResolverClasificacionAsync(_productos, s.SubcategoriaId, s.OpcionIds, ct);
        if (errorClasificacion is not null)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, errorClasificacion);

        var producto = await _productos.ObtenerPorIdAsync(id, ct);
        if (producto is null)
            return Resultado<ProductoDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El producto no existe.");

        if (!producto.Activo)
            return Resultado<ProductoDto>.Falla(TipoError.Regla, CodigosError.Regla, "No se puede editar un producto inactivo. Primero debe reactivarlo.");

        if (await _productos.ExisteNombreAsync(s.Nombre.Trim(), id, ct))
            return Resultado<ProductoDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Ya existe otro producto con ese nombre.");

        _productos.FijarVersionOriginal(producto, version);

        producto.Nombre = s.Nombre.Trim();
        producto.Descripcion = s.Descripcion.Trim();
        producto.PrecioVenta = s.PrecioVenta;
        producto.CantidadStock = s.CantidadStock;
        producto.FechaIngreso = s.FechaIngreso;
        producto.MostrarStock = s.MostrarStock;

        producto.EstablecerClasificacion(s.SubcategoriaId, opciones);

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
