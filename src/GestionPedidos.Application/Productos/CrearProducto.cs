using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Tienda;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: alta de un nuevo producto en el catálogo.</summary>
public sealed class CrearProducto
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
    public CrearProducto(IProductoRepository productos, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _productos = productos;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Ejecuta el alta del producto.</summary>
    /// <param name="s">Datos de la solicitud.</param>
    /// <param name="actorId">Usuario administrador u operador que realiza la acción.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado con el producto creado o el error correspondiente.</returns>
    public async Task<Resultado<ProductoDto>> EjecutarAsync(CrearProductoSolicitud s, int actorId, CancellationToken ct)
    {
        var hoy = RealizarPedidoDeCliente.Hoy(_reloj);   // fecha de El Salvador, no la del servidor
        var error = ValidacionesProducto.Validar(s.Nombre, s.Descripcion, s.PrecioVenta, s.CantidadStock, s.FechaIngreso, hoy);
        if (error is not null)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);

        if (await _productos.ExisteNombreAsync(s.Nombre.Trim(), null, ct))
            return Resultado<ProductoDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Ya existe un producto con ese nombre.");

        var producto = new Producto
        {
            Nombre = s.Nombre.Trim(),
            Descripcion = s.Descripcion.Trim(),
            PrecioVenta = s.PrecioVenta,
            CantidadStock = s.CantidadStock,
            FechaIngreso = s.FechaIngreso,
            MostrarStock = s.MostrarStock,
            Activo = true,
        };

        if (!string.IsNullOrWhiteSpace(s.Codigo))
            producto.Codigo = s.Codigo.Trim().ToUpperInvariant();

        var (errorClasificacion, opciones) = await ValidacionesProducto.ResolverClasificacionAsync(_productos, s.SubcategoriaId, s.OpcionIds, ct);
        if (errorClasificacion is not null)
            return Resultado<ProductoDto>.Falla(TipoError.Validacion, CodigosError.Validacion, errorClasificacion);

        if (s.SubcategoriaId is not null)
            producto.EstablecerClasificacion(s.SubcategoriaId, opciones);

        _productos.Agregar(producto);
        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Producto",
            EntidadId = producto.Id,
            Tipo = TipoCambioServidor.Registrada,
            UsuarioId = actorId,
            Detalle = $"Producto {producto.Nombre} ({producto.Codigo}) registrado con {producto.CantidadStock} unidades",
        });

        var guardado = await _productos.ObtenerPorIdAsync(producto.Id, ct);
        return Resultado<ProductoDto>.Ok(guardado!.ADto());
    }
}
