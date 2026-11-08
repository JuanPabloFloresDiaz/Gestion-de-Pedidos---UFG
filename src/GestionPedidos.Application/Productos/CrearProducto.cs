using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: alta de un nuevo producto en el catálogo.</summary>
public sealed class CrearProducto
{private readonly IProductoRepository _productos;
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
    public async Task<Resultado<ProductoDto>> EjecutarAsync(CrearProductoSolicitud s, int actorId, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);
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

        if (s.SubcategoriaId is { } subId)
        {
            var opcionesValidas = s.OpcionIds is not null && s.OpcionIds.Count > 0
                ? await _productos.ObtenerOpcionesValidasAsync(subId, s.OpcionIds, ct)
                : Array.Empty<(int, int)>();

            producto.EstablecerClasificacion(subId, opcionesValidas);
        }

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
