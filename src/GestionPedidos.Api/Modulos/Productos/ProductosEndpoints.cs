using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Productos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Productos;

/// <summary>Endpoints de administración del catálogo de productos ([Productos.md]).</summary>
public static class ProductosEndpoints
{
    /// <summary>Registra las rutas bajo <c>/api/v1/productos</c>.</summary>
    /// <param name="app">Enrutador de endpoints de la aplicación.</param>
    /// <returns>El enrutador para llamadas fluidas.</returns>
    public static IEndpointRouteBuilder MapProductos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/productos").WithTags("Productos");

        g.MapGet("/", async (string? q, int? categoriaId, int? subcategoriaId, bool? activo, int? pagina, int? tamanoPagina,
                BuscarProductos caso, CancellationToken ct) =>
                Results.Ok(await caso.EjecutarAsync(q, categoriaId, subcategoriaId, activo, pagina, tamanoPagina, ct)))
            .RequierePermiso(CatalogoPermisos.ProductosVer);

        g.MapGet("/{id:int}", async (int id, ObtenerProducto caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.ProductosVer);

        g.MapPost("/", async (CrearProductoSolicitud s, HttpContext http, CrearProducto caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(s, http.User.IdUsuario(), ct))
                    .AHttp(p => Results.Created($"/api/v1/productos/{p.Id}", p)))
            .RequierePermiso(CatalogoPermisos.ProductosCrear);

        g.MapPut("/{id:int}", async (int id, ActualizarProductoSolicitud s, HttpContext http, ActualizarProducto caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.ProductosEditar);

        g.MapPatch("/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http, CambiarEstadoProducto caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.ProductosDesactivar);

        return app;
    }
}
