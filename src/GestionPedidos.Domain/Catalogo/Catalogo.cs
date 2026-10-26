using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Domain.Catalogo;

/// <summary>
/// Base de los elementos de clasificación del catálogo de productos ([CatalogoProductos.md]): todos comparten la
/// baja suave reversible (<c>Activo</c> + <c>EliminadoEn</c>) y la concurrencia optimista.
/// </summary>
public abstract class ElementoDeCatalogo : IAuditable
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Baja suave: <c>false</c> = inactivo.</summary>
    public bool Activo { get; set; } = true;
    /// <summary>Cuándo se dio de baja; <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; set; }
    /// <summary>Creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }
    /// <summary>Última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }
    /// <summary>Concurrencia optimista.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Da de baja (guarda la fecha) o reactiva (la limpia). Es el mismo comportamiento que <c>Usuario.CambiarEstado</c>:
    /// la baja nunca destruye información y siempre se puede revertir.
    /// </summary>
    /// <param name="activo">Estado deseado.</param>
    /// <param name="ahora">Momento del cambio.</param>
    public void CambiarEstado(bool activo, DateTimeOffset ahora)
    {
        if (activo) { Activo = true; EliminadoEn = null; }
        else
        {
            if (Activo || EliminadoEn is null) EliminadoEn = ahora;
            Activo = false;
        }
    }
}

/// <summary>Categoría: agrupa subcategorías (por ejemplo, <i>Ropa</i>).</summary>
public class Categoria : ElementoDeCatalogo
{
    /// <summary>
    /// Código estable e irrepetible de la categoría (por ejemplo <c>ROP</c>); liga su imagen del almacén de objetos y permite
    /// recuperarla si la base se restaura. Si no se indica, la base lo genera (<c>CAT-000007</c>).
    /// </summary>
    public string Codigo { get; set; } = default!;
    /// <summary>Clave de la imagen en el almacén de objetos (<c>categorias/CODIGO.jpg</c>), o <c>null</c> si no tiene.</summary>
    public string? ImagenClave { get; set; }
    /// <summary>Nombre (único).</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Descripción opcional.</summary>
    public string? Descripcion { get; set; }
    /// <summary>Subcategorías.</summary>
    public List<Subcategoria> Subcategorias { get; set; } = new();
}

/// <summary>Subcategoría: clasifica productos y define sus filtros (por ejemplo, <i>Camisas</i>).</summary>
public class Subcategoria : ElementoDeCatalogo
{
    /// <summary>Categoría a la que pertenece.</summary>
    public int CategoriaId { get; set; }
    /// <summary>Navegación a la categoría.</summary>
    public Categoria? Categoria { get; set; }
    /// <summary>Nombre (único dentro de la categoría).</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Descripción opcional.</summary>
    public string? Descripcion { get; set; }
    /// <summary>Filtros que describen a sus productos.</summary>
    public List<Filtro> Filtros { get; set; } = new();
}

/// <summary>Filtro: una característica de los productos de una subcategoría (por ejemplo, <i>Color</i>).</summary>
public class Filtro : ElementoDeCatalogo
{
    /// <summary>Subcategoría a la que pertenece.</summary>
    public int SubcategoriaId { get; set; }
    /// <summary>Navegación a la subcategoría.</summary>
    public Subcategoria? Subcategoria { get; set; }
    /// <summary>Nombre (único dentro de la subcategoría).</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Opciones disponibles.</summary>
    public List<OpcionFiltro> Opciones { get; set; } = new();
}

/// <summary>Opción de un filtro (por ejemplo, <i>Negro</i> en <i>Color</i>).</summary>
public class OpcionFiltro : ElementoDeCatalogo
{
    /// <summary>Filtro al que pertenece.</summary>
    public int FiltroId { get; set; }
    /// <summary>Navegación al filtro.</summary>
    public Filtro? Filtro { get; set; }
    /// <summary>Valor (único dentro del filtro).</summary>
    public string Valor { get; set; } = "";
}

/// <summary>
/// Detalle de un producto: la opción elegida para un filtro. La clave es (producto, filtro): un producto solo
/// tiene una opción por filtro, y la base garantiza que la opción sea del filtro indicado.
/// </summary>
public class ProductoOpcion
{
    /// <summary>Producto descrito.</summary>
    public int ProductoId { get; set; }
    /// <summary>Navegación al producto.</summary>
    public Producto? Producto { get; set; }
    /// <summary>Filtro.</summary>
    public int FiltroId { get; set; }
    /// <summary>Navegación al filtro.</summary>
    public Filtro? Filtro { get; set; }
    /// <summary>Opción elegida (del filtro indicado).</summary>
    public int OpcionFiltroId { get; set; }
    /// <summary>Navegación a la opción.</summary>
    public OpcionFiltro? Opcion { get; set; }
}
