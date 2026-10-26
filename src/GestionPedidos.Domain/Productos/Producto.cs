using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Domain.Productos;

/// <summary>
/// Producto del catálogo. En la Fase 0.5 solo define la forma persistida; <c>DescontarStock</c> y
/// <c>ReponerStock</c> llegan en la Fase 2 ([Productos.md]).
/// </summary>
public class Producto : IAuditable
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>
    /// Código estable e irrepetible del producto (por ejemplo <c>ROP-CAM-001</c>). Es lo que liga la imagen guardada en el almacén
    /// de objetos con su producto: si la base se pierde y se restaura, la imagen se vuelve a enlazar por este código. Si no se
    /// indica, la base lo genera (<c>PRD-000123</c>).
    /// </summary>
    public string Codigo { get; set; } = default!;
    /// <summary>Clave de la imagen en el almacén de objetos (<c>productos/CODIGO.jpg</c>), o <c>null</c> si no tiene.</summary>
    public string? ImagenClave { get; set; }
    /// <summary>Nombre del producto.</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Descripción.</summary>
    public string Descripcion { get; set; } = "";
    /// <summary>Cantidad en stock (nunca negativa).</summary>
    public int CantidadStock { get; set; }
    /// <summary>Precio de venta.</summary>
    public decimal PrecioVenta { get; set; }
    /// <summary>Fecha de ingreso al inventario.</summary>
    public DateOnly FechaIngreso { get; set; }
    /// <summary>Subcategoría que lo clasifica; <c>null</c> = sin clasificar ([CatalogoProductos.md]).</summary>
    public int? SubcategoriaId { get; set; }
    /// <summary>Navegación a la subcategoría.</summary>
    public Subcategoria? Subcategoria { get; set; }
    /// <summary>Detalle del producto: una opción por filtro.</summary>
    public List<ProductoOpcion> Opciones { get; set; } = new();
    /// <summary>Si la tienda web muestra la cantidad exacta o solo Disponible/Agotado.</summary>
    public bool MostrarStock { get; set; }
    /// <summary>Baja lógica: <c>false</c> = inactivo.</summary>
    public bool Activo { get; set; } = true;
    /// <summary>Cuándo se dio de baja (eliminado suave); <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; set; }
    /// <summary>Descuenta existencias al vender. Rechaza cantidades no positivas y que el stock quede negativo.</summary>
    /// <param name="cantidad">Unidades vendidas (mayor que cero).</param>
    /// <exception cref="ArgumentOutOfRangeException">La cantidad no es positiva.</exception>
    /// <exception cref="InvalidOperationException">No hay suficientes existencias.</exception>
    public void DescontarStock(int cantidad)
    {
        if (cantidad <= 0) throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor que cero.");
        if (cantidad > CantidadStock) throw new InvalidOperationException($"Solo quedan {CantidadStock} unidades de {Nombre}.");
        CantidadStock -= cantidad;
    }

    /// <summary>Repone existencias (por ejemplo, al cancelar un pedido).</summary>
    /// <param name="cantidad">Unidades devueltas (mayor que cero).</param>
    /// <exception cref="ArgumentOutOfRangeException">La cantidad no es positiva.</exception>
    public void ReponerStock(int cantidad)
    {
        if (cantidad <= 0) throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor que cero.");
        CantidadStock += cantidad;
    }

    /// <summary>
    /// Clasifica el producto y reemplaza su detalle. Cambiar la subcategoría descarta el detalle anterior porque los
    /// filtros son otros; la validación de que cada opción pertenezca a un filtro de la subcategoría está en el caso de uso.
    /// </summary>
    /// <param name="subcategoriaId">Subcategoría, o <c>null</c> para dejarlo sin clasificar.</param>
    /// <param name="detalle">Pares (filtro, opción); a lo sumo uno por filtro.</param>
    /// <exception cref="ArgumentException">Hay dos opciones del mismo filtro o hay detalle sin subcategoría.</exception>
    public void EstablecerClasificacion(int? subcategoriaId, IEnumerable<(int FiltroId, int OpcionId)> detalle)
    {
        var lista = detalle.ToList();
        if (lista.Select(d => d.FiltroId).Distinct().Count() != lista.Count)
            throw new ArgumentException("Un producto solo puede tener una opción por filtro.", nameof(detalle));
        if (subcategoriaId is null && lista.Count > 0)
            throw new ArgumentException("No se puede describir un producto sin subcategoría.", nameof(detalle));

        SubcategoriaId = subcategoriaId;
        Opciones.Clear();
        foreach (var (filtroId, opcionId) in lista)
            Opciones.Add(new ProductoOpcion { FiltroId = filtroId, OpcionFiltroId = opcionId });
    }

    /// <summary>Creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }
    /// <summary>Última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }
    /// <summary>Concurrencia optimista.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
