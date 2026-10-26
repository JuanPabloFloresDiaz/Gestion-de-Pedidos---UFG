using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Catalogo;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Clasificacion;

/// <summary>
/// Endpoints de la clasificación del catálogo de productos: categorías, subcategorías, filtros y opciones
/// ([CatalogoProductos.md §5]). Un solo grupo de permisos <c>categorias.*</c> los protege a todos.
/// </summary>
public static class ClasificacionEndpoints
{
    /// <summary>Registra las rutas de la clasificación.</summary>
    public static IEndpointRouteBuilder MapClasificacion(this IEndpointRouteBuilder app)
    {
        var raiz = app.MapGroup("/api/v1").WithTags("Clasificación del catálogo");

        // ---- Categorías ----
        raiz.MapGet("/categorias", async (bool? incluirInactivas, ObtenerArbolDeCatalogo caso, CancellationToken ct) =>
                Results.Ok(await caso.EjecutarAsync(incluirInactivas ?? false, ct)))
            .RequierePermiso(CatalogoPermisos.CategoriasVer);
        raiz.MapPost("/categorias", async (GuardarClasificacionSolicitud s, HttpContext http, GestionarCategorias caso, CancellationToken ct) =>
                (await caso.CrearAsync(s, http.User.IdUsuario(), ct)).AHttp(c => Results.Created($"/api/v1/categorias/{c.Id}", c)))
            .RequierePermiso(CatalogoPermisos.CategoriasCrear);
        raiz.MapPut("/categorias/{id:int}", async (int id, GuardarClasificacionSolicitud s, HttpContext http, GestionarCategorias caso, CancellationToken ct) =>
                (await caso.ActualizarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasEditar);
        raiz.MapPatch("/categorias/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http, GestionarCategorias caso, CancellationToken ct) =>
                (await caso.CambiarEstadoAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasDesactivar);
        raiz.MapDelete("/categorias/{id:int}", async (int id, HttpContext http, GestionarCategorias caso, CancellationToken ct) =>
                (await caso.EliminarAsync(id, http.User.IdUsuario(), ct)).AHttp())
            .RequierePermiso(CatalogoPermisos.CategoriasEliminar);

        // ---- Subcategorías ----
        raiz.MapPost("/categorias/{categoriaId:int}/subcategorias", async (int categoriaId, GuardarClasificacionSolicitud s, HttpContext http,
                GestionarSubcategorias caso, CancellationToken ct) =>
                (await caso.CrearAsync(categoriaId, s, http.User.IdUsuario(), ct)).AHttp(x => Results.Created($"/api/v1/subcategorias/{x.Id}", x)))
            .RequierePermiso(CatalogoPermisos.CategoriasCrear);
        raiz.MapPut("/subcategorias/{id:int}", async (int id, GuardarClasificacionSolicitud s, HttpContext http, GestionarSubcategorias caso, CancellationToken ct) =>
                (await caso.ActualizarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasEditar);
        raiz.MapPatch("/subcategorias/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http, GestionarSubcategorias caso, CancellationToken ct) =>
                (await caso.CambiarEstadoAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasDesactivar);
        raiz.MapDelete("/subcategorias/{id:int}", async (int id, HttpContext http, GestionarSubcategorias caso, CancellationToken ct) =>
                (await caso.EliminarAsync(id, http.User.IdUsuario(), ct)).AHttp())
            .RequierePermiso(CatalogoPermisos.CategoriasEliminar);

        // ---- Filtros ----
        raiz.MapGet("/subcategorias/{id:int}/filtros", async (int id, bool? incluirInactivos, ObtenerFiltrosDeSubcategoria caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, incluirInactivos ?? false, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasVer);
        raiz.MapPost("/subcategorias/{id:int}/filtros", async (int id, GuardarFiltroSolicitud s, HttpContext http, GestionarFiltros caso, CancellationToken ct) =>
                (await caso.CrearAsync(id, s, http.User.IdUsuario(), ct)).AHttp(f => Results.Created($"/api/v1/filtros/{f.Id}", f)))
            .RequierePermiso(CatalogoPermisos.CategoriasCrear);
        raiz.MapPut("/filtros/{id:int}", async (int id, GuardarFiltroSolicitud s, HttpContext http, GestionarFiltros caso, CancellationToken ct) =>
                (await caso.ActualizarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasEditar);
        raiz.MapPatch("/filtros/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http, GestionarFiltros caso, CancellationToken ct) =>
                (await caso.CambiarEstadoAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasDesactivar);
        raiz.MapDelete("/filtros/{id:int}", async (int id, HttpContext http, GestionarFiltros caso, CancellationToken ct) =>
                (await caso.EliminarAsync(id, http.User.IdUsuario(), ct)).AHttp())
            .RequierePermiso(CatalogoPermisos.CategoriasEliminar);

        // ---- Opciones de filtro ----
        raiz.MapPost("/filtros/{id:int}/opciones", async (int id, GuardarOpcionSolicitud s, HttpContext http, GestionarOpciones caso, CancellationToken ct) =>
                (await caso.CrearAsync(id, s, http.User.IdUsuario(), ct)).AHttp(o => Results.Created($"/api/v1/opciones/{o.Id}", o)))
            .RequierePermiso(CatalogoPermisos.CategoriasCrear);
        raiz.MapPut("/opciones/{id:int}", async (int id, GuardarOpcionSolicitud s, HttpContext http, GestionarOpciones caso, CancellationToken ct) =>
                (await caso.ActualizarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasEditar);
        raiz.MapPatch("/opciones/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http, GestionarOpciones caso, CancellationToken ct) =>
                (await caso.CambiarEstadoAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.CategoriasDesactivar);
        raiz.MapDelete("/opciones/{id:int}", async (int id, HttpContext http, GestionarOpciones caso, CancellationToken ct) =>
                (await caso.EliminarAsync(id, http.User.IdUsuario(), ct)).AHttp())
            .RequierePermiso(CatalogoPermisos.CategoriasEliminar);

        return app;
    }
}
