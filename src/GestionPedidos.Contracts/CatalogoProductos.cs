namespace GestionPedidos.Contracts;

/// <summary>Opción de un filtro (por ejemplo, "Negro" en "Color").</summary>
public sealed record OpcionFiltroDto(int Id, int FiltroId, string Valor, bool Activo, DateTimeOffset? EliminadoEn, string RowVersion);

/// <summary>Filtro de una subcategoría con sus opciones.</summary>
public sealed record FiltroDto(int Id, int SubcategoriaId, string Nombre, bool Activo, DateTimeOffset? EliminadoEn, string RowVersion,
    IReadOnlyList<OpcionFiltroDto> Opciones);

/// <summary>Subcategoría con la cantidad de filtros y de productos que tiene.</summary>
public sealed record SubcategoriaDto(int Id, int CategoriaId, string Nombre, string? Descripcion, bool Activo, DateTimeOffset? EliminadoEn,
    string RowVersion, int TotalFiltros, int TotalProductos);

/// <summary>Categoría con sus subcategorías (el árbol del catálogo).</summary>
public sealed record CategoriaDto(int Id, string Nombre, string? Descripcion, bool Activo, DateTimeOffset? EliminadoEn, string RowVersion,
    IReadOnlyList<SubcategoriaDto> Subcategorias);

/// <summary>Datos para crear o editar una categoría o una subcategoría (<c>RowVersion</c> solo al editar).</summary>
public sealed record GuardarClasificacionSolicitud(string Nombre, string? Descripcion, string? RowVersion = null);

/// <summary>Datos para crear o editar un filtro.</summary>
public sealed record GuardarFiltroSolicitud(string Nombre, string? RowVersion = null);

/// <summary>Datos para crear o editar una opción de filtro.</summary>
public sealed record GuardarOpcionSolicitud(string Valor, string? RowVersion = null);
