using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Productos;

/// <summary>Funciones de mapeo para convertir entidades del dominio en DTOs de salida.</summary>
public static class Mapeos
{
    /// <summary>Convierte la entidad <see cref="Producto"/> en su DTO de administración.</summary>
    /// <param name="p">Entidad producto.</param>
    /// <returns>DTO listo para viajar por la API.</returns>
    public static ProductoDto ADto(this Producto p)
    {
        var detalles = p.Opciones?
            .Where(o => o.Filtro is not null && o.Opcion is not null)
            .Select(o => new DetalleProductoDto(o.FiltroId, o.Filtro!.Nombre, o.OpcionFiltroId, o.Opcion!.Valor))
            .ToList();

        var imagenUrl = !string.IsNullOrEmpty(p.Codigo) ? $"/imagenes/productos/{p.Codigo}" : null;

        return new ProductoDto(
            p.Id,
            p.Codigo,
            p.Nombre,
            p.Descripcion,
            p.PrecioVenta,
            p.CantidadStock,
            p.FechaIngreso,
            p.Subcategoria?.CategoriaId,
            p.Subcategoria?.Categoria?.Nombre,
            p.SubcategoriaId,
            p.Subcategoria?.Nombre,
            p.MostrarStock,
            p.Activo,
            p.ImagenClave,
            imagenUrl,
            Convert.ToBase64String(p.RowVersion),
            p.EliminadoEn,
            detalles);
    }
}
