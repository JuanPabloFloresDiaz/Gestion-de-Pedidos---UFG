using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: alta de un nuevo producto en el catálogo.</summary>
public sealed class CrearProducto
{
    private readonly IProductoRepository _productos;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    /// <param name="productos">Repositorio de productos.</param>
    /// <param name="uow">Unidad de trabajo.</param>
    /// <param name="eventos">Publicador de eventos del servidor.</param>
    public CrearProducto(IProductoRepository productos, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _productos = productos;
        _uow = uow;
        _eventos = eventos;
    }

    /// <summary>Ejecuta el alta del producto.</summary>
    /// <param name="s">Datos de la solicitud.</param>
    /// <param name="actorId">Usuario administrador u operador que realiza la acción.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado con el producto creado o el error correspondiente.</returns>
    public async Task<Resultado<ProductoDto>> EjecutarAsync(CrearProductoSolicitud s, int actorId, CancellationToken ct)
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
