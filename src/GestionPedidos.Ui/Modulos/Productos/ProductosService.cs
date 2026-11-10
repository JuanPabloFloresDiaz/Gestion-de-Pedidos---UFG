using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Productos;

/// <summary>
/// Operaciones del catálogo de productos contra la API (bloque P).
/// <b>Publica <see cref="IPublicadorDeCambios.EntidadCambiada"/> solo después de que la API confirme</b>
/// para notificar cambios en la interfaz y refrescar listados abiertos.
/// </summary>
public sealed class ProductosService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio con el cliente API autenticado.</summary>
    /// <param name="api">Cliente HTTP.</param>
    public ProductosService(ApiClient api) => _api = api;

    /// <summary>Se produce tras crear, editar o cambiar el estado de un producto.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    /// <summary>Busca productos con filtros y paginación.</summary>
    /// <param name="q">Texto buscado en nombre, descripción o código.</param>
    /// <param name="categoriaId">Filtro opcional por categoría.</param>
    /// <param name="subcategoriaId">Filtro opcional por subcategoría.</param>
    /// <param name="activo">Filtro por estado: <c>true</c> activos, <c>false</c> inactivos, <c>null</c> todos.</param>
    /// <param name="pagina">Número de página (desde 1).</param>
    /// <param name="tamano">Elementos por página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Página de productos.</returns>
    public Task<PaginaDto<ProductoDto>> BuscarAsync(string? q, int? categoriaId, int? subcategoriaId, bool? activo, int pagina, int tamano, CancellationToken ct)
    {
        var parametros = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamano}" };
        if (!string.IsNullOrWhiteSpace(q)) parametros.Add($"q={Uri.EscapeDataString(q.Trim())}");
        if (categoriaId is not null) parametros.Add($"categoriaId={categoriaId.Value}");
        if (subcategoriaId is not null) parametros.Add($"subcategoriaId={subcategoriaId.Value}");
        if (activo is not null) parametros.Add($"activo={activo.Value.ToString().ToLowerInvariant()}");
        return _api.GetAsync<PaginaDto<ProductoDto>>($"/api/v1/productos?{string.Join('&', parametros)}", ct);
    }

    /// <summary>Obtiene un producto por su identificador.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Detalle del producto.</returns>
    public Task<ProductoDto> ObtenerAsync(int id, CancellationToken ct) =>
        _api.GetAsync<ProductoDto>($"/api/v1/productos/{id}", ct);

    /// <summary>Crea un producto nuevo y notifica el alta.</summary>
    /// <param name="solicitud">Datos del producto.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Producto creado.</returns>
    public async Task<ProductoDto> CrearAsync(CrearProductoSolicitud solicitud, CancellationToken ct)
    {
        var creado = await _api.PostAsync<ProductoDto>("/api/v1/productos", solicitud, ct);
        OnEntidadCambiada(creado, TipoCambio.Registrada);
        return creado;
    }

    /// <summary>Actualiza un producto existente y notifica el cambio.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="solicitud">Nuevos datos y versión de concurrencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Producto actualizado.</returns>
    public async Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoSolicitud solicitud, CancellationToken ct)
    {
        var actualizado = await _api.PutAsync<ProductoDto>($"/api/v1/productos/{id}", solicitud, ct);
        OnEntidadCambiada(actualizado, TipoCambio.Actualizada);
        return actualizado;
    }

    /// <summary>Da de baja lógica o reactiva un producto y notifica el cambio.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="activo">Nuevo estado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Producto con el estado resultante.</returns>
    public async Task<ProductoDto> CambiarEstadoAsync(int id, bool activo, CancellationToken ct)
    {
        var p = await _api.PatchAsync<ProductoDto>($"/api/v1/productos/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        OnEntidadCambiada(p, TipoCambio.EstadoCambiado);
        return p;
    }

    /// <summary>Obtiene el árbol de clasificación de categorías y subcategorías (Bloque K).</summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de categorías con sus subcategorías.</returns>
    public Task<IReadOnlyList<CategoriaDto>> ObtenerCategoriasAsync(CancellationToken ct) =>
        _api.GetAsync<IReadOnlyList<CategoriaDto>>("/api/v1/categorias", ct);

    /// <summary>Obtiene los filtros y opciones de una subcategoría (Bloque K).</summary>
    /// <param name="subcategoriaId">Identificador de la subcategoría.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de filtros con sus opciones disponibles.</returns>
    public Task<IReadOnlyList<FiltroDto>> ObtenerFiltrosAsync(int subcategoriaId, CancellationToken ct) =>
        _api.GetAsync<IReadOnlyList<FiltroDto>>($"/api/v1/subcategorias/{subcategoriaId}/filtros", ct);

    private void OnEntidadCambiada(ProductoDto p, TipoCambio tipo) =>
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Producto,
            Id = p.Id,
            Tipo = tipo,
            Descripcion = p.Nombre,
            Activo = p.Activo,
        });
}
