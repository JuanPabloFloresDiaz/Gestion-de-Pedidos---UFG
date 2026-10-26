namespace GestionPedidos.Contracts;

/// <summary>Opción de un filtro en la tienda pública.</summary>
public sealed record OpcionTiendaDto(int Id, string Valor);

/// <summary>Filtro de una subcategoría con sus opciones (solo activas).</summary>
public sealed record FiltroTiendaDto(int Id, string Nombre, IReadOnlyList<OpcionTiendaDto> Opciones);

/// <summary>Subcategoría de la tienda con su cantidad de productos disponibles y sus filtros.</summary>
public sealed record SubcategoriaTiendaDto(int Id, string Nombre, int TotalProductos, IReadOnlyList<FiltroTiendaDto> Filtros);

/// <summary>Categoría de la tienda con sus subcategorías (el menú de navegación).</summary>
public sealed record CategoriaTiendaDto(int Id, string Nombre, string? Descripcion, int TotalProductos, IReadOnlyList<SubcategoriaTiendaDto> Subcategorias,
    string? Codigo = null, string? ImagenUrl = null);

/// <summary>Una característica del producto: el filtro y la opción elegida (por ejemplo, Color: Negro).</summary>
public sealed record DetalleProductoTiendaDto(int FiltroId, string Filtro, int OpcionId, string Valor);

/// <summary>
/// Producto tal como lo ve el público: sin datos internos. <c>Existencias</c> solo viene si el administrador activó
/// "mostrar existencias" para ese producto; <c>MaximoPorPedido</c> limita la cantidad que se puede pedir de una vez.
/// </summary>
public sealed record ProductoTiendaDto(int Id, string Nombre, string Descripcion, decimal Precio, int? CategoriaId, string? Categoria,
    int? SubcategoriaId, string? Subcategoria, bool Disponible, int? Existencias, int MaximoPorPedido, DateOnly FechaIngreso,
    IReadOnlyList<DetalleProductoTiendaDto> Detalles, string? Codigo = null, string? ImagenUrl = null);

/// <summary>Consulta del catálogo público: texto, clasificación, opciones de filtro, precio, orden y página.</summary>
/// <param name="Q">Texto buscado en nombre o descripción.</param>
/// <param name="CategoriaId">Categoría.</param>
/// <param name="SubcategoriaId">Subcategoría.</param>
/// <param name="OpcionIds">Opciones elegidas: <b>Y</b> entre filtros distintos, <b>O</b> entre opciones del mismo filtro.</param>
/// <param name="PrecioMin">Precio mínimo.</param>
/// <param name="PrecioMax">Precio máximo.</param>
/// <param name="SoloDisponibles">Oculta los agotados.</param>
/// <param name="Orden"><c>nuevos</c> (por defecto), <c>precio-asc</c>, <c>precio-desc</c> o <c>nombre</c>.</param>
/// <param name="Pagina">Página (desde 1).</param>
/// <param name="Tamano">Elementos por página.</param>
public sealed record ConsultaCatalogo(string? Q = null, int? CategoriaId = null, int? SubcategoriaId = null,
    IReadOnlyList<int>? OpcionIds = null, decimal? PrecioMin = null, decimal? PrecioMax = null, bool SoloDisponibles = false,
    string? Orden = null, int? Pagina = null, int? Tamano = null);

/// <summary>Resultado de volver a ligar las imágenes del almacén con sus productos y categorías por código.</summary>
/// <param name="Enlazadas">Imágenes que se ligaron (no estaban ligadas).</param>
/// <param name="YaEnlazadas">Imágenes que ya estaban bien ligadas.</param>
/// <param name="SinDestino">Imágenes cuyo código no corresponde a ningún producto ni categoría.</param>
public sealed record ReenlaceImagenesDto(int Enlazadas, int YaEnlazadas, int SinDestino);
