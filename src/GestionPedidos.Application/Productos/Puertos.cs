using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Productos;

/// <summary>Puerto del repositorio de productos.</summary>
public interface IProductoRepository
{
    /// <summary>Busca un producto por su identificador (con subcategoría y opciones cargadas).</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Producto encontrado o <c>null</c>.</returns>
    Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct);

    /// <summary>Busca productos con filtros y paginación.</summary>
    /// <param name="q">Texto buscado.</param>
    /// <param name="categoriaId">Filtro por categoría.</param>
    /// <param name="subcategoriaId">Filtro por subcategoría.</param>
    /// <param name="activo">Filtro por estado activo/inactivo.</param>
    /// <param name="pagina">Número de página.</param>
    /// <param name="tamano">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de productos y total encontrado.</returns>
    Task<(IReadOnlyList<Producto> Elementos, int Total)> BuscarAsync(string? q, int? categoriaId, int? subcategoriaId, bool? activo, int pagina, int tamano, CancellationToken ct);

    /// <summary>Comprueba si ya existe un producto con el mismo nombre.</summary>
    /// <param name="nombre">Nombre a verificar.</param>
    /// <param name="exceptoId">Identificador a excluir (en ediciones).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><c>true</c> si existe, <c>false</c> en caso contrario.</returns>
    Task<bool> ExisteNombreAsync(string nombre, int? exceptoId, CancellationToken ct);

    /// <summary>Comprueba que la subcategoría exista y esté activa.</summary>
    /// <param name="subcategoriaId">Identificador de la subcategoría.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><c>true</c> si existe y está activa.</returns>
    Task<bool> ExisteSubcategoriaActivaAsync(int subcategoriaId, CancellationToken ct);

    /// <summary>Valida que las opciones pertenezcan a filtros de la subcategoría indicada.</summary>
    /// <param name="subcategoriaId">Identificador de la subcategoría.</param>
    /// <param name="opcionIds">Identificadores de las opciones.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de pares (FiltroId, OpcionId) válidos.</returns>
    Task<IReadOnlyList<(int FiltroId, int OpcionId)>> ObtenerOpcionesValidasAsync(int subcategoriaId, IEnumerable<int> opcionIds, CancellationToken ct);

    /// <summary>Fija la versión previa para detección de concurrencia optimista.</summary>
    /// <param name="producto">Producto en edición.</param>
    /// <param name="rowVersion">Versión de fila previa.</param>
    void FijarVersionOriginal(Producto producto, byte[] rowVersion);

    /// <summary>Agrega un nuevo producto a la persistencia.</summary>
    /// <param name="producto">Producto a persistir.</param>
    void Agregar(Producto producto);
}
