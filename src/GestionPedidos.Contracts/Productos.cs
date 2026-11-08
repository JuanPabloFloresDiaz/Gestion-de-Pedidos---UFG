namespace GestionPedidos.Contracts;

/// <summary>Detalle de opción de filtro asignada a un producto (por ejemplo: Color = Negro).</summary>
/// <param name="FiltroId">Identificador del filtro.</param>
/// <param name="Filtro">Nombre del filtro (ej. "Color", "Talla", "Marca").</param>
/// <param name="OpcionId">Identificador de la opción seleccionada.</param>
/// <param name="Valor">Valor de la opción (ej. "Negro", "42", "Samsung").</param>
public sealed record DetalleProductoDto(int FiltroId, string Filtro, int OpcionId, string Valor);

/// <summary>Producto del inventario para la administración y sus listados.</summary>
/// <param name="Id">Identificador único del producto.</param>
/// <param name="Codigo">Código único e irrepetible (ej. <c>PRD-000123</c> o <c>ROP-CAM-001</c>).</param>
/// <param name="Nombre">Nombre del producto.</param>
/// <param name="Descripcion">Descripción del producto.</param>
/// <param name="PrecioVenta">Precio de venta actual.</param>
/// <param name="CantidadStock">Cantidad disponible en el inventario.</param>
/// <param name="FechaIngreso">Fecha en la que ingresó al inventario.</param>
/// <param name="CategoriaId">Identificador de la categoría padre (si pertenece a una subcategoría).</param>
/// <param name="Categoria">Nombre de la categoría padre.</param>
/// <param name="SubcategoriaId">Identificador de la subcategoría que lo clasifica (opcional).</param>
/// <param name="Subcategoria">Nombre de la subcategoría.</param>
/// <param name="MostrarStock">Indica si la tienda pública debe mostrar las existencias exactas.</param>
/// <param name="Activo">Indica si el producto está activo (baja lógica).</param>
/// <param name="ImagenClave">Clave del archivo de imagen en Garage S3 (ej. <c>productos/PRD-000123.jpg</c>).</param>
/// <param name="ImagenUrl">URL relativa para visualizar la imagen en el cliente.</param>
/// <param name="RowVersion">Versión en base64 para concurrencia optimista.</param>
/// <param name="EliminadoEn">Fecha y hora en que se dio de baja lógica; <c>null</c> si está activo.</param>
/// <param name="Detalles">Detalles de filtros y opciones asignados.</param>
public sealed record ProductoDto(
    int Id,
    string Codigo,
    string Nombre,
    string Descripcion,
    decimal PrecioVenta,
    int CantidadStock,
    DateOnly FechaIngreso,
    int? CategoriaId,
    string? Categoria,
    int? SubcategoriaId,
    string? Subcategoria,
    bool MostrarStock,
    bool Activo,
    string? ImagenClave,
    string? ImagenUrl,
    string RowVersion,
    DateTimeOffset? EliminadoEn = null,
    IReadOnlyList<DetalleProductoDto>? Detalles = null);

/// <summary>Datos para dar de alta un producto.</summary>
/// <param name="Nombre">Nombre obligatorio del producto (1–150).</param>
/// <param name="Descripcion">Descripción obligatoria (1–500).</param>
/// <param name="PrecioVenta">Precio de venta no negativo.</param>
/// <param name="CantidadStock">Existencias iniciales no negativas.</param>
/// <param name="FechaIngreso">Fecha de ingreso (no puede ser futura).</param>
/// <param name="SubcategoriaId">Subcategoría opcional para clasificación.</param>
/// <param name="MostrarStock">Si se muestran las existencias en la tienda.</param>
/// <param name="OpcionIds">Lista de identificadores de opciones elegidas para cada filtro.</param>
/// <param name="Codigo">Código manual opcional; si no se especifica, la base lo genera.</param>
public sealed record CrearProductoSolicitud(
    string Nombre,
    string Descripcion,
    decimal PrecioVenta,
    int CantidadStock,
    DateOnly FechaIngreso,
    int? SubcategoriaId,
    bool MostrarStock,
    IReadOnlyList<int>? OpcionIds = null,
    string? Codigo = null);

/// <summary>Datos para actualizar un producto existente.</summary>
/// <param name="Nombre">Nombre obligatorio del producto (1–150).</param>
/// <param name="Descripcion">Descripción obligatoria (1–500).</param>
/// <param name="PrecioVenta">Precio de venta no negativo.</param>
/// <param name="CantidadStock">Existencias ajustadas no negativas.</param>
/// <param name="FechaIngreso">Fecha de ingreso (no puede ser futura).</param>
/// <param name="SubcategoriaId">Subcategoría opcional.</param>
/// <param name="MostrarStock">Si se muestran las existencias en la tienda.</param>
/// <param name="RowVersion">Versión previa leída para concurrencia optimista.</param>
/// <param name="OpcionIds">Lista de identificadores de opciones elegidas para cada filtro.</param>
public sealed record ActualizarProductoSolicitud(
    string Nombre,
    string Descripcion,
    decimal PrecioVenta,
    int CantidadStock,
    DateOnly FechaIngreso,
    int? SubcategoriaId,
    bool MostrarStock,
    string? RowVersion,
    IReadOnlyList<int>? OpcionIds = null);
