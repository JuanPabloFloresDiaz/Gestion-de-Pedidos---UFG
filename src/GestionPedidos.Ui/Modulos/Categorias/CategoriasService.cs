using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Categorias;

/// <summary>
/// Operaciones de la clasificación del catálogo (categorías, subcategorías, filtros y opciones) contra la API.
/// Publica <c>EntidadCambiada</c> solo después de que la API confirma; si falla, no se notifica nada.
/// </summary>
public sealed class CategoriasService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public CategoriasService(ApiClient api) => _api = api;

    /// <summary>Se produce tras crear, editar, cambiar el estado o eliminar un elemento.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    /// <summary>Árbol de categorías con sus subcategorías.</summary>
    public Task<List<CategoriaDto>> ArbolAsync(bool incluirInactivas, CancellationToken ct) =>
        _api.GetAsync<List<CategoriaDto>>($"/api/v1/categorias?incluirInactivas={incluirInactivas.ToString().ToLowerInvariant()}", ct);

    /// <summary>Filtros de una subcategoría con sus opciones.</summary>
    public Task<List<FiltroDto>> FiltrosAsync(int subcategoriaId, bool incluirInactivos, CancellationToken ct) =>
        _api.GetAsync<List<FiltroDto>>($"/api/v1/subcategorias/{subcategoriaId}/filtros?incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}", ct);

    // ---- Categorías ----
    /// <summary>Crea una categoría.</summary>
    public async Task<CategoriaDto> CrearCategoriaAsync(GuardarClasificacionSolicitud s, CancellationToken ct)
    {
        var c = await _api.PostAsync<CategoriaDto>("/api/v1/categorias", s, ct);
        Publicar(TipoEntidad.Categoria, c.Id, TipoCambio.Registrada, c.Nombre, c.Activo);
        return c;
    }

    /// <summary>Edita una categoría.</summary>
    public async Task<CategoriaDto> ActualizarCategoriaAsync(int id, GuardarClasificacionSolicitud s, CancellationToken ct)
    {
        var c = await _api.PutAsync<CategoriaDto>($"/api/v1/categorias/{id}", s, ct);
        Publicar(TipoEntidad.Categoria, c.Id, TipoCambio.Actualizada, c.Nombre, c.Activo);
        return c;
    }

    /// <summary>Da de baja o reactiva una categoría.</summary>
    public async Task<CategoriaDto> CambiarEstadoCategoriaAsync(int id, bool activo, CancellationToken ct)
    {
        var c = await _api.PatchAsync<CategoriaDto>($"/api/v1/categorias/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        Publicar(TipoEntidad.Categoria, c.Id, TipoCambio.EstadoCambiado, c.Nombre, c.Activo);
        return c;
    }

    /// <summary>Elimina definitivamente una categoría inactiva y vacía.</summary>
    public async Task EliminarCategoriaAsync(int id, string nombre, CancellationToken ct)
    {
        await _api.DeleteAsync($"/api/v1/categorias/{id}", ct);
        Publicar(TipoEntidad.Categoria, id, TipoCambio.Eliminada, nombre, false);
    }

    // ---- Subcategorías ----
    /// <summary>Crea una subcategoría dentro de una categoría.</summary>
    public async Task<SubcategoriaDto> CrearSubcategoriaAsync(int categoriaId, GuardarClasificacionSolicitud s, CancellationToken ct)
    {
        var x = await _api.PostAsync<SubcategoriaDto>($"/api/v1/categorias/{categoriaId}/subcategorias", s, ct);
        Publicar(TipoEntidad.Subcategoria, x.Id, TipoCambio.Registrada, x.Nombre, x.Activo);
        return x;
    }

    /// <summary>Edita una subcategoría.</summary>
    public async Task<SubcategoriaDto> ActualizarSubcategoriaAsync(int id, GuardarClasificacionSolicitud s, CancellationToken ct)
    {
        var x = await _api.PutAsync<SubcategoriaDto>($"/api/v1/subcategorias/{id}", s, ct);
        Publicar(TipoEntidad.Subcategoria, x.Id, TipoCambio.Actualizada, x.Nombre, x.Activo);
        return x;
    }

    /// <summary>Da de baja o reactiva una subcategoría.</summary>
    public async Task<SubcategoriaDto> CambiarEstadoSubcategoriaAsync(int id, bool activo, CancellationToken ct)
    {
        var x = await _api.PatchAsync<SubcategoriaDto>($"/api/v1/subcategorias/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        Publicar(TipoEntidad.Subcategoria, x.Id, TipoCambio.EstadoCambiado, x.Nombre, x.Activo);
        return x;
    }

    /// <summary>Elimina definitivamente una subcategoría inactiva y sin filtros ni productos.</summary>
    public async Task EliminarSubcategoriaAsync(int id, string nombre, CancellationToken ct)
    {
        await _api.DeleteAsync($"/api/v1/subcategorias/{id}", ct);
        Publicar(TipoEntidad.Subcategoria, id, TipoCambio.Eliminada, nombre, false);
    }

    // ---- Filtros ----
    /// <summary>Crea un filtro dentro de una subcategoría.</summary>
    public async Task<FiltroDto> CrearFiltroAsync(int subcategoriaId, GuardarFiltroSolicitud s, CancellationToken ct)
    {
        var x = await _api.PostAsync<FiltroDto>($"/api/v1/subcategorias/{subcategoriaId}/filtros", s, ct);
        Publicar(TipoEntidad.Filtro, x.Id, TipoCambio.Registrada, x.Nombre, x.Activo);
        return x;
    }

    /// <summary>Renombra un filtro.</summary>
    public async Task<FiltroDto> ActualizarFiltroAsync(int id, GuardarFiltroSolicitud s, CancellationToken ct)
    {
        var x = await _api.PutAsync<FiltroDto>($"/api/v1/filtros/{id}", s, ct);
        Publicar(TipoEntidad.Filtro, x.Id, TipoCambio.Actualizada, x.Nombre, x.Activo);
        return x;
    }

    /// <summary>Da de baja o reactiva un filtro.</summary>
    public async Task<FiltroDto> CambiarEstadoFiltroAsync(int id, bool activo, CancellationToken ct)
    {
        var x = await _api.PatchAsync<FiltroDto>($"/api/v1/filtros/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        Publicar(TipoEntidad.Filtro, x.Id, TipoCambio.EstadoCambiado, x.Nombre, x.Activo);
        return x;
    }

    /// <summary>Elimina definitivamente un filtro inactivo y sin opciones.</summary>
    public async Task EliminarFiltroAsync(int id, string nombre, CancellationToken ct)
    {
        await _api.DeleteAsync($"/api/v1/filtros/{id}", ct);
        Publicar(TipoEntidad.Filtro, id, TipoCambio.Eliminada, nombre, false);
    }

    // ---- Opciones ----
    /// <summary>Crea una opción dentro de un filtro.</summary>
    public async Task<OpcionFiltroDto> CrearOpcionAsync(int filtroId, GuardarOpcionSolicitud s, CancellationToken ct)
    {
        var x = await _api.PostAsync<OpcionFiltroDto>($"/api/v1/filtros/{filtroId}/opciones", s, ct);
        Publicar(TipoEntidad.OpcionFiltro, x.Id, TipoCambio.Registrada, x.Valor, x.Activo);
        return x;
    }

    /// <summary>Cambia el valor de una opción.</summary>
    public async Task<OpcionFiltroDto> ActualizarOpcionAsync(int id, GuardarOpcionSolicitud s, CancellationToken ct)
    {
        var x = await _api.PutAsync<OpcionFiltroDto>($"/api/v1/opciones/{id}", s, ct);
        Publicar(TipoEntidad.OpcionFiltro, x.Id, TipoCambio.Actualizada, x.Valor, x.Activo);
        return x;
    }

    /// <summary>Da de baja o reactiva una opción.</summary>
    public async Task<OpcionFiltroDto> CambiarEstadoOpcionAsync(int id, bool activo, CancellationToken ct)
    {
        var x = await _api.PatchAsync<OpcionFiltroDto>($"/api/v1/opciones/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        Publicar(TipoEntidad.OpcionFiltro, x.Id, TipoCambio.EstadoCambiado, x.Valor, x.Activo);
        return x;
    }

    /// <summary>Elimina definitivamente una opción inactiva que ningún producto use.</summary>
    public async Task EliminarOpcionAsync(int id, string valor, CancellationToken ct)
    {
        await _api.DeleteAsync($"/api/v1/opciones/{id}", ct);
        Publicar(TipoEntidad.OpcionFiltro, id, TipoCambio.Eliminada, valor, false);
    }

    private void Publicar(TipoEntidad entidad, int id, TipoCambio tipo, string descripcion, bool activo) =>
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = entidad, Id = id, Tipo = tipo, Descripcion = descripcion, Activo = activo,
        });
}
