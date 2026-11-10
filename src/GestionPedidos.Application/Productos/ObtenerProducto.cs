using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Productos;

/// <summary>Caso de uso: obtener el detalle completo de un producto por su identificador.</summary>
public sealed class ObtenerProducto
{
    private readonly IProductoRepository _productos;

    /// <summary>Crea el caso de uso con el repositorio.</summary>
    /// <param name="productos">Repositorio de productos.</param>
    public ObtenerProducto(IProductoRepository productos) => _productos = productos;

    /// <summary>Ejecuta la consulta del producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Detalle del producto o error 404.</returns>
    public async Task<Resultado<ProductoDto>> EjecutarAsync(int id, CancellationToken ct)
    {
        var producto = await _productos.ObtenerPorIdAsync(id, ct);
        return producto is null
            ? Resultado<ProductoDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El producto no existe.")
            : Resultado<ProductoDto>.Ok(producto.ADto());
    }
}
