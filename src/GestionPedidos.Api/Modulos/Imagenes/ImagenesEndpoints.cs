using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Imagenes;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Imagenes;

/// <summary>
/// Imágenes de productos y categorías ([CatalogoProductos.md §9]). Las imágenes viven en el almacén de objetos (Garage, compatible con S3);
/// la API las entrega con caché y permite subirlas, quitarlas y volver a ligarlas por código si la base se restaura.
/// </summary>
public static class ImagenesEndpoints
{
    /// <summary>Registra las rutas de imágenes.</summary>
    public static IEndpointRouteBuilder MapImagenes(this IEndpointRouteBuilder app)
    {
        // Pública: la tienda las muestra sin sesión. La clave sale de la base de datos por código, nunca de lo que mande quien llama.
        app.MapGet("/api/v1/imagenes/{tipo}/{codigo}", async (string tipo, string codigo, HttpContext http, ObtenerImagen caso, CancellationToken ct) =>
            {
                var r = await caso.EjecutarAsync(tipo, codigo, ct);
                if (!r.Exito) return r.Error!.AProblema();
                var imagen = r.Valor!;
                var etag = imagen.ETag is null ? null : $"\"{imagen.ETag.Trim('"')}\"";
                http.Response.Headers.CacheControl = "public, max-age=3600";
                if (etag is not null)
                {
                    http.Response.Headers.ETag = etag;
                    if (http.Request.Headers.IfNoneMatch == etag) { await imagen.Contenido.DisposeAsync(); return Results.StatusCode(StatusCodes.Status304NotModified); }
                }
                return Results.Stream(imagen.Contenido, imagen.TipoContenido, lastModified: imagen.Modificada, enableRangeProcessing: false);
            })
            .WithTags("Imágenes").AllowAnonymous().RequireRateLimiting("tienda");

        // Subir o quitar la imagen de una categoría o de un producto (JWT, sin cookies: no hay riesgo de CSRF, por eso sin antiforgery)
        MapSubida(app, "categorias", TipoImagen.Categoria, CatalogoPermisos.CategoriasEditar);
        MapSubida(app, "productos", TipoImagen.Producto, CatalogoPermisos.ProductosEditar);

        // Recuperación tras restaurar la base: vuelve a ligar cada imagen del almacén con su producto o categoría por código
        app.MapPost("/api/v1/imagenes/reenlazar", async (HttpContext http, GestionarImagenes caso, CancellationToken ct) =>
                Results.Ok(await caso.ReenlazarAsync(http.User.IdUsuario(), ct)))
            .WithTags("Imágenes").RequierePermiso(CatalogoPermisos.CategoriasEditar);
        return app;
    }

    private static void MapSubida(IEndpointRouteBuilder app, string carpeta, TipoImagen tipo, string permiso)
    {
        app.MapPut($"/api/v1/{carpeta}/{{id:int}}/imagen", async (int id, IFormFile archivo, HttpContext http, GestionarImagenes caso, CancellationToken ct) =>
            {
                await using var flujo = archivo.OpenReadStream();
                return (await caso.SubirAsync(tipo, id, flujo, http.User.IdUsuario(), ct)).AHttp(Results.Ok);
            })
            .WithTags("Imágenes").RequierePermiso(permiso).DisableAntiforgery();

        app.MapDelete($"/api/v1/{carpeta}/{{id:int}}/imagen", async (int id, HttpContext http, GestionarImagenes caso, CancellationToken ct) =>
                (await caso.QuitarAsync(tipo, id, http.User.IdUsuario(), ct)).AHttp())
            .WithTags("Imágenes").RequierePermiso(permiso);
    }
}
