using GestionPedidos.Api.Errores;
using GestionPedidos.Application.Tienda;
using GestionPedidos.Contracts;

namespace GestionPedidos.Api.Modulos.Tienda;

/// <summary>Endpoints públicos de la tienda: explorar el catálogo sin iniciar sesión ([PortalWeb.md §4]).</summary>
public static class TiendaEndpoints
{
    /// <summary>Registra <c>/api/v1/tienda/categorias</c> y <c>/api/v1/catalogo</c> (anónimos, con límite de peticiones).</summary>
    public static IEndpointRouteBuilder MapTienda(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1").WithTags("Tienda pública").AllowAnonymous().RequireRateLimiting("tienda");

        g.MapGet("/tienda/categorias", async (ConsultarCatalogoPublico caso, CancellationToken ct) =>
            Results.Ok(await caso.CategoriasAsync(ct)));

        g.MapGet("/catalogo", async (string? q, int? categoriaId, int? subcategoriaId, string? opcionIds, decimal? precioMin, decimal? precioMax,
                bool? soloDisponibles, string? orden, int? pagina, int? tamanoPagina, ConsultarCatalogoPublico caso, CancellationToken ct) =>
            {
                var opciones = (opcionIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => int.TryParse(x, out var n) ? n : (int?)null).Where(n => n is not null).Select(n => n!.Value).ToList();
                var consulta = new ConsultaCatalogo(q, categoriaId, subcategoriaId, opciones, precioMin, precioMax, soloDisponibles ?? false, orden, pagina, tamanoPagina);
                return (await caso.BuscarAsync(consulta, ct)).AHttp(Results.Ok);
            });

        g.MapGet("/catalogo/{id:int}", async (int id, ConsultarCatalogoPublico caso, CancellationToken ct) =>
            (await caso.ObtenerAsync(id, ct)).AHttp(Results.Ok));

        return app;
    }
}
