namespace GestionPedidos.Domain.Productos;

/// <summary>
/// Producto del catálogo. En la Fase 0.5 solo define la forma persistida; <c>DescontarStock</c> y
/// <c>ReponerStock</c> llegan en la Fase 2 ([Productos.md]).
/// </summary>
public class Producto : IAuditable
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
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
    /// <summary>Si la tienda web muestra la cantidad exacta o solo Disponible/Agotado.</summary>
    public bool MostrarStock { get; set; }
    /// <summary>Baja lógica: <c>false</c> = inactivo.</summary>
    public bool Activo { get; set; } = true;
    /// <summary>Cuándo se dio de baja (eliminado suave); <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; set; }
    /// <summary>Creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }
    /// <summary>Última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }
    /// <summary>Concurrencia optimista.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
