using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Application.Catalogo;

/// <summary>
/// Puerto de la clasificación del catálogo de productos (categorías, subcategorías, filtros y opciones). Una sola
/// interfaz genérica para las cuatro entidades porque todas se guardan, buscan y borran igual; las reglas propias de
/// cada una viven en sus casos de uso.
/// </summary>
public interface IClasificacionRepository
{
    /// <summary>Árbol completo: categorías con sus subcategorías y cantidades.</summary>
    Task<IReadOnlyList<CategoriaDto>> ObtenerArbolAsync(bool incluirInactivas, CancellationToken ct);
    /// <summary>Filtros de una subcategoría con sus opciones.</summary>
    Task<IReadOnlyList<FiltroDto>> ObtenerFiltrosAsync(int subcategoriaId, bool incluirInactivos, CancellationToken ct);

    /// <summary>Busca un elemento por id (con su padre cargado).</summary>
    Task<T?> ObtenerAsync<T>(int id, CancellationToken ct) where T : ElementoDeCatalogo;
    /// <summary>Marca el elemento como nuevo.</summary>
    void Agregar<T>(T elemento) where T : ElementoDeCatalogo;
    /// <summary>Marca el elemento para borrado definitivo.</summary>
    void Eliminar<T>(T elemento) where T : ElementoDeCatalogo;
    /// <summary>Fija la versión que leyó quien edita, para detectar ediciones simultáneas.</summary>
    void FijarVersionOriginal(ElementoDeCatalogo elemento, byte[] rowVersion);

    /// <summary>Indica si ya hay una categoría con ese nombre (sin distinguir mayúsculas).</summary>
    Task<bool> ExisteCategoriaAsync(string nombre, int? exceptoId, CancellationToken ct);
    /// <summary>Indica si la categoría ya tiene una subcategoría con ese nombre.</summary>
    Task<bool> ExisteSubcategoriaAsync(int categoriaId, string nombre, int? exceptoId, CancellationToken ct);
    /// <summary>Indica si la subcategoría ya tiene un filtro con ese nombre.</summary>
    Task<bool> ExisteFiltroAsync(int subcategoriaId, string nombre, int? exceptoId, CancellationToken ct);
    /// <summary>Indica si el filtro ya tiene una opción con ese valor.</summary>
    Task<bool> ExisteOpcionAsync(int filtroId, string valor, int? exceptoId, CancellationToken ct);

    /// <summary>Cuenta las subcategorías de una categoría.</summary>
    Task<int> ContarSubcategoriasAsync(int categoriaId, bool soloActivas, CancellationToken ct);
    /// <summary>Cuenta los filtros de una subcategoría.</summary>
    Task<int> ContarFiltrosAsync(int subcategoriaId, CancellationToken ct);
    /// <summary>Cuenta los productos de una subcategoría.</summary>
    Task<int> ContarProductosAsync(int subcategoriaId, bool soloActivos, CancellationToken ct);
    /// <summary>Cuenta las opciones de un filtro.</summary>
    Task<int> ContarOpcionesAsync(int filtroId, CancellationToken ct);
    /// <summary>Cuenta los productos que usan una opción.</summary>
    Task<int> ContarUsosDeOpcionAsync(int opcionId, CancellationToken ct);
}
