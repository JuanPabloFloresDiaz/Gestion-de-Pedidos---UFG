using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Historial;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Historial;

/// <summary>Endpoints de consulta del historial de actividades (solo lectura, solo administrador).</summary>
public static class HistorialEndpoints
{
    /// <summary>Registra <c>/api/v1/actividades</c>.</summary>
    public static IEndpointRouteBuilder MapHistorial(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/actividades").WithTags("Historial").RequierePermiso(CatalogoPermisos.HistorialVer);

        g.MapGet("/", async (int? usuarioId, string? q, string? entidad, string? accion, DateOnly? desde, DateOnly? hasta,
                int? pagina, int? tamanoPagina, ListarActividades caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(usuarioId, q, entidad, accion, desde, hasta, pagina, tamanoPagina, ct)).AHttp(Results.Ok));

        g.MapGet("/filtros", async (ListarActividades caso, CancellationToken ct) =>
        {
            var (entidades, acciones) = await caso.FiltrosAsync(ct);
            return Results.Ok(new FiltrosHistorialDto(entidades, acciones));
        });
        return app;
    }
}
