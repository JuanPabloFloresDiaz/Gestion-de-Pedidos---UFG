using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Catalogos;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Catalogos;

/// <summary>Endpoints de los catálogos geográficos de El Salvador.</summary>
public static class CatalogosEndpoints
{
    /// <summary>Registra departamentos y ciudades.</summary>
    public static IEndpointRouteBuilder MapCatalogos(this IEndpointRouteBuilder app)
    {
        // Datos públicos y oficiales: el registro de la tienda web los necesita sin haber iniciado sesión.
        app.MapGet("/api/v1/departamentos", async (ListarDepartamentos caso, CancellationToken ct) => Results.Ok(await caso.EjecutarAsync(ct)))
            .WithTags("Catálogos").AllowAnonymous();

        app.MapGet("/api/v1/departamentos/{id:int}/ciudades", async (int id, string? q, ListarCiudades caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, q, 1, 100, ct)).AHttp(p => Results.Ok(p.Elementos)))   // como máximo 33 por departamento
            .WithTags("Catálogos").AllowAnonymous();

        // Consulta paginada de todas las ciudades para la administración.
        app.MapGet("/api/v1/ciudades", async (int? departamentoId, string? q, int? pagina, int? tamanoPagina, ListarCiudades caso,
                CancellationToken ct) => (await caso.EjecutarAsync(departamentoId, q, pagina, tamanoPagina, ct)).AHttp(Results.Ok))
            .WithTags("Catálogos").RequierePermiso(CatalogoPermisos.CatalogosVer);
        return app;
    }
}
